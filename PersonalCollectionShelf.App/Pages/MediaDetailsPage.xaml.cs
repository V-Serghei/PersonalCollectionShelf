using Microsoft.Extensions.DependencyInjection;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class MediaDetailsPage : ContentPage, IQueryAttributable
{
    private Guid? _mediaItemId;
    private bool? _usesCompactLayout;

    public MediaDetailsPage()
        : this(App.Services.GetRequiredService<MediaDetailsViewModel>())
    {
    }

    public MediaDetailsPage(MediaDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        try
        {
            if (query.TryGetValue("id", out var value) && Guid.TryParse(value?.ToString(), out var mediaItemId))
            {
                _mediaItemId = mediaItemId;
                await ViewModel.LoadAsync(mediaItemId);
            }
        }
        catch (Exception exception)
        {
            await CrashReporter.ReportAsync(exception, "MediaDetailsPage.ApplyQueryAttributes");
        }
    }

    public void SetNavigationTarget(Guid mediaItemId)
    {
        _mediaItemId = mediaItemId;
    }

    public Task LoadNavigationTargetAsync() =>
        _mediaItemId.HasValue ? ViewModel.LoadAsync(_mediaItemId.Value) : Task.CompletedTask;

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_mediaItemId.HasValue)
        {
            try
            {
                await ViewModel.ReloadAsync();
            }
            catch (Exception exception)
            {
                await CrashReporter.ReportAsync(exception, "MediaDetailsPage.OnAppearing");
            }
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
        DetailsContent.Padding = usesCompactLayout ? new Thickness(14) : new Thickness(24);
        HeroCard.Padding = usesCompactLayout ? new Thickness(16) : new Thickness(22);
        ConfigureStackingGrid(HeroGrid, HeroCover, HeroInfo, usesCompactLayout, 16, 26);
        HeroCover.HorizontalOptions = usesCompactLayout ? LayoutOptions.Center : LayoutOptions.Fill;
        ConfigureMetricGrid(usesCompactLayout);
        ConfigureStackingGrid(
            DetailsColumnsGrid,
            DetailsLeftColumn,
            DetailsRightColumn,
            usesCompactLayout,
            18,
            18);
    }

    private void ConfigureMetricGrid(bool usesCompactLayout)
    {
        var metrics = new View[] { RatingMetric, YearMetric };
        MetricsGrid.ColumnDefinitions.Clear();
        MetricsGrid.RowDefinitions.Clear();
        MetricsGrid.RowSpacing = usesCompactLayout ? 12 : 0;

        if (usesCompactLayout)
        {
            MetricsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            MetricsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            MetricsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var index = 0; index < metrics.Length; index++)
            {
                Grid.SetColumn(metrics[index], index % 2);
                Grid.SetRow(metrics[index], 0);
            }
        }
        else
        {
            for (var index = 0; index < metrics.Length; index++)
            {
                MetricsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                Grid.SetColumn(metrics[index], index);
                Grid.SetRow(metrics[index], 0);
            }
            MetricsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
    }

    private static void ConfigureStackingGrid(
        Grid grid,
        View first,
        View second,
        bool usesCompactLayout,
        double compactSpacing,
        double wideSpacing)
    {
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        grid.ColumnSpacing = usesCompactLayout ? 0 : wideSpacing;
        grid.RowSpacing = usesCompactLayout ? compactSpacing : 0;
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        if (usesCompactLayout)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(first, 0);
            Grid.SetRow(first, 0);
            Grid.SetColumn(second, 0);
            Grid.SetRow(second, 1);
        }
        else
        {
            grid.ColumnDefinitions.Insert(0, new ColumnDefinition(GridLength.Star));
            Grid.SetColumn(first, 0);
            Grid.SetRow(first, 0);
            Grid.SetColumn(second, 1);
            Grid.SetRow(second, 0);
        }
    }

    public MediaDetailsViewModel ViewModel => (MediaDetailsViewModel)BindingContext;
}
