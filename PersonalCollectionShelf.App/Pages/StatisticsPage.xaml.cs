using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Graphics;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class StatisticsPage : ContentPage
{
    public StatisticsPage()
        : this(App.Services.GetRequiredService<LibraryViewModel>())
    {
    }

    public StatisticsPage(LibraryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        MonthlyChart.Drawable = new MonthlyActivityDrawable();
        DonutChart.Drawable = new CategoryDonutDrawable();
        BarChart.Drawable = new StatusBarDrawable();
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
        }
    }

    private sealed class MonthlyActivityDrawable : IDrawable
    {
        private static readonly float[] Values = [3, 5, 4, 7, 6, 8, 5, 9, 7];
        private static readonly string[] Months = ["Aug", "Sep", "Oct", "Nov", "Dec", "Jan", "Feb", "Mar", "Apr"];

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.SaveState();
            canvas.FontSize = 11;
            canvas.FontColor = Color.FromArgb("#9D7FF4");
            canvas.StrokeColor = Color.FromArgb("#342B4F");
            canvas.StrokeSize = 1;

            var left = 46f;
            var top = 18f;
            var width = dirtyRect.Width - 70f;
            var height = dirtyRect.Height - 46f;

            for (var i = 0; i <= 4; i++)
            {
                var y = top + height * i / 4f;
                canvas.DrawLine(left, y, left + width, y);
            }

            var path = new PathF();
            for (var i = 0; i < Values.Length; i++)
            {
                var x = left + width * i / (Values.Length - 1);
                var y = top + height - (Values[i] / 12f * height);
                if (i == 0)
                {
                    path.MoveTo(x, y);
                }
                else
                {
                    path.LineTo(x, y);
                }

                canvas.FillColor = Color.FromArgb("#9D7FF4");
                canvas.FillCircle(x, y, 3);
                canvas.DrawString(Months[i], x - 14, top + height + 12, 34, 14, HorizontalAlignment.Center, VerticalAlignment.Top);
            }

            canvas.StrokeColor = Color.FromArgb("#9D7FF4");
            canvas.StrokeSize = 2;
            canvas.DrawPath(path);
            canvas.RestoreState();
        }
    }

    private sealed class CategoryDonutDrawable : IDrawable
    {
        private static readonly Color[] Colors =
        [
            Color.FromArgb("#E07C54"),
            Color.FromArgb("#7CCC8A"),
            Color.FromArgb("#C47CF0"),
            Color.FromArgb("#F07CB8"),
            Color.FromArgb("#F0C040")
        ];

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var size = Math.Min(dirtyRect.Width, dirtyRect.Height) - 30;
            var rect = new RectF((dirtyRect.Width - size) / 2, (dirtyRect.Height - size) / 2, size, size);
            var start = -90f;

            canvas.StrokeSize = 24;
            foreach (var color in Colors)
            {
                canvas.StrokeColor = color;
                canvas.DrawArc(rect, start, start + 62, false, false);
                start += 72;
            }

            canvas.FillColor = Color.FromArgb("#1A1726");
            canvas.FillCircle(dirtyRect.Center.X, dirtyRect.Center.Y, size * 0.25f);
        }
    }

    private sealed class StatusBarDrawable : IDrawable
    {
        private static readonly (float Value, Color Color, string Label)[] Bars =
        [
            (8, Color.FromArgb("#4ADE80"), "Done"),
            (4, Color.FromArgb("#60A5FA"), "Active"),
            (2, Color.FromArgb("#FBBF24"), "Wishlist"),
            (0.3f, Color.FromArgb("#F87171"), "Dropped")
        ];

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var left = 34f;
            var bottom = dirtyRect.Height - 26f;
            var maxHeight = dirtyRect.Height - 54f;
            var barWidth = 42f;
            var gap = 22f;

            canvas.FontSize = 10;
            canvas.FontColor = Color.FromArgb("#9D7FF4");

            for (var i = 0; i < Bars.Length; i++)
            {
                var (value, color, label) = Bars[i];
                var x = left + i * (barWidth + gap);
                var height = value / 8f * maxHeight;
                canvas.FillColor = color;
                canvas.FillRoundedRectangle(x, bottom - height, barWidth, height, 5);
                canvas.DrawString(label, x - 6, bottom + 6, barWidth + 12, 14, HorizontalAlignment.Center, VerticalAlignment.Top);
            }
        }
    }
}
