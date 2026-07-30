using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Services;

namespace PersonalCollectionShelf.App.ViewModels;

public sealed class CollectionDetailItemViewModel : ObservableObject
{
    private string _coverSource;

    public CollectionDetailItemViewModel(
        Guid id,
        string title,
        string positionText,
        string mediaTypeLabel,
        Color mediaTypeColor,
        string metadata,
        string statusLabel,
        Color statusColor,
        string coverSource)
    {
        Id = id;
        Title = title;
        PositionText = positionText;
        MediaTypeLabel = mediaTypeLabel;
        MediaTypeColor = mediaTypeColor;
        Metadata = metadata;
        StatusLabel = statusLabel;
        StatusColor = statusColor;
        _coverSource = coverSource;
    }

    public Guid Id { get; }
    public string Title { get; }
    public string PositionText { get; }
    public bool HasPosition => !string.IsNullOrWhiteSpace(PositionText);
    public string MediaTypeLabel { get; }
    public Color MediaTypeColor { get; }
    public string Metadata { get; }
    public string StatusLabel { get; }
    public Color StatusColor { get; }
    public string CoverSource => _coverSource;
    public bool HasCover => !string.IsNullOrWhiteSpace(_coverSource);
    public bool HasNoCover => !HasCover;
    public string Initial => string.IsNullOrWhiteSpace(Title) ? "?" : char.ToUpperInvariant(Title[0]).ToString();

    public void SetCoverSource(string source)
    {
        if (string.Equals(_coverSource, source, StringComparison.Ordinal)) return;
        _coverSource = source;
        OnPropertyChanged(nameof(CoverSource));
        OnPropertyChanged(nameof(HasCover));
        OnPropertyChanged(nameof(HasNoCover));
    }
}

public partial class CollectionDetailsViewModel : BaseViewModel, IQueryAttributable
{
    private readonly ICollectionExplorerService _explorer;
    private readonly IAuthService _auth;
    private readonly UiThumbnailCache _thumbnailCache;
    private Guid _collectionId;
    private Guid _loadedCollectionId;
    private CollectionExplorerDto? _collection;
    private Task? _loadingTask;

    public CollectionDetailsViewModel(
        ICollectionExplorerService explorer,
        IAuthService auth,
        UiThumbnailCache thumbnailCache,
        ILocalizationService localization)
        : base(localization)
    {
        _explorer = explorer;
        _auth = auth;
        _thumbnailCache = thumbnailCache;
        IsBusy = true;
    }

    public ObservableCollection<CollectionDetailItemViewModel> Items { get; } = [];

    public string PageTitle => _collection?.Name ?? T("Collections.DetailsTitle");
    public string KindText => _collection is null ? string.Empty : T($"CollectionKind.{_collection.Kind}");
    public string ItemCountText => string.Format(T("Collections.ItemCountFormat"), Items.Count);
    public string EmptyText => T("Collections.DetailsEmpty");
    public string LoadingText => T("Collections.LoadingDetails");
    public string Description => _collection?.Description ?? string.Empty;
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var rawId) && Guid.TryParse(rawId?.ToString(), out var id))
        {
            SetCollectionId(id);
        }
    }

    public void SetCollectionId(Guid id)
    {
        if (_collectionId == id) return;
        _collectionId = id;
        IsBusy = true;
    }

    public Task LoadAsync()
    {
        if (_collectionId == Guid.Empty || _loadedCollectionId == _collectionId) return Task.CompletedTask;
        return _loadingTask ??= LoadCoreAsync();
    }

    private async Task LoadCoreAsync()
    {
        IsBusy = true;
        var targetId = _collectionId;
        try
        {
            var userId = await _auth.GetCurrentUserIdAsync() ?? "local-user";
            _collection = await _explorer.GetByIdAsync(targetId, userId);
            if (_collectionId != targetId) return;
            _loadedCollectionId = targetId;
            PopulateItems();
            OnPropertyChanged(string.Empty);
            _ = WarmCoversAsync(_collection?.Items ?? []);
        }
        finally
        {
            if (_collectionId == targetId) IsBusy = false;
            _loadingTask = null;
        }
    }

    private void PopulateItems()
    {
        Items.Clear();
        if (_collection is null) return;

        foreach (var item in _collection.Items)
        {
            var metadataParts = new[]
                {
                    item.ReleaseYear?.ToString(),
                    string.IsNullOrWhiteSpace(item.Category) ? null : item.Category
                }
                .Where(value => !string.IsNullOrWhiteSpace(value));
            var position = item.Position.HasValue ? $"#{item.Position.Value:0.##}" : string.Empty;
            var cover = MediaPresentation.HasValidCoverUrl(item.CoverUrl)
                ? _thumbnailCache.GetDisplaySource(item.CoverUrl)
                : string.Empty;
            Items.Add(new CollectionDetailItemViewModel(
                item.MediaItemId,
                item.Title,
                position,
                T($"MediaType.{item.MediaType}"),
                MediaPresentation.GetMediaTypeColor(item.MediaType),
                string.Join("  •  ", metadataParts),
                T($"MediaStatus.{item.Status}"),
                MediaPresentation.GetStatusForegroundColor(item.Status),
                cover));
        }
    }

    private async Task WarmCoversAsync(IReadOnlyList<CollectionItemDto> items)
    {
        await _thumbnailCache.WarmBatchAsync(items.Select(item => item.CoverUrl), 160, degreeOfParallelism: 3);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var sourceLookup = items
                .GroupBy(item => item.MediaItemId)
                .ToDictionary(group => group.Key, group => _thumbnailCache.GetDisplaySource(group.First().CoverUrl));
            foreach (var item in Items)
            {
                if (sourceLookup.TryGetValue(item.Id, out var source)) item.SetCoverSource(source);
            }
            OnPropertyChanged(nameof(Items));
        });
    }

    [RelayCommand]
    private Task GoBackAsync() => AppNavigation.CloseAsync();

    [RelayCommand]
    private Task OpenItemAsync(CollectionDetailItemViewModel item) => AppNavigation.OpenMediaDetailsAsync(item.Id);

    protected override void RefreshLocalizedProperties()
    {
        PopulateItems();
        base.RefreshLocalizedProperties();
    }
}
