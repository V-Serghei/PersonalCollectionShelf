using System.Net;
using System.Text.Json;
using PersonalCollectionShelf.Infrastructure.Services.Firebase;

namespace PersonalCollectionShelf.Tests.Infrastructure;

public sealed class FirestoreRestClientTests
{
    [Fact]
    public async Task PutBatch_retries_transient_failure_and_sends_one_commit()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        var client = new FirestoreRestClient(
            httpClient,
            new FirebaseOptions { ApiKey = "test-key", ProjectId = "test-project" },
            new TestSessionProvider());
        var documents = new[]
        {
            new CloudDocument("media", "one", "{\"title\":\"One\"}", "hash-1", DateTime.UtcNow, false),
            new CloudDocument("media", "two", "{\"title\":\"Two\"}", "hash-2", DateTime.UtcNow, false)
        };

        await client.PutBatchAsync(documents);

        Assert.Equal(2, handler.Attempts);
        Assert.EndsWith("/documents:commit", handler.LastRequestUri, StringComparison.Ordinal);
        using var payload = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal(2, payload.RootElement.GetProperty("writes").GetArrayLength());
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int Attempts { get; private set; }
        public string? LastRequestUri { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Attempts++;
            LastRequestUri = request.RequestUri?.AbsoluteUri;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            if (Attempts == 1) throw new HttpRequestException("temporary disconnect");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
        }
    }

    private sealed class TestSessionProvider : IFirebaseSessionProvider
    {
        public Task<FirebaseSession?> GetSessionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<FirebaseSession?>(new FirebaseSession("test-user", "test-token", null));
    }
}
