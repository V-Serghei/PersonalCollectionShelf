using Microsoft.Extensions.DependencyInjection;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class LibraryPage : ContentPage
{
    private bool? _usesCompactLayout;
    private int _lastThumbnailCenter = -100;
#if ANDROID
    private AndroidNativeCatalogRenderer.NativeCatalogBinding<MediaItemListItemViewModel>? _nativeGridBinding;
    private AndroidNativeCatalogRenderer.NativeCatalogBinding<MediaItemListItemViewModel>? _nativeListBinding;
#endif

    public LibraryPage()
        : this(App.Services.GetRequiredService<LibraryViewModel>())
    {
    }

    public LibraryPage(LibraryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.PropertyChanged += HandleViewModelPropertyChanged;
#if ANDROID
        GridLibraryList.HandlerChanged += (_, _) => AttachNativeLists();
        ListLibraryList.HandlerChanged += (_, _) => AttachNativeLists();
        AttachNativeLists();
#else
        AttachFreeScrolling(GridLibraryList);
        AttachFreeScrolling(ListLibraryList);
#endif
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
#if ANDROID
        if (e.PropertyName == nameof(LibraryViewModel.MediaItems))
        {
            Dispatcher.Dispatch(AttachNativeLists);
        }
#endif
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
        ViewModel.EnsureThumbnailLookAhead(e.LastVisibleItemIndex);
        var center = (e.FirstVisibleItemIndex + e.LastVisibleItemIndex) / 2;
        if (Math.Abs(center - _lastThumbnailCenter) < 12) return;
        _lastThumbnailCenter = center;
        ViewModel.PreloadThumbnails(e.FirstVisibleItemIndex, e.LastVisibleItemIndex);
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
#if ANDROID
                AttachNativeLists();
#else
                ApplyFreeScrolling();
                await Task.Delay(100);
                ApplyFreeScrolling();
#endif
            }
            catch (Exception exception)
            {
                await CrashReporter.ReportAsync(exception, "LibraryPage.OnAppearing");
            }
        }
    }

    private void ApplyFreeScrolling()
    {
        CollectionViewScrollTuner.EnableFreeScrolling(GridLibraryList);
        CollectionViewScrollTuner.EnableFreeScrolling(ListLibraryList);
    }

#if ANDROID
    private void AttachNativeLists()
    {
        if (_nativeGridBinding is null || !_nativeGridBinding.IsCurrent(GridLibraryList))
        {
            _nativeGridBinding?.Dispose();
            _nativeGridBinding = AndroidNativeCatalogRenderer.AttachMedia(
                GridLibraryList,
                ViewModel.MediaItems,
                grid: true,
                ViewModel.GridColumnCount,
                item => ViewModel.OpenMediaItemCommand.Execute(item));
        }
        else
        {
            _nativeGridBinding.Update(ViewModel.MediaItems);
        }

        if (_nativeListBinding is null || !_nativeListBinding.IsCurrent(ListLibraryList))
        {
            _nativeListBinding?.Dispose();
            _nativeListBinding = AndroidNativeCatalogRenderer.AttachMedia(
                ListLibraryList,
                ViewModel.MediaItems,
                grid: false,
                1,
                item => ViewModel.OpenMediaItemCommand.Execute(item));
        }
        else
        {
            _nativeListBinding.Update(ViewModel.MediaItems);
        }
    }
#endif
}
