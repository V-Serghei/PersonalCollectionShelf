using PersonalCollectionShelf.Application.DTOs;

namespace PersonalCollectionShelf.App.Models;

public sealed record LibraryExportDocument
{
    public int Version { get; init; } = 1;

    public DateTime ExportedAtUtc { get; init; } = DateTime.UtcNow;

    public IReadOnlyList<MediaItemDto> Items { get; init; } = [];
}
