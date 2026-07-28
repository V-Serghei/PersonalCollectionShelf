using Microsoft.Extensions.DependencyInjection;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class SettingsPage : ContentPage
{
    private bool? _usesCompactLayout;

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
            try
            {
                await viewModel.LoadSyncStatusAsync();
                await viewModel.LoadAccountStateAsync();
            }
            catch (Exception exception)
            {
                await CrashReporter.ReportAsync(exception, "SettingsPage.OnAppearing");
            }
        }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width <= 0)
        {
            return;
        }

        var usesCompactLayout = DeviceInfo.Current.Idiom == DeviceIdiom.Phone || width < 700;
        if (_usesCompactLayout == usesCompactLayout)
        {
            return;
        }

        _usesCompactLayout = usesCompactLayout;
        TopBar.ColumnDefinitions.Clear();
        TopBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        TopBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        TopBar.Padding = usesCompactLayout ? new Thickness(12, 7) : new Thickness(18, 7);
        SettingsContent.Padding = usesCompactLayout
            ? new Thickness(14, 18, 14, 30)
            : new Thickness(0, 24, 0, 30);
        SettingsContent.Spacing = usesCompactLayout ? 18 : 24;
    }
}
