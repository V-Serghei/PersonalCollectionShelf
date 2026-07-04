using PersonalCollectionShelf.Domain.Entities;

namespace PersonalCollectionShelf.Tests.Domain;

public sealed class MediaItemTests
{
    [Fact]
    public void MarkDeleted_sets_deleted_and_updated_timestamps()
    {
        var timestamp = new DateTime(2026, 7, 4, 10, 0, 0, DateTimeKind.Utc);
        var item = new MediaItem();

        item.MarkDeleted(timestamp);

        Assert.Equal(timestamp, item.DeletedAt);
        Assert.Equal(timestamp, item.UpdatedAt);
        Assert.True(item.IsDeleted);
    }
}
