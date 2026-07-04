using Microsoft.Extensions.DependencyInjection;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class MediaDetailsPage : ContentPage, IQueryAttributable
{
    private Guid? _mediaItemId;

    public MediaDetailsPage()
        : this(App.Services.GetRequiredService<MediaDetailsViewModel>())
    {
    }

    public MediaDetailsPage(MediaDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value) && Guid.TryParse(value?.ToString(), out var mediaItemId))
        {
            _mediaItemId = mediaItemId;
            await ViewModel.LoadAsync(mediaItemId);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_mediaItemId.HasValue)
        {
            await ViewModel.ReloadAsync();
        }
    }

    private MediaDetailsViewModel ViewModel => (MediaDetailsViewModel)BindingContext;
}
