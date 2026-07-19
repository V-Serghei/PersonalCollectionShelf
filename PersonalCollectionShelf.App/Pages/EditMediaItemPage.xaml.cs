using Microsoft.Extensions.DependencyInjection;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class EditMediaItemPage : ContentPage, IQueryAttributable
{
    private bool _hasQuery;

    public EditMediaItemPage()
        : this(App.Services.GetRequiredService<EditMediaItemViewModel>())
    {
    }

    public EditMediaItemPage(EditMediaItemViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        try
        {
            _hasQuery = true;

            if (query.TryGetValue("id", out var value) && Guid.TryParse(value?.ToString(), out var mediaItemId))
            {
                await ViewModel.LoadForEditAsync(mediaItemId);
                return;
            }

            await ViewModel.LoadForCreateAsync();
        }
        catch (Exception exception)
        {
            await CrashReporter.ReportAsync(exception, "EditMediaItemPage.ApplyQueryAttributes");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_hasQuery)
        {
            try
            {
                await ViewModel.LoadForCreateAsync();
            }
            catch (Exception exception)
            {
                await CrashReporter.ReportAsync(exception, "EditMediaItemPage.OnAppearing");
            }
        }
    }

    public EditMediaItemViewModel ViewModel => (EditMediaItemViewModel)BindingContext;
}
