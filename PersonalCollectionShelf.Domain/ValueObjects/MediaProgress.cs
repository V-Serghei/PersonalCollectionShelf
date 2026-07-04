using System.Globalization;

namespace PersonalCollectionShelf.Domain.ValueObjects;

public readonly record struct MediaProgress(int Current, int? Total)
{
    public bool IsComplete => Total is > 0 && Current >= Total.Value;

    public override string ToString()
    {
        return Total is > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{Current}/{Total.Value}")
            : Current.ToString(CultureInfo.InvariantCulture);
    }
}
