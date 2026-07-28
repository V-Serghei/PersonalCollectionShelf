using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Graphics;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class StatisticsPage : ContentPage
{
    private bool? _usesCompactLayout;

    public StatisticsPage()
        : this(App.Services.GetRequiredService<LibraryViewModel>())
    {
    }

    public StatisticsPage(LibraryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        MonthlyChart.Drawable = new MonthlyActivityDrawable(viewModel);
        DonutChart.Drawable = new CategoryDonutDrawable(viewModel);
        BarChart.Drawable = new StatusBarDrawable(viewModel);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is LibraryViewModel viewModel)
        {
            try
            {
                await viewModel.LoadAsync();
            }
            catch (Exception exception)
            {
                await CrashReporter.ReportAsync(exception, "StatisticsPage.OnAppearing");
            }

            MonthlyChart.Invalidate();
            DonutChart.Invalidate();
            BarChart.Invalidate();
        }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width <= 0)
        {
            return;
        }

        var usesCompactLayout = DeviceInfo.Current.Idiom == DeviceIdiom.Phone || width < 700;
        if (_usesCompactLayout == usesCompactLayout)
        {
            return;
        }

        _usesCompactLayout = usesCompactLayout;
        TopBar.ColumnDefinitions.Clear();
        TopBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        TopBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        TopBar.Padding = usesCompactLayout ? new Thickness(12, 7) : new Thickness(18, 7);
        StatisticsContent.Padding = usesCompactLayout
            ? new Thickness(14, 18, 14, 30)
            : new Thickness(0, 24, 0, 30);
        StatisticsContent.Spacing = usesCompactLayout ? 18 : 28;

        ConfigureStackingGrid(SummaryGrid, [RatingCard, CompletionCard, CollectionCard], usesCompactLayout);
        ConfigureStackingGrid(BreakdownGrid, [CategoryChartCard, StatusChartCard], usesCompactLayout);
    }

    private static void ConfigureStackingGrid(Grid grid, IReadOnlyList<View> children, bool usesCompactLayout)
    {
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        grid.ColumnSpacing = usesCompactLayout ? 0 : 14;
        grid.RowSpacing = usesCompactLayout ? 12 : 0;

        if (usesCompactLayout)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            for (var index = 0; index < children.Count; index++)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Grid.SetColumn(children[index], 0);
                Grid.SetRow(children[index], index);
            }
        }
        else
        {
            for (var index = 0; index < children.Count; index++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                Grid.SetColumn(children[index], index);
                Grid.SetRow(children[index], 0);
            }
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
    }

    private sealed class MonthlyActivityDrawable(LibraryViewModel viewModel) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var points = viewModel.MonthlyActivity.ToList();
            canvas.SaveState();

            var left = 26f;
            var top = 14f;
            var width = dirtyRect.Width - left - 10f;
            var height = dirtyRect.Height - 46f;
            var maxCount = points.Count == 0 ? 0 : points.Max(point => point.Count);
            var gridMax = (float)Math.Max(4, Math.Ceiling(Math.Max(1, maxCount) / 4.0) * 4);

            canvas.FontSize = 10;
            canvas.StrokeColor = Color.FromArgb("#241E38");
            canvas.StrokeSize = 1;
            for (var i = 0; i <= 4; i++)
            {
                var y = top + height * i / 4f;
                canvas.DrawLine(left, y, left + width, y);
                canvas.FontColor = Color.FromArgb("#6F6098");
                var label = (gridMax - gridMax * i / 4f).ToString("0", CultureInfo.InvariantCulture);
                canvas.DrawString(label, 0, y - 6, left - 8, 12, HorizontalAlignment.Right, VerticalAlignment.Center);
            }

            if (points.Count == 0)
            {
                canvas.RestoreState();
                return;
            }

            PointF Project(int index) => new(
                points.Count == 1 ? left + width / 2f : left + (width * index / (points.Count - 1)),
                top + height - points[index].Count / gridMax * height);

            var linePath = BuildSmoothPath(points.Count, Project);
            var areaPath = BuildSmoothPath(points.Count, Project);
            areaPath.LineTo(Project(points.Count - 1).X, top + height);
            areaPath.LineTo(Project(0).X, top + height);
            areaPath.Close();

            canvas.SetFillPaint(
                new LinearGradientPaint
                {
                    GradientStops =
                    [
                        new PaintGradientStop(0, Color.FromArgb("#409D7FF4")),
                        new PaintGradientStop(1, Color.FromArgb("#009D7FF4"))
                    ],
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1)
                },
                new RectF(left, top, width, height));
            canvas.FillPath(areaPath);

            canvas.StrokeColor = Color.FromArgb("#9D7FF4");
            canvas.StrokeSize = 2.5f;
            canvas.StrokeLineJoin = LineJoin.Round;
            canvas.DrawPath(linePath);

            for (var i = 0; i < points.Count; i++)
            {
                var point = Project(i);
                canvas.FillColor = Color.FromArgb("#272238");
                canvas.FillCircle(point.X, point.Y, 4);
                canvas.StrokeColor = Color.FromArgb("#9D7FF4");
                canvas.StrokeSize = 2;
                canvas.DrawCircle(point.X, point.Y, 4);

                canvas.FontColor = Color.FromArgb("#9D7FF4");
                canvas.DrawString(points[i].MonthLabel, point.X - 17, top + height + 10, 34, 14, HorizontalAlignment.Center, VerticalAlignment.Top);
            }

            canvas.RestoreState();
        }

        private static PathF BuildSmoothPath(int count, Func<int, PointF> project)
        {
            var path = new PathF();
            if (count == 0)
            {
                return path;
            }

            var points = Enumerable.Range(0, count).Select(project).ToArray();
            path.MoveTo(points[0].X, points[0].Y);
            if (count == 1)
            {
                return path;
            }

            for (var i = 0; i < count - 1; i++)
            {
                var previous = points[Math.Max(0, i - 1)];
                var current = points[i];
                var next = points[i + 1];
                var afterNext = points[Math.Min(count - 1, i + 2)];

                var control1 = new PointF(current.X + (next.X - previous.X) / 6f, current.Y + (next.Y - previous.Y) / 6f);
                var control2 = new PointF(next.X - (afterNext.X - current.X) / 6f, next.Y - (afterNext.Y - current.Y) / 6f);
                path.CurveTo(control1, control2, next);
            }

            return path;
        }
    }

    private sealed class CategoryDonutDrawable(LibraryViewModel viewModel) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var summaries = viewModel.CategorySummaries.Where(summary => summary.Count > 0).ToList();
            var size = Math.Min(dirtyRect.Width, dirtyRect.Height) - 30;
            var rect = new RectF((dirtyRect.Width - size) / 2, (dirtyRect.Height - size) / 2, size, size);
            var total = summaries.Sum(summary => summary.Count);

            canvas.SaveState();
            canvas.StrokeLineCap = LineCap.Butt;

            if (total == 0)
            {
                canvas.StrokeColor = Color.FromArgb("#2B2440");
                canvas.StrokeSize = 24;
                canvas.DrawEllipse(rect);
            }
            else
            {
                var gapDegrees = summaries.Count > 1 ? 3f : 0f;
                var start = -90f;
                foreach (var summary in summaries)
                {
                    var sweep = summary.Count / (float)total * 360f;
                    var drawnSweep = Math.Max(0f, sweep - gapDegrees);
                    canvas.StrokeColor = summary.Color;
                    canvas.StrokeSize = 24;
                    canvas.DrawArc(rect, start, start + drawnSweep, false, false);
                    start += sweep;
                }
            }

            canvas.FillColor = Color.FromArgb("#1A1726");
            canvas.FillCircle(dirtyRect.Center.X, dirtyRect.Center.Y, size * 0.28f);

            canvas.FontSize = 22;
            canvas.FontColor = Color.FromArgb("#EDE9F8");
            canvas.DrawString(total.ToString(CultureInfo.InvariantCulture), dirtyRect.Center.X - 50, dirtyRect.Center.Y - 16, 100, 22, HorizontalAlignment.Center, VerticalAlignment.Center);
            canvas.FontSize = 10;
            canvas.FontColor = Color.FromArgb("#8179A3");
            canvas.DrawString("items", dirtyRect.Center.X - 50, dirtyRect.Center.Y + 8, 100, 14, HorizontalAlignment.Center, VerticalAlignment.Center);

            canvas.RestoreState();
        }
    }

    private sealed class StatusBarDrawable(LibraryViewModel viewModel) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var summaries = viewModel.StatusSummaries.Where(summary => summary.Count > 0).ToList();
            canvas.SaveState();

            if (summaries.Count == 0)
            {
                canvas.RestoreState();
                return;
            }

            var maxValue = Math.Max(1, summaries.Max(summary => summary.Count));
            var bottom = dirtyRect.Height - 26f;
            var maxHeight = dirtyRect.Height - 58f;
            var gap = 18f;
            var barWidth = Math.Min(42f, (dirtyRect.Width - gap * (summaries.Count + 1)) / summaries.Count);
            var totalWidth = summaries.Count * barWidth + (summaries.Count - 1) * gap;
            var left = (dirtyRect.Width - totalWidth) / 2f;

            canvas.FontSize = 10;
            for (var i = 0; i < summaries.Count; i++)
            {
                var summary = summaries[i];
                var x = left + i * (barWidth + gap);
                var barHeight = Math.Max(3f, summary.Count / (float)maxValue * maxHeight);

                canvas.FillColor = summary.Color;
                canvas.FillRoundedRectangle(x, bottom - barHeight, barWidth, barHeight, 6);

                canvas.FontColor = Color.FromArgb("#EDE9F8");
                canvas.DrawString(summary.Count.ToString(CultureInfo.InvariantCulture), x - 6, bottom - barHeight - 18, barWidth + 12, 14, HorizontalAlignment.Center, VerticalAlignment.Bottom);

                canvas.FontColor = Color.FromArgb("#9D7FF4");
                canvas.DrawString(summary.Label, x - 10, bottom + 6, barWidth + 20, 14, HorizontalAlignment.Center, VerticalAlignment.Top);
            }

            canvas.RestoreState();
        }
    }
}
