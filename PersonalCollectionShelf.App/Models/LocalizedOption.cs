namespace PersonalCollectionShelf.App.Models;

public abstract record DisplayOption(string DisplayName)
{
    public override string ToString() => DisplayName;
}

public sealed record LocalizedOption<T>(T Value, string DisplayName) : DisplayOption(DisplayName)
{
    public override string ToString() => DisplayName;
}
