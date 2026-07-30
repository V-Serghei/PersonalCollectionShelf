using SQLite;

namespace PersonalCollectionShelf.Infrastructure.Persistence;

[Table("Studios")]
public sealed class StudioRecord
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string UserId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
