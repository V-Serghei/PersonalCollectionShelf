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
        TopBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        TopBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        TopBar.Padding = usesCompactLayout ? new Thickness(12, 7) : new Thickness(18, 7);

        LibraryContent.Padding = usesCompactLayout
            ? new Thickness(12, 6, 12, 0)
            : new Thickness(0, 8, 0, 0);
    }

    private void HandleLibraryScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        if (e.VerticalOffset > 24 && e.VerticalDelta > 4)
        {
            ViewModel.CollapseFilters();
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is LibraryViewModel viewModel)
        {
            try
            {
                viewModel.RefreshDisplayPreferences();
                await viewModel.LoadAsync();
            }
            catch (Exception exception)
            {
                await CrashReporter.ReportAsync(exception, "LibraryPage.OnAppearing");
            }
        }
    }
}
