using Microsoft.Extensions.DependencyInjection;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class DashboardPage : ContentPage
{
    public DashboardPage()
        : this(App.Services.GetRequiredService<LibraryViewModel>())
    {
    }

    public DashboardPage(LibraryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public LibraryViewModel ViewModel => (LibraryViewModel)BindingContext;

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is LibraryViewModel viewModel)
        {
            try
            {
                await viewModel.LoadAsync();
            }
            catch (Exception exception)
            {
                await CrashReporter.ReportAsync(exception, "DashboardPage.OnAppearing");
            }
        }
    }
}
