namespace PersonalCollectionShelf.App.Models;

public sealed record LocalizedOption<T>(T Value, string DisplayName)
{
    public override string ToString()
    {
        return DisplayName;
    }
}
