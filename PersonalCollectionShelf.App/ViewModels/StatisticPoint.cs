using Microsoft.Maui.Graphics;

namespace PersonalCollectionShelf.App.ViewModels;

public sealed record StatisticPoint(string Label, double Value, Color Color, string Detail = "");
