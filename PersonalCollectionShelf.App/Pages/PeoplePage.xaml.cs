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
        AttachFreeScrolling(GridPeopleList);
        AttachFreeScrolling(ListPeopleList);
    }

    public PeopleViewModel ViewModel => (PeopleViewModel)BindingContext;

    private static void AttachFreeScrolling(CollectionView collectionView)
    {
        collectionView.HandlerChanged += (_, _) => CollectionViewScrollTuner.EnableFreeScrolling(collectionView);
        collectionView.Loaded += (_, _) => CollectionViewScrollTuner.EnableFreeScrolling(collectionView);
    }

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
