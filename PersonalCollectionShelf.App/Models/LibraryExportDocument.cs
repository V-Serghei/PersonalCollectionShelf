using PersonalCollectionShelf.Application.DTOs;

namespace PersonalCollectionShelf.App.Models;

public sealed record LibraryExportDocument
{
    public int Version { get; init; } = 2;

    public DateTime ExportedAtUtc { get; init; } = DateTime.UtcNow;

    public IReadOnlyList<MediaItemDto> Items { get; init; } = [];

    public IReadOnlyList<PersonExportDocument> People { get; init; } = [];
}

public sealed record PersonExportDocument
{
    public PersonDetailsDto Person { get; init; } = new();
    public IReadOnlyList<PortablePersonPhoto> Photos { get; init; } = [];
}

public sealed record PortablePersonPhoto
{
    public Guid Id { get; init; }
    public string? Caption { get; init; }
    public bool IsPrimary { get; init; }
    public int SortOrder { get; init; }
    public string Extension { get; init; } = ".jpg";
    public string DataBase64 { get; init; } = string.Empty;
}
