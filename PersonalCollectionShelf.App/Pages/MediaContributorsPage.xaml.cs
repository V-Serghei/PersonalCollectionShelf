using Microsoft.Extensions.DependencyInjection;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.App.ViewModels;

namespace PersonalCollectionShelf.App.Pages;

public partial class MediaContributorsPage : ContentPage, IQueryAttributable
{
    private Guid? _mediaItemId;

    public MediaContributorsPage() : this(App.Services.GetRequiredService<MediaContributorsViewModel>()) { }

    public MediaContributorsPage(MediaContributorsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public MediaContributorsViewModel ViewModel => (MediaContributorsViewModel)BindingContext;

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value) && Guid.TryParse(value?.ToString(), out var id))
        {
            _mediaItemId = id;
            try { await ViewModel.LoadAsync(id); }
            catch (Exception exception) { await CrashReporter.ReportAsync(exception, "MediaContributorsPage.ApplyQueryAttributes"); }
        }
    }

    public void SetNavigationTarget(Guid mediaItemId) => _mediaItemId = mediaItemId;

    public Task LoadNavigationTargetAsync() => _mediaItemId.HasValue ? ViewModel.LoadAsync(_mediaItemId.Value) : Task.CompletedTask;
}
