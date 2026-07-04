using Microsoft.Extensions.DependencyInjection;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class LibraryPage : ContentPage
{
    public LibraryPage()
        : this(App.Services.GetRequiredService<LibraryViewModel>())
    {
    }

    public LibraryPage(LibraryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is LibraryViewModel viewModel)
        {
            await viewModel.LoadAsync();
        }
    }
}
