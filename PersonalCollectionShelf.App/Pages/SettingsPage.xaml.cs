using Microsoft.Extensions.DependencyInjection;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
        : this(App.Services.GetRequiredService<SettingsViewModel>())
    {
    }

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is SettingsViewModel viewModel)
        {
            await viewModel.LoadSyncStatusAsync();
        }
    }
}
