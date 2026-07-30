namespace PersonalCollectionShelf.Domain.Entities;

public sealed class PersonProfession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;

    public Guid PersonId { get; set; }

    public Guid ProfessionId { get; set; }

    public string? Notes { get; set; }
}
