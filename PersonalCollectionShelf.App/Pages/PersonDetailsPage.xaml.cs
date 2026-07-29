using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class PersonDetailsPage : ContentPage
{
    public PersonDetailsPage(PersonDetailsViewModel viewModel) { InitializeComponent(); BindingContext = viewModel; }
    public PersonDetailsViewModel ViewModel => (PersonDetailsViewModel)BindingContext;
    protected override async void OnAppearing() { base.OnAppearing(); await ViewModel.LoadAsync(); }
}
