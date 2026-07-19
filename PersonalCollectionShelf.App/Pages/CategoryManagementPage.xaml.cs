using Microsoft.Extensions.DependencyInjection;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class CategoryManagementPage : ContentPage
{
    public CategoryManagementPage() : this(App.Services.GetRequiredService<CategoryManagementViewModel>()) { }

    public CategoryManagementPage(CategoryManagementViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public CategoryManagementViewModel ViewModel => (CategoryManagementViewModel)BindingContext;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await ((CategoryManagementViewModel)BindingContext).LoadAsync(); }
        catch (Exception exception) { await CrashReporter.ReportAsync(exception, "CategoryManagementPage.OnAppearing"); }
    }
}
