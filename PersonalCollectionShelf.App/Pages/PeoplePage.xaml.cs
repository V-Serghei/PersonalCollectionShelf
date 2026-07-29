using Microsoft.Extensions.DependencyInjection;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class PeoplePage : ContentPage
{
    public PeoplePage() : this(App.Services.GetRequiredService<PeopleViewModel>()) { }

    public PeoplePage(PeopleViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public PeopleViewModel ViewModel => (PeopleViewModel)BindingContext;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await ViewModel.LoadAsync(); }
        catch (Exception exception) { await CrashReporter.ReportAsync(exception, "PeoplePage.OnAppearing"); }
    }

    private async void HandleSearchClicked(object? sender, EventArgs e)
    {
        ViewModel.ToggleSearchCommand.Execute(null);
        if (ViewModel.IsSearchVisible)
        {
            await Task.Delay(50);
            PeopleSearch.Focus();
        }
    }
}
