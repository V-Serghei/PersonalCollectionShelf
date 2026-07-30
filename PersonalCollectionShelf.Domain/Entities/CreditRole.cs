namespace PersonalCollectionShelf.Domain.Entities;

public sealed class CreditRole
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool IsSystem { get; set; }
}
