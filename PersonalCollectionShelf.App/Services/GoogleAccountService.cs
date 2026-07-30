using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.Infrastructure.Services.Firebase;

namespace PersonalCollectionShelf.App.Services;

public sealed class GoogleAccountService(
    HttpClient httpClient,
    FirebaseOptions options,
    IAuthService firebaseAuthService,
    ILocalizationService localizationService) : IGoogleAccountService
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string AccessTokenKey = "google.oauth.accessToken";
    private const string RefreshTokenKey = "google.oauth.refreshToken";
    private const string ExpiryKey = "google.oauth.expiresAtUtc";
    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromMinutes(2);

    public bool IsConfigured => options.IsGoogleConfigured;

    public async Task<AuthResultDto> SignInAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return AuthResultDto.Failure("Auth.Error.GoogleNotConfigured");
        }

        try
        {
            var authorization = await AuthorizeAsync(cancellationToken);
            var result = await firebaseAuthService.SignInWithGoogleAsync(
                authorization.IdToken,
                authorization.AccessToken,
                cancellationToken);
            if (!result.Succeeded)
            {
                CrashReporter.LogMessage(
                    "GoogleAccountService.FirebaseSignIn",
                    $"Firebase Google sign-in failed with localization key: {result.ErrorKey ?? "Auth.Error.Unknown"}");
                return result;
            }

            await SaveTokensAsync(authorization);
            return result;
        }
        catch (OperationCanceledException)
        {
            return AuthResultDto.Failure("Auth.Error.Cancelled");
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException)
        {
            CrashReporter.Log(exception, "GoogleAccountService.SignInAsync");
            return AuthResultDto.Failure("Auth.Error.Network");
        }
    }

    public async Task<string?> GetDriveAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var accessToken = await SecureStorage.Default.GetAsync(AccessTokenKey);
        var expiryRaw = await SecureStorage.Default.GetAsync(ExpiryKey);
        if (!string.IsNullOrWhiteSpace(accessToken) &&
            DateTime.TryParse(expiryRaw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expiry) &&
            expiry.ToUniversalTime() > DateTime.UtcNow + ExpiryBuffer)
        {
            return accessToken;
        }

        var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var refreshFields = new Dictionary<string, string>
        {
            ["client_id"] = options.GoogleClientId,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        };
        AddClientSecret(refreshFields);
        using var response = await PostFormWithTransientRetryAsync(TokenEndpoint, refreshFields, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        accessToken = document.RootElement.GetProperty("access_token").GetString();
        var expiresIn = document.RootElement.GetProperty("expires_in").GetInt32();
        if (accessToken is null)
        {
            return null;
        }

        await SecureStorage.Default.SetAsync(AccessTokenKey, accessToken);
        await SecureStorage.Default.SetAsync(ExpiryKey, DateTime.UtcNow.AddSeconds(expiresIn).ToString("O"));
        return accessToken;
    }

    public Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(RefreshTokenKey);
        SecureStorage.Default.Remove(ExpiryKey);
        return firebaseAuthService.SignOutAsync(cancellationToken);
    }

    private async Task<GoogleAuthorization> AuthorizeAsync(CancellationToken cancellationToken)
    {
        var codeVerifier = Base64Url(RandomNumberGenerator.GetBytes(48));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
        var state = Base64Url(RandomNumberGenerator.GetBytes(24));

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var redirectUri = $"http://127.0.0.1:{port}/";
        var authorizationUri = new Uri(
            "https://accounts.google.com/o/oauth2/v2/auth" +
            $"?client_id={Uri.EscapeDataString(options.GoogleClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            "&response_type=code" +
            "&scope=openid%20email%20profile%20https%3A%2F%2Fwww.googleapis.com%2Fauth%2Fdrive.file" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&code_challenge={Uri.EscapeDataString(challenge)}" +
            "&code_challenge_method=S256&access_type=offline&prompt=consent");

        await Browser.Default.OpenAsync(authorizationUri, BrowserLaunchMode.SystemPreferred);
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(cancellationToken)
            ?? throw new IOException("Google OAuth callback was empty.");
        var target = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1)
            ?? throw new IOException("Google OAuth callback was invalid.");
        var query = ParseQuery(new Uri(new Uri(redirectUri), target).Query);
        var responseBody = $"<html><body><h2>Personal Collection Shelf</h2><p>{WebUtility.HtmlEncode(localizationService.GetString("Auth.Google.Completed"))}</p></body></html>";
        var responseBytes = Encoding.UTF8.GetBytes(responseBody);
        var headers = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {responseBytes.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(headers, cancellationToken);
        await stream.WriteAsync(responseBytes, cancellationToken);

        if (!query.TryGetValue("state", out var returnedState) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state), Encoding.UTF8.GetBytes(returnedState)))
        {
            throw new IOException("Google OAuth state validation failed.");
        }

        if (!query.TryGetValue("code", out var code))
        {
            throw new OperationCanceledException("Google authorization was cancelled.");
        }

        var tokenFields = new Dictionary<string, string>
        {
            ["client_id"] = options.GoogleClientId,
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        };
        AddClientSecret(tokenFields);
        using var tokenResponse = await PostFormWithTransientRetryAsync(TokenEndpoint, tokenFields, cancellationToken);
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            var oauthError = ReadOAuthError(tokenJson);
            CrashReporter.LogMessage(
                "GoogleAccountService.TokenExchange",
                $"Google token endpoint returned HTTP {(int)tokenResponse.StatusCode} ({tokenResponse.StatusCode}); error={oauthError}.");
            throw new IOException($"Google OAuth token exchange failed: {oauthError}.");
        }

        using var tokenDocument = JsonDocument.Parse(tokenJson);
        var root = tokenDocument.RootElement;
        return new GoogleAuthorization(
            root.GetProperty("access_token").GetString()!,
            root.GetProperty("id_token").GetString()!,
            root.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() : null,
            DateTime.UtcNow.AddSeconds(root.GetProperty("expires_in").GetInt32()));
    }

    private static async Task SaveTokensAsync(GoogleAuthorization authorization)
    {
        await SecureStorage.Default.SetAsync(AccessTokenKey, authorization.AccessToken);
        await SecureStorage.Default.SetAsync(ExpiryKey, authorization.ExpiresAtUtc.ToString("O"));
        if (!string.IsNullOrWhiteSpace(authorization.RefreshToken))
        {
            await SecureStorage.Default.SetAsync(RefreshTokenKey, authorization.RefreshToken);
        }
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        return query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                pair => Uri.UnescapeDataString(pair[0]),
                pair => Uri.UnescapeDataString(pair.Length > 1 ? pair[1].Replace('+', ' ') : string.Empty),
                StringComparer.Ordinal);
    }

    private static string ReadOAuthError(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var code = root.TryGetProperty("error", out var error) ? error.GetString() : null;
            var description = root.TryGetProperty("error_description", out var details) ? details.GetString() : null;
            return string.IsNullOrWhiteSpace(description)
                ? code ?? "unknown"
                : $"{code ?? "unknown"}: {description}";
        }
        catch (JsonException)
        {
            return "invalid_response";
        }
    }

    private void AddClientSecret(IDictionary<string, string> fields)
    {
        if (!string.IsNullOrWhiteSpace(options.GoogleClientSecret))
        {
            fields["client_secret"] = options.GoogleClientSecret;
        }
    }

    private async Task<HttpResponseMessage> PostFormWithTransientRetryAsync(
        string url,
        IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var content = new FormUrlEncodedContent(fields);
                return await httpClient.PostAsync(url, content, cancellationToken);
            }
            catch (HttpRequestException exception) when (attempt < 3)
            {
                CrashReporter.LogMessage(
                    "GoogleAccountService.TransientNetworkRetry",
                    $"Google request failed before receiving a response; retry {attempt + 1}/3. {exception.GetType().Name}: {exception.Message}");
                await Task.Delay(TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt)), cancellationToken);
            }
        }
    }

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record GoogleAuthorization(
        string AccessToken,
        string IdToken,
        string? RefreshToken,
        DateTime ExpiresAtUtc);
}
