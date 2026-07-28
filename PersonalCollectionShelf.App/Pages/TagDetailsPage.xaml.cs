using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class TagDetailsPage : ContentPage
{
    public TagDetailsPage(TagDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public TagDetailsViewModel ViewModel => (TagDetailsViewModel)BindingContext;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadAsync();
    }

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
