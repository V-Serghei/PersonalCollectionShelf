using Microsoft.Extensions.DependencyInjection;
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
        _hasQuery = true;

        if (query.TryGetValue("id", out var value) && Guid.TryParse(value?.ToString(), out var mediaItemId))
        {
            await ViewModel.LoadForEditAsync(mediaItemId);
            return;
        }

        await ViewModel.LoadForCreateAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_hasQuery)
        {
            await ViewModel.LoadForCreateAsync();
        }
    }

    private EditMediaItemViewModel ViewModel => (EditMediaItemViewModel)BindingContext;
}
