using SQLite;

namespace PersonalCollectionShelf.Infrastructure.Persistence;

[Table("MediaItemCastMembers")]
public sealed class MediaItemCastMemberRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string MediaItemId { get; set; } = string.Empty;

    [Indexed]
    public string PersonId { get; set; } = string.Empty;
}
