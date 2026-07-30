using Microsoft.Extensions.DependencyInjection;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class PeoplePage : ContentPage
{
    private int _lastThumbnailCenter = -100;
#if ANDROID
    private AndroidNativeCatalogRenderer.NativeCatalogBinding<PersonCardViewModel>? _nativeGridBinding;
    private AndroidNativeCatalogRenderer.NativeCatalogBinding<PersonCardViewModel>? _nativeListBinding;
#endif

    public PeoplePage() : this(App.Services.GetRequiredService<PeopleViewModel>()) { }

    public PeoplePage(PeopleViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
#if ANDROID
        viewModel.PropertyChanged += HandleViewModelPropertyChanged;
        GridPeopleList.HandlerChanged += (_, _) => AttachNativeLists();
        ListPeopleList.HandlerChanged += (_, _) => AttachNativeLists();
        AttachNativeLists();
#else
        AttachFreeScrolling(GridPeopleList);
        AttachFreeScrolling(ListPeopleList);
#endif
    }

    public PeopleViewModel ViewModel => (PeopleViewModel)BindingContext;

    private static void AttachFreeScrolling(CollectionView collectionView)
    {
        collectionView.HandlerChanged += (_, _) => CollectionViewScrollTuner.EnableFreeScrolling(collectionView);
        collectionView.Loaded += (_, _) => CollectionViewScrollTuner.EnableFreeScrolling(collectionView);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            ViewModel.RefreshDisplayPreferences();
            await ViewModel.LoadAsync();
#if ANDROID
            AttachNativeLists();
#else
            ApplyFreeScrolling();
            await Task.Delay(100);
            ApplyFreeScrolling();
#endif
        }
        catch (Exception exception) { await CrashReporter.ReportAsync(exception, "PeoplePage.OnAppearing"); }
    }

    private async void HandleSearchClicked(object? sender, EventArgs e)
    {
        ViewModel.ToggleSearchCommand.Execute(null);
        if (ViewModel.IsSearchVisible)
        {
            await Task.Delay(50);
            PeopleSearch.Focus();
        }
    }

    private void HandlePeopleScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        ViewModel.EnsureThumbnailLookAhead(e.LastVisibleItemIndex);
        var center = (e.FirstVisibleItemIndex + e.LastVisibleItemIndex) / 2;
        if (Math.Abs(center - _lastThumbnailCenter) < 12) return;
        _lastThumbnailCenter = center;
        ViewModel.PreloadThumbnails(e.FirstVisibleItemIndex, e.LastVisibleItemIndex);
    }

    private void ApplyFreeScrolling()
    {
        CollectionViewScrollTuner.EnableFreeScrolling(GridPeopleList);
        CollectionViewScrollTuner.EnableFreeScrolling(ListPeopleList);
    }

#if ANDROID
    private void HandleViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PeopleViewModel.People)) Dispatcher.Dispatch(AttachNativeLists);
    }

    private void AttachNativeLists()
    {
        if (_nativeGridBinding is null || !_nativeGridBinding.IsCurrent(GridPeopleList))
        {
            _nativeGridBinding?.Dispose();
            _nativeGridBinding = AndroidNativeCatalogRenderer.AttachPeople(
                GridPeopleList,
                ViewModel.People,
                grid: true,
                ViewModel.GridColumnCount,
                item => ViewModel.OpenPersonCommand.Execute(item));
        }
        else
        {
            _nativeGridBinding.Update(ViewModel.People);
        }

        if (_nativeListBinding is null || !_nativeListBinding.IsCurrent(ListPeopleList))
        {
            _nativeListBinding?.Dispose();
            _nativeListBinding = AndroidNativeCatalogRenderer.AttachPeople(
                ListPeopleList,
                ViewModel.People,
                grid: false,
                1,
                item => ViewModel.OpenPersonCommand.Execute(item));
        }
        else
        {
            _nativeListBinding.Update(ViewModel.People);
        }
    }
#endif
}
