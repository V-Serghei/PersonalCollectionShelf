using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class TagDetailsPage : ContentPage
{
#if ANDROID
    private AndroidNativeCatalogRenderer.NativeCatalogBinding<TagMediaItemViewModel>? _nativeBinding;
#endif

    public TagDetailsPage(TagDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.PropertyChanged += HandleViewModelPropertyChanged;
#if ANDROID
        ItemsList.HandlerChanged += (_, _) => AttachNativeList();
        AttachNativeList();
#else
        ItemsList.HandlerChanged += (_, _) => CollectionViewScrollTuner.EnableFreeScrolling(ItemsList);
        ItemsList.Loaded += (_, _) => CollectionViewScrollTuner.EnableFreeScrolling(ItemsList);
#endif
    }

    public TagDetailsViewModel ViewModel => (TagDetailsViewModel)BindingContext;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadAsync();
#if ANDROID
        AttachNativeList();
#else
        CollectionViewScrollTuner.EnableFreeScrolling(ItemsList);
#endif
    }

    private void HandleViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
#if ANDROID
        if (e.PropertyName == nameof(TagDetailsViewModel.Items))
        {
            Dispatcher.Dispatch(AttachNativeList);
        }
#endif
    }

#if ANDROID
    private void AttachNativeList()
    {
        if (_nativeBinding is null || !_nativeBinding.IsCurrent(ItemsList))
        {
            _nativeBinding?.Dispose();
            _nativeBinding = AndroidNativeCatalogRenderer.AttachTagItems(
                ItemsList,
                ViewModel.Items,
                item => ViewModel.OpenItemCommand.Execute(item));
        }
        else
        {
            _nativeBinding.Update(ViewModel.Items);
        }
    }
#endif

    private async void HandleSearchClicked(object? sender, EventArgs e)
    {
        ViewModel.ToggleSearchCommand.Execute(null);
        if (ViewModel.IsSearchVisible)
        {
            await Task.Delay(50);
            ItemSearch.Focus();
        }
    }
}
