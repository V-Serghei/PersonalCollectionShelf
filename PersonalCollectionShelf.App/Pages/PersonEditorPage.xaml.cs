using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class PersonEditorPage : ContentPage
{
    public PersonEditorPage(PersonEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        MobileFormFocus.Attach(this, EditorScroll);
    }

    public PersonEditorViewModel ViewModel => (PersonEditorViewModel)BindingContext;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadAsync();
    }
}
