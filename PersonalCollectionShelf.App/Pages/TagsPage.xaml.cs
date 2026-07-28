using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class TagsPage : ContentPage
{
    public TagsPage(TagsViewModel viewModel) { InitializeComponent(); BindingContext = viewModel; }
    protected override async void OnAppearing() { base.OnAppearing(); await ViewModel.LoadAsync(); }
    public TagsViewModel ViewModel => (TagsViewModel)BindingContext;
}
