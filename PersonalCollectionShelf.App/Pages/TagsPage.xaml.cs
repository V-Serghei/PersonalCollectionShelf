using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class TagsPage : ContentPage
{
    public TagsPage(TagsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        TagsList.HandlerChanged += (_, _) => CollectionViewScrollTuner.EnableFreeScrolling(TagsList);
        TagsList.Loaded += (_, _) => CollectionViewScrollTuner.EnableFreeScrolling(TagsList);
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        CollectionViewScrollTuner.EnableFreeScrolling(TagsList);
        await ViewModel.LoadAsync();
    }
    public TagsViewModel ViewModel => (TagsViewModel)BindingContext;

}
