using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Text.Json;
using PersonalCollectionShelf.Application.Interfaces;

namespace PersonalCollectionShelf.App.Services;

public sealed class GoogleDriveCloudAssetStore(
    HttpClient httpClient,
    IGoogleAccountService googleAccountService) : ICloudAssetStore
{
    private const string DriveApi = "https://www.googleapis.com/drive/v3";
    private const string UploadApi = "https://www.googleapis.com/upload/drive/v3";
    private const string FolderName = "Personal Collection Shelf Assets";
    private readonly SemaphoreSlim _inventoryLock = new(1, 1);
    private ConcurrentDictionary<string, string>? _fileIds;
    private string? _folderId;

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        await googleAccountService.GetDriveAccessTokenAsync(cancellationToken) is not null;

    public async Task UploadAsync(
        string objectName,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var token = await RequireTokenAsync(cancellationToken);
        await EnsureInventoryAsync(token, cancellationToken);
        if (_fileIds!.ContainsKey(objectName))
        {
            return;
        }

        var metadata = JsonSerializer.Serialize(new { name = objectName, parents = new[] { _folderId! } });
        var bytes = content.ToArray();
        using var response = await SendWithRetryAsync(() =>
        {
            var boundary = $"pcs-asset-{Guid.NewGuid():N}";
            var multipart = new MultipartContent("related", boundary)
            {
                new StringContent(metadata, Encoding.UTF8, "application/json"),
                new ByteArrayContent(bytes)
            };
            multipart.Last().Headers.ContentType = new MediaTypeHeaderValue(contentType);
            var request = CreateRequest(HttpMethod.Post, $"{UploadApi}/files?uploadType=multipart&fields=id", token);
            request.Content = multipart;
            return request;
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        _fileIds[objectName] = document.RootElement.GetProperty("id").GetString()!;
    }

    public async Task<byte[]> DownloadAsync(string objectName, CancellationToken cancellationToken = default)
    {
        var token = await RequireTokenAsync(cancellationToken);
        await EnsureInventoryAsync(token, cancellationToken);
        if (!_fileIds!.TryGetValue(objectName, out var fileId))
        {
            throw new FileNotFoundException("The cloud image was not found in Google Drive.", objectName);
        }

        using var request = CreateRequest(
            HttpMethod.Get,
            $"{DriveApi}/files/{Uri.EscapeDataString(fileId)}?alt=media",
            token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private async Task EnsureInventoryAsync(string token, CancellationToken cancellationToken)
    {
        if (_fileIds is not null)
        {
            return;
        }

        await _inventoryLock.WaitAsync(cancellationToken);
        try
        {
            if (_fileIds is not null)
            {
                return;
            }

            _folderId = await GetOrCreateFolderAsync(token, cancellationToken);
            var files = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
            string? pageToken = null;
            do
            {
                var query = $"'{_folderId}' in parents and trashed = false";
                var url = $"{DriveApi}/files?q={Uri.EscapeDataString(query)}&spaces=drive&fields=nextPageToken,files(id,name)&pageSize=1000";
                if (!string.IsNullOrWhiteSpace(pageToken))
                {
                    url += $"&pageToken={Uri.EscapeDataString(pageToken)}";
                }

                using var request = CreateRequest(HttpMethod.Get, url, token);
                using var response = await httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                foreach (var file in document.RootElement.GetProperty("files").EnumerateArray())
                {
                    var name = file.GetProperty("name").GetString();
                    var id = file.GetProperty("id").GetString();
                    if (name is not null && id is not null)
                    {
                        files[name] = id;
                    }
                }

                pageToken = document.RootElement.TryGetProperty("nextPageToken", out var next)
                    ? next.GetString()
                    : null;
            }
            while (!string.IsNullOrWhiteSpace(pageToken));

            _fileIds = files;
        }
        finally
        {
            _inventoryLock.Release();
        }
    }

    private async Task<string> GetOrCreateFolderAsync(string token, CancellationToken cancellationToken)
    {
        var query = $"name = '{FolderName}' and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
        var url = $"{DriveApi}/files?q={Uri.EscapeDataString(query)}&spaces=drive&fields=files(id)&pageSize=1";
        using (var request = CreateRequest(HttpMethod.Get, url, token))
        using (var response = await httpClient.SendAsync(request, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            foreach (var folder in document.RootElement.GetProperty("files").EnumerateArray())
            {
                return folder.GetProperty("id").GetString()!;
            }
        }

        using var createRequest = CreateRequest(HttpMethod.Post, $"{DriveApi}/files?fields=id", token);
        createRequest.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                name = FolderName,
                mimeType = "application/vnd.google-apps.folder"
            }),
            Encoding.UTF8,
            "application/json");
        using var createResponse = await httpClient.SendAsync(createRequest, cancellationToken);
        createResponse.EnsureSuccessStatusCode();
        using var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(cancellationToken));
        return created.RootElement.GetProperty("id").GetString()!;
    }

    private async Task<string> RequireTokenAsync(CancellationToken cancellationToken) =>
        await googleAccountService.GetDriveAccessTokenAsync(cancellationToken)
        ?? throw new InvalidOperationException("Google Drive authorization is required for image sync.");

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        const int maximumAttempts = 4;
        for (var attempt = 1; ; attempt++)
        {
            using var request = requestFactory();
            try
            {
                var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (attempt >= maximumAttempts || !IsTransient(response.StatusCode)) return response;
                response.Dispose();
            }
            catch (Exception exception) when (attempt < maximumAttempts && IsTransient(exception, cancellationToken))
            {
            }

            await Task.Delay(TimeSpan.FromMilliseconds(400 * Math.Pow(2, attempt - 1)), cancellationToken);
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private static bool IsTransient(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException or IOException or WebException ||
        exception is OperationCanceledException && !cancellationToken.IsCancellationRequested ||
        exception.InnerException is not null && IsTransient(exception.InnerException, cancellationToken);
}
