using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class CollectionsPage : ContentPage
{
#if ANDROID
    private AndroidNativeCatalogRenderer.NativeCatalogBinding<CollectionCardViewModel>? _nativeBinding;
#endif

    public CollectionsPage(CollectionsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
#if ANDROID
        viewModel.PropertyChanged += HandleViewModelPropertyChanged;
        CollectionsList.HandlerChanged += (_, _) => AttachNativeList();
        AttachNativeList();
#else
        CollectionsList.HandlerChanged += (_, _) => CollectionViewScrollTuner.EnableFreeScrolling(CollectionsList);
        CollectionsList.Loaded += (_, _) => CollectionViewScrollTuner.EnableFreeScrolling(CollectionsList);
#endif
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
#if !ANDROID
        CollectionViewScrollTuner.EnableFreeScrolling(CollectionsList);
#endif
        await ViewModel.LoadAsync();
#if ANDROID
        AttachNativeList();
#endif
    }
    public CollectionsViewModel ViewModel => (CollectionsViewModel)BindingContext;

#if ANDROID
    private void HandleViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CollectionsViewModel.Collections)) Dispatcher.Dispatch(AttachNativeList);
    }

    private void AttachNativeList()
    {
        if (_nativeBinding is null || !_nativeBinding.IsCurrent(CollectionsList))
        {
            _nativeBinding?.Dispose();
            var span = DeviceInfo.Current.Idiom == DeviceIdiom.Phone ? 2 : DeviceInfo.Current.Idiom == DeviceIdiom.Tablet ? 3 : 4;
            _nativeBinding = AndroidNativeCatalogRenderer.AttachCollections(
                CollectionsList,
                ViewModel.Collections,
                span,
                item => ViewModel.OpenCollectionCommand.Execute(item));
        }
        else
        {
            _nativeBinding.Update(ViewModel.Collections);
        }
    }
#endif
}
