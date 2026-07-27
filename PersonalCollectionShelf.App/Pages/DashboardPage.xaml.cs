using Microsoft.Extensions.DependencyInjection;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class DashboardPage : ContentPage
{
    private bool? _usesCompactLayout;

    public DashboardPage()
        : this(App.Services.GetRequiredService<LibraryViewModel>())
    {
    }

    public DashboardPage(LibraryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public LibraryViewModel ViewModel => (LibraryViewModel)BindingContext;

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width <= 0)
        {
            return;
        }

        var usesCompactLayout = width < 600;
        if (_usesCompactLayout == usesCompactLayout)
        {
            return;
        }

        _usesCompactLayout = usesCompactLayout;
        ApplyResponsiveLayout(usesCompactLayout, width);
    }

    private void ApplyResponsiveLayout(bool usesCompactLayout, double width)
    {
        TopBar.Padding = usesCompactLayout
            ? new Thickness(12, 7)
            : new Thickness(18, 7);
        DashboardContent.Padding = usesCompactLayout
            ? new Thickness(16, 20, 16, 30)
            : new Thickness(0, 24, 0, 30);
        DashboardContent.Spacing = usesCompactLayout ? 22 : 28;

        StatsGrid.ColumnDefinitions.Clear();
        StatsGrid.RowDefinitions.Clear();

        if (usesCompactLayout)
        {
            StatsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            StatsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            StatsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            StatsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            StatsGrid.RowSpacing = 10;

            PositionStatCard(TotalItemsCard, 0, 0);
            PositionStatCard(CompletedItemsCard, 1, 0);
            PositionStatCard(InProgressItemsCard, 0, 1);
            PositionStatCard(WishlistItemsCard, 1, 1);
        }
        else
        {
            for (var index = 0; index < 4; index++)
            {
                StatsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            }

            StatsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            StatsGrid.RowSpacing = 0;

            PositionStatCard(TotalItemsCard, 0, 0);
            PositionStatCard(CompletedItemsCard, 1, 0);
            PositionStatCard(InProgressItemsCard, 2, 0);
            PositionStatCard(WishlistItemsCard, 3, 0);
        }

        CategoryGridLayout.Span = usesCompactLayout ? 1 : width < 900 ? 2 : 3;
    }

    private static void PositionStatCard(View card, int column, int row)
    {
        Grid.SetColumn(card, column);
        Grid.SetRow(card, row);
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
                await CrashReporter.ReportAsync(exception, "DashboardPage.OnAppearing");
            }
        }
    }
}
