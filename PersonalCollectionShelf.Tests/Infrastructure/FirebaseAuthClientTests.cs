using System.Net;
using System.Text;
using PersonalCollectionShelf.Infrastructure.Services.Firebase;

namespace PersonalCollectionShelf.Tests.Infrastructure;

public sealed class FirebaseAuthClientTests
{
    private static readonly FirebaseOptions Options = new() { ApiKey = "test-key", ProjectId = "test-project" };

    [Fact]
    public async Task SignInAsync_parses_tokens_from_successful_response()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, """
            {
              "localId": "uid-1",
              "email": "user@example.com",
              "idToken": "id-token",
              "refreshToken": "refresh-token",
              "expiresIn": "3600"
            }
            """);
        var client = new FirebaseAuthClient(new HttpClient(handler), Options);

        var tokens = await client.SignInAsync("user@example.com", "secret");

        Assert.Equal("uid-1", tokens.UserId);
        Assert.Equal("user@example.com", tokens.Email);
        Assert.Equal("id-token", tokens.IdToken);
        Assert.Equal("refresh-token", tokens.RefreshToken);
        Assert.True(tokens.ExpiresAtUtc > DateTime.UtcNow.AddMinutes(50));
        Assert.Contains("signInWithPassword", handler.LastRequestUri);
        Assert.Contains("key=test-key", handler.LastRequestUri);
    }

    [Fact]
    public async Task SignUpAsync_throws_with_error_code_stripped_from_details()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.BadRequest, """
            { "error": { "message": "WEAK_PASSWORD : Password should be at least 6 characters" } }
            """);
        var client = new FirebaseAuthClient(new HttpClient(handler), Options);

        var exception = await Assert.ThrowsAsync<FirebaseAuthException>(
            () => client.SignUpAsync("user@example.com", "123"));

        Assert.Equal("WEAK_PASSWORD", exception.ErrorCode);
    }

    [Fact]
    public async Task RefreshAsync_parses_tokens_from_secure_token_response()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, """
            {
              "user_id": "uid-1",
              "id_token": "new-id-token",
              "refresh_token": "new-refresh-token",
              "expires_in": "3600"
            }
            """);
        var client = new FirebaseAuthClient(new HttpClient(handler), Options);

        var tokens = await client.RefreshAsync("old-refresh-token");

        Assert.Equal("uid-1", tokens.UserId);
        Assert.Equal("new-id-token", tokens.IdToken);
        Assert.Equal("new-refresh-token", tokens.RefreshToken);
        Assert.Contains("securetoken", handler.LastRequestUri);
    }

    [Fact]
    public async Task SignInAsync_throws_invalid_response_when_body_is_not_json()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "not json");
        var client = new FirebaseAuthClient(new HttpClient(handler), Options);

        var exception = await Assert.ThrowsAsync<FirebaseAuthException>(
            () => client.SignInAsync("user@example.com", "secret"));

        Assert.Equal("INVALID_RESPONSE", exception.ErrorCode);
    }

    private sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public string LastRequestUri { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.ToString() ?? string.Empty;

            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
