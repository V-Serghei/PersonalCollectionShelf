using Microsoft.Maui.Graphics;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.ViewModels;

public sealed record StatusSummaryViewModel(
    MediaStatus Status,
    string Label,
    int Count,
    Color Color);
