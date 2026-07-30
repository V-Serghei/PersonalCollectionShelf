using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class PersonGalleryPage : ContentPage
{
    public PersonGalleryPage(PersonGalleryViewModel viewModel) { InitializeComponent(); BindingContext = viewModel; }
    protected override async void OnAppearing() { base.OnAppearing(); await ((PersonGalleryViewModel)BindingContext).LoadAsync(); }
}
