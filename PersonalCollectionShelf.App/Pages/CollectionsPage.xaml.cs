using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class CollectionsPage : ContentPage
{
    public CollectionsPage(CollectionsViewModel viewModel) { InitializeComponent(); BindingContext = viewModel; }
    protected override async void OnAppearing() { base.OnAppearing(); await ViewModel.LoadAsync(); }
    public CollectionsViewModel ViewModel => (CollectionsViewModel)BindingContext;
}
