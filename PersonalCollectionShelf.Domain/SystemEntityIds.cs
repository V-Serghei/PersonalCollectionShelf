using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Domain;

public static class SystemEntityIds
{
    public static Guid MediaCategory(MediaType mediaType) =>
        Guid.Parse($"00000000-0000-0000-0000-{(int)mediaType:D12}");

    public static Guid CreditRole(ContributionRole role) =>
        Guid.Parse($"00000000-0000-0001-0000-{(int)role:D12}");
}
