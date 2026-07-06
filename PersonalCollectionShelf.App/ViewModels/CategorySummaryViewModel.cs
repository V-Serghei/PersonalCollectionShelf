using Microsoft.Maui.Graphics;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.ViewModels;

public sealed record CategorySummaryViewModel(
    MediaType MediaType,
    string Label,
    int Count,
    int CompletedCount,
    Color Color)
{
    public string CountText => $"{Count} items - {CompletedCount} done";
}
