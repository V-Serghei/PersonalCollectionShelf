using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class TagsPage : ContentPage
{
    public TagsPage(TagsViewModel viewModel) { InitializeComponent(); BindingContext = viewModel; }
    protected override async void OnAppearing() { base.OnAppearing(); await ViewModel.LoadAsync(); }
    public TagsViewModel ViewModel => (TagsViewModel)BindingContext;

    private async void HandleSearchClicked(object? sender, EventArgs e)
    {
        ViewModel.ToggleSearchCommand.Execute(null);
        if (ViewModel.IsSearchVisible) { await Task.Delay(50); TagSearch.Focus(); }
    }
}
