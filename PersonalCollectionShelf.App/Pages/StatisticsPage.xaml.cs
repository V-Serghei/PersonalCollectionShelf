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
        ReleaseYearChart.Drawable = new VerticalPointBarDrawable(viewModel.ReleaseYearStatistics);
        AddedYearChart.Drawable = new PointTrendDrawable(viewModel.AddedYearStatistics);
        DecadeChart.Drawable = new PointDonutDrawable(viewModel.DecadeStatistics);
        GenreChart.Drawable = new HorizontalPointBarDrawable(viewModel.GenreStatistics);
        CountryChart.Drawable = new HorizontalPointBarDrawable(viewModel.CountryStatistics);
        RatingChart.Drawable = new VerticalPointBarDrawable(viewModel.RatingStatistics);
        AverageByTypeChart.Drawable = new HorizontalPointBarDrawable(viewModel.AverageRatingByTypeStatistics, "0.0");
        CompletionByTypeChart.Drawable = new HorizontalPointBarDrawable(viewModel.CompletionByTypeStatistics, "0'%'", 100);
        FavoritesByTypeChart.Drawable = new HorizontalPointBarDrawable(viewModel.FavoritesByTypeStatistics);
        SelectTab("overview");
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
            ReleaseYearChart.Invalidate();
            AddedYearChart.Invalidate();
            DecadeChart.Invalidate();
            GenreChart.Invalidate();
            CountryChart.Invalidate();
            RatingChart.Invalidate();
            AverageByTypeChart.Invalidate();
            CompletionByTypeChart.Invalidate();
            FavoritesByTypeChart.Invalidate();
        }
    }

    private void HandleStatisticsTabClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string tab }) SelectTab(tab);
    }

    private void SelectTab(string tab)
    {
        OverviewSection.IsVisible = tab == "overview";
        TimeSection.IsVisible = tab == "time";
        GenresSection.IsVisible = tab == "genres";
        RatingsSection.IsVisible = tab == "ratings";
        foreach (var (button, key) in new[]
                 {
                     (OverviewTabButton, "overview"), (TimeTabButton, "time"),
                     (GenresTabButton, "genres"), (RatingsTabButton, "ratings")
                 })
        {
            button.BackgroundColor = key == tab ? Color.FromArgb("#9D7FF4") : Colors.Transparent;
            button.TextColor = key == tab ? Colors.White : Color.FromArgb("#9D7FF4");
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
        TopBar.Padding = usesCompactLayout ? new Thickness(12, 7) : new Thickness(18, 7);
        StatisticsContent.Padding = usesCompactLayout
            ? new Thickness(12, 8, 12, 30)
            : new Thickness(0, 24, 0, 30);
        StatisticsContent.Spacing = usesCompactLayout ? 14 : 20;
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
            canvas.DrawString(viewModel.StatisticsItemsLabel, dirtyRect.Center.X - 50, dirtyRect.Center.Y + 8, 100, 14, HorizontalAlignment.Center, VerticalAlignment.Center);

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

    private sealed class HorizontalPointBarDrawable(
        IReadOnlyList<StatisticPoint> points,
        string valueFormat = "0",
        double? fixedMaximum = null) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var values = points.Where(point => point.Value > 0).Take(18).ToList();
            if (values.Count == 0) return;
            var max = fixedMaximum ?? Math.Max(1, values.Max(point => point.Value));
            var labelWidth = Math.Min(150f, dirtyRect.Width * .34f);
            var rowHeight = dirtyRect.Height / values.Count;
            for (var index = 0; index < values.Count; index++)
            {
                var point = values[index];
                var y = index * rowHeight + 3;
                var height = Math.Max(8, rowHeight - 9);
                var available = dirtyRect.Width - labelWidth - 48;
                var width = Math.Max(3, (float)(point.Value / max * available));
                canvas.FontSize = 10;
                canvas.FontColor = Color.FromArgb("#C6BEE0");
                canvas.DrawString(point.Label, 0, y, labelWidth - 8, height, HorizontalAlignment.Right, VerticalAlignment.Center);
                canvas.FillColor = Color.FromArgb("#241E38");
                canvas.FillRoundedRectangle(labelWidth, y, available, height, height / 2);
                canvas.FillColor = point.Color;
                canvas.FillRoundedRectangle(labelWidth, y, width, height, height / 2);
                canvas.FontColor = Color.FromArgb("#EDE9F8");
                canvas.DrawString(point.Value.ToString(valueFormat, CultureInfo.InvariantCulture), labelWidth + available + 6, y, 42, height, HorizontalAlignment.Left, VerticalAlignment.Center);
            }
        }
    }

    private sealed class VerticalPointBarDrawable(IReadOnlyList<StatisticPoint> points) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var values = points.Where(point => point.Value >= 0).ToList();
            if (values.Count == 0) return;
            var max = Math.Max(1, values.Max(point => point.Value));
            var bottom = dirtyRect.Height - 30;
            var availableHeight = dirtyRect.Height - 54;
            var slot = dirtyRect.Width / values.Count;
            var barWidth = Math.Max(3, Math.Min(32, slot * .62f));
            for (var index = 0; index < values.Count; index++)
            {
                var point = values[index];
                var height = Math.Max(2, point.Value / max * availableHeight);
                var x = index * slot + (slot - barWidth) / 2;
                canvas.FillColor = point.Color;
                canvas.FillRoundedRectangle((float)x, (float)(bottom - height), (float)barWidth, (float)height, 5);
                canvas.FontSize = values.Count > 14 ? 7 : 9;
                canvas.FontColor = Color.FromArgb("#8179A3");
                canvas.DrawString(point.Label, (float)(index * slot), bottom + 5, (float)slot, 16, HorizontalAlignment.Center, VerticalAlignment.Top);
            }
        }
    }

    private sealed class PointTrendDrawable(IReadOnlyList<StatisticPoint> points) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var values = points.ToList();
            if (values.Count == 0) return;
            var max = Math.Max(1, values.Max(point => point.Value));
            var left = 18f;
            var width = dirtyRect.Width - 36;
            var height = dirtyRect.Height - 48;
            var path = new PathF();
            for (var index = 0; index < values.Count; index++)
            {
                var x = values.Count == 1 ? left + width / 2 : left + width * index / (values.Count - 1);
                var y = 12 + height - values[index].Value / max * height;
                if (index == 0) path.MoveTo(x, (float)y); else path.LineTo(x, (float)y);
                canvas.FillColor = values[index].Color;
                canvas.FillCircle(x, (float)y, 4);
                canvas.FontSize = 9;
                canvas.FontColor = Color.FromArgb("#8179A3");
                canvas.DrawString(values[index].Label, x - 25, dirtyRect.Height - 24, 50, 14, HorizontalAlignment.Center, VerticalAlignment.Top);
            }
            canvas.StrokeColor = Color.FromArgb("#9D7FF4");
            canvas.StrokeSize = 3;
            canvas.DrawPath(path);
        }
    }

    private sealed class PointDonutDrawable(IReadOnlyList<StatisticPoint> points) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var values = points.Where(point => point.Value > 0).ToList();
            var total = values.Sum(point => point.Value);
            if (total <= 0) return;
            var size = Math.Min(dirtyRect.Width, dirtyRect.Height) - 36;
            var rect = new RectF((dirtyRect.Width - size) / 2, (dirtyRect.Height - size) / 2, size, size);
            var start = -90f;
            foreach (var point in values)
            {
                var sweep = (float)(point.Value / total * 360);
                canvas.StrokeColor = point.Color;
                canvas.StrokeSize = 30;
                canvas.DrawArc(rect, start, start + Math.Max(0, sweep - 2), false, false);
                start += sweep;
            }
            canvas.FontColor = Color.FromArgb("#EDE9F8");
            canvas.FontSize = 22;
            canvas.DrawString(total.ToString("0", CultureInfo.InvariantCulture), dirtyRect.Center.X - 60, dirtyRect.Center.Y - 12, 120, 24, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}
