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
        GenresList.HandlerChanged += (_, _) => CollectionViewScrollTuner.EnableFreeScrolling(GenresList);
        GenresList.Loaded += (_, _) => CollectionViewScrollTuner.EnableFreeScrolling(GenresList);
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        CollectionViewScrollTuner.EnableFreeScrolling(TagsList);
        CollectionViewScrollTuner.EnableFreeScrolling(GenresList);
        await ViewModel.LoadAsync();
    }
    public TagsViewModel ViewModel => (TagsViewModel)BindingContext;

}
