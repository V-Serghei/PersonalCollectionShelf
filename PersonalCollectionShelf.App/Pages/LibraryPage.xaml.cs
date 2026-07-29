using Microsoft.Extensions.DependencyInjection;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class LibraryPage : ContentPage
{
    private bool? _usesCompactLayout;

    public LibraryPage()
        : this(App.Services.GetRequiredService<LibraryViewModel>())
    {
    }

    public LibraryPage(LibraryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.PropertyChanged += HandleViewModelPropertyChanged;
        AttachFreeScrolling(GridLibraryList);
        AttachFreeScrolling(ListLibraryList);
    }

    public LibraryViewModel ViewModel => (LibraryViewModel)BindingContext;

    private static void AttachFreeScrolling(CollectionView collectionView)
    {
        collectionView.HandlerChanged += (_, _) => CollectionViewScrollTuner.EnableFreeScrolling(collectionView);
        collectionView.Loaded += (_, _) => CollectionViewScrollTuner.EnableFreeScrolling(collectionView);
    }

    private async void HandleSearchClicked(object? sender, EventArgs e)
    {
        ViewModel.ToggleSearchCommand.Execute(null);
        if (ViewModel.IsSearchVisible) { await Task.Delay(50); TopSearchEntry.Focus(); }
    }

    private void HandleViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LibraryViewModel.SelectedMediaTypeFilter) || ViewModel.SelectedMediaTypeFilter is null) return;
        Dispatcher.Dispatch(() => MediaTypeFilterList.ScrollTo(ViewModel.SelectedMediaTypeFilter, position: ScrollToPosition.Center, animate: true));
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
        ApplyResponsiveLayout(usesCompactLayout);
    }

    private void ApplyResponsiveLayout(bool usesCompactLayout)
    {
        TopBar.ColumnDefinitions.Clear();
        TopBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        TopBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        TopBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        TopBar.Padding = usesCompactLayout ? new Thickness(12, 7) : new Thickness(18, 7);

        LibraryContent.Padding = usesCompactLayout
            ? new Thickness(14, 18, 14, 0)
            : new Thickness(0, 24, 0, 0);

        FilterGrid.ColumnDefinitions.Clear();
        FilterGrid.RowDefinitions.Clear();
        if (usesCompactLayout)
        {
            FilterGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            FilterGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            FilterGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            FilterGrid.RowSpacing = 8;
            Grid.SetColumn(LibrarySearch, 0);
            Grid.SetRow(LibrarySearch, 0);
            Grid.SetColumn(StatusPicker, 0);
            Grid.SetRow(StatusPicker, 1);
            Grid.SetColumn(SortPicker, 0);
            Grid.SetRow(SortPicker, 1);
            StatusPicker.HorizontalOptions = LayoutOptions.Start;
            SortPicker.HorizontalOptions = LayoutOptions.End;
            StatusPicker.WidthRequest = 145;
            SortPicker.WidthRequest = 175;
        }
        else
        {
            FilterGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            FilterGrid.ColumnDefinitions.Add(new ColumnDefinition(130));
            FilterGrid.ColumnDefinitions.Add(new ColumnDefinition(170));
            FilterGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            FilterGrid.RowSpacing = 0;
            Grid.SetColumn(LibrarySearch, 0);
            Grid.SetRow(LibrarySearch, 0);
            Grid.SetColumn(StatusPicker, 1);
            Grid.SetRow(StatusPicker, 0);
            Grid.SetColumn(SortPicker, 2);
            Grid.SetRow(SortPicker, 0);
            StatusPicker.HorizontalOptions = LayoutOptions.Fill;
            SortPicker.HorizontalOptions = LayoutOptions.Fill;
            StatusPicker.WidthRequest = -1;
            SortPicker.WidthRequest = -1;
        }
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
                await CrashReporter.ReportAsync(exception, "LibraryPage.OnAppearing");
            }
        }
    }
}
