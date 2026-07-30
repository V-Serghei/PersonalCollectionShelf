using SQLite;

namespace PersonalCollectionShelf.Infrastructure.Persistence;

[Table("PersonPhotos")]
public sealed class PersonPhotoRecord
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string UserId { get; set; } = string.Empty;

    [Indexed]
    public string PersonId { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}
