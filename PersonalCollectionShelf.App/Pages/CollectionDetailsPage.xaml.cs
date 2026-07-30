using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class CollectionDetailsPage : ContentPage
{
#if ANDROID
    private AndroidNativeCatalogRenderer.NativeCatalogBinding<CollectionDetailItemViewModel>? _nativeBinding;
#endif

    public CollectionDetailsPage(CollectionDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
#if ANDROID
        viewModel.PropertyChanged += HandleViewModelPropertyChanged;
        CollectionItemsList.HandlerChanged += (_, _) => AttachNativeList();
        AttachNativeList();
#else
        CollectionItemsList.HandlerChanged += (_, _) => CollectionViewScrollTuner.EnableFreeScrolling(CollectionItemsList);
        CollectionItemsList.Loaded += (_, _) => CollectionViewScrollTuner.EnableFreeScrolling(CollectionItemsList);
#endif
    }

    public CollectionDetailsViewModel ViewModel => (CollectionDetailsViewModel)BindingContext;

    public void SetNavigationTarget(Guid collectionId) => ViewModel.SetCollectionId(collectionId);

    protected override async void OnAppearing()
    {
        base.OnAppearing();
#if !ANDROID
        CollectionViewScrollTuner.EnableFreeScrolling(CollectionItemsList);
#endif
        await ViewModel.LoadAsync();
#if ANDROID
        AttachNativeList();
#endif
    }

#if ANDROID
    private void HandleViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CollectionDetailsViewModel.Items)) Dispatcher.Dispatch(AttachNativeList);
    }

    private void AttachNativeList()
    {
        if (_nativeBinding is null || !_nativeBinding.IsCurrent(CollectionItemsList))
        {
            _nativeBinding?.Dispose();
            _nativeBinding = AndroidNativeCatalogRenderer.AttachCollectionItems(
                CollectionItemsList,
                ViewModel.Items,
                item => ViewModel.OpenItemCommand.Execute(item));
        }
        else
        {
            _nativeBinding.Update(ViewModel.Items);
        }
    }
#endif
}
