using PersonalCollectionShelf.Infrastructure.Services.Firebase;

namespace PersonalCollectionShelf.Tests.Infrastructure;

public sealed class FirebaseOptionsTests
{
    [Fact]
    public void Load_returns_disabled_options_when_config_file_is_missing()
    {
        var directory = CreateTempDirectory();

        try
        {
            var options = FirebaseOptions.Load(directory);

            Assert.False(options.IsConfigured);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_reads_api_key_and_project_id_from_config_file()
    {
        var directory = CreateTempDirectory();

        try
        {
            File.WriteAllText(
                Path.Combine(directory, "firebase.json"),
                """{ "apiKey": "test-key", "projectId": "test-project" }""");

            var options = FirebaseOptions.Load(directory);

            Assert.True(options.IsConfigured);
            Assert.Equal("test-key", options.ApiKey);
            Assert.Equal("test-project", options.ProjectId);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_returns_disabled_options_when_config_file_is_invalid_json()
    {
        var directory = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(directory, "firebase.json"), "not json");

            var options = FirebaseOptions.Load(directory);

            Assert.False(options.IsConfigured);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"pcs-firebase-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
