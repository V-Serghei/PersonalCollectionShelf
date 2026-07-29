using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PersonalCollectionShelf.Application.Interfaces;

namespace PersonalCollectionShelf.App.Services;

public sealed class GoogleDriveBackupService(
    HttpClient httpClient,
    IGoogleAccountService googleAccountService) : IGoogleDriveBackupService
{
    private const string DriveApi = "https://www.googleapis.com/drive/v3";
    private const string UploadApi = "https://www.googleapis.com/upload/drive/v3";
    private const string FolderName = "Personal Collection Shelf Backups";

    public async Task<string> UploadBackupAsync(
        string fileName,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        var token = await RequireTokenAsync(cancellationToken);
        var folderId = await GetOrCreateFolderAsync(token, cancellationToken);
        var boundary = $"pcs-{Guid.NewGuid():N}";
        var metadata = JsonSerializer.Serialize(new { name = fileName, parents = new[] { folderId } });
        using var requestContent = new MultipartContent("related", boundary)
        {
            new StringContent(metadata, Encoding.UTF8, "application/json"),
            new ByteArrayContent(content.ToArray())
        };
        requestContent.Last().Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        using var request = CreateRequest(HttpMethod.Post, $"{UploadApi}/files?uploadType=multipart&fields=id", token);
        request.Content = requestContent;
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var fileId = document.RootElement.GetProperty("id").GetString()!;
        await PruneOldBackupsAsync(folderId, token, cancellationToken);
        return fileId;
    }

    public async Task<DriveBackupFileDto?> DownloadLatestBackupAsync(CancellationToken cancellationToken = default)
    {
        var token = await RequireTokenAsync(cancellationToken);
        var folderId = await FindFolderAsync(token, cancellationToken);
        if (folderId is null)
        {
            return null;
        }

        var files = await ListBackupsAsync(folderId, token, cancellationToken);
        var latest = files.OrderByDescending(file => file.CreatedAtUtc).FirstOrDefault();
        if (latest is null)
        {
            return null;
        }

        using var request = CreateRequest(HttpMethod.Get, $"{DriveApi}/files/{Uri.EscapeDataString(latest.Id)}?alt=media", token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return new DriveBackupFileDto(
            latest.Name,
            await response.Content.ReadAsByteArrayAsync(cancellationToken),
            latest.CreatedAtUtc);
    }

    private async Task<string> GetOrCreateFolderAsync(string token, CancellationToken cancellationToken)
    {
        var existing = await FindFolderAsync(token, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var metadata = JsonSerializer.Serialize(new
        {
            name = FolderName,
            mimeType = "application/vnd.google-apps.folder"
        });
        using var request = CreateRequest(HttpMethod.Post, $"{DriveApi}/files?fields=id", token);
        request.Content = new StringContent(metadata, Encoding.UTF8, "application/json");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.GetProperty("id").GetString()!;
    }

    private async Task<string?> FindFolderAsync(string token, CancellationToken cancellationToken)
    {
        var escapedName = FolderName.Replace("'", "\\'");
        var query = $"name = '{escapedName}' and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
        var url = $"{DriveApi}/files?q={Uri.EscapeDataString(query)}&spaces=drive&fields=files(id)&pageSize=1";
        using var request = CreateRequest(HttpMethod.Get, url, token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        foreach (var file in document.RootElement.GetProperty("files").EnumerateArray())
        {
            if (file.TryGetProperty("id", out var id))
            {
                return id.GetString();
            }
        }

        return null;
    }

    private async Task<IReadOnlyList<DriveFile>> ListBackupsAsync(
        string folderId,
        string token,
        CancellationToken cancellationToken)
    {
        var query = $"'{folderId}' in parents and trashed = false";
        var url = $"{DriveApi}/files?q={Uri.EscapeDataString(query)}&spaces=drive&fields=files(id,name,createdTime)&pageSize=1000&orderBy=createdTime%20desc";
        using var request = CreateRequest(HttpMethod.Get, url, token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.GetProperty("files").EnumerateArray()
            .Select(file => new DriveFile(
                file.GetProperty("id").GetString()!,
                file.GetProperty("name").GetString()!,
                DateTime.Parse(file.GetProperty("createdTime").GetString()!, null, System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime()))
            .ToList();
    }

    private async Task PruneOldBackupsAsync(string folderId, string token, CancellationToken cancellationToken)
    {
        var files = (await ListBackupsAsync(folderId, token, cancellationToken))
            .OrderByDescending(file => file.CreatedAtUtc)
            .ToList();
        var keep = new HashSet<string>(files.Take(30).Select(file => file.Id));
        foreach (var file in files.GroupBy(file => (file.CreatedAtUtc.Year, file.CreatedAtUtc.Month)).Select(group => group.First()).Take(12))
        {
            keep.Add(file.Id);
        }
        foreach (var file in files.GroupBy(file => file.CreatedAtUtc.Year).Select(group => group.First()))
        {
            keep.Add(file.Id);
        }

        foreach (var file in files.Where(file => !keep.Contains(file.Id)))
        {
            using var request = CreateRequest(HttpMethod.Delete, $"{DriveApi}/files/{Uri.EscapeDataString(file.Id)}", token);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
    }

    private async Task<string> RequireTokenAsync(CancellationToken cancellationToken) =>
        await googleAccountService.GetDriveAccessTokenAsync(cancellationToken)
        ?? throw new InvalidOperationException("Google Drive authorization is required.");

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private sealed record DriveFile(string Id, string Name, DateTime CreatedAtUtc);
}
