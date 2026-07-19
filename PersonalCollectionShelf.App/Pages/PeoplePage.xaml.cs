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
        try { await ((PeopleViewModel)BindingContext).LoadAsync(); }
        catch (Exception exception) { await CrashReporter.ReportAsync(exception, "PeoplePage.OnAppearing"); }
    }
}
