using Microsoft.Maui.Graphics;

namespace PersonalCollectionShelf.App.ViewModels;

public sealed record StatisticPoint(string Label, double Value, Color Color, string Detail = "");

public sealed record ChartLegendItem(string Label, string Value, Color Color);

public sealed record StatisticYearRange(int StartYear, int EndYear);
