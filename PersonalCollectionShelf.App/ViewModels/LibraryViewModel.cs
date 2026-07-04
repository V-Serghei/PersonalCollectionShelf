using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Models;
using PersonalCollectionShelf.App.Pages;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.ViewModels;

public partial class LibraryViewModel : BaseViewModel
{
    private readonly IMediaItemService _mediaItemService;
    private readonly IAuthService _authService;
    private bool _suppressFilterReload;

    public LibraryViewModel(
        IMediaItemService mediaItemService,
        IAuthService authService,
        ILocalizationService localizationService)
        : base(localizationService)
    {
        _mediaItemService = mediaItemService;
        _authService = authService;
        ReloadFilterOptions();
    }

    public ObservableCollection<MediaItemListItemViewModel> MediaItems { get; } = [];

    public ObservableCollection<LocalizedOption<MediaType?>> MediaTypeFilters { get; } = [];

    public ObservableCollection<LocalizedOption<MediaStatus?>> StatusFilters { get; } = [];

    [ObservableProperty]
    private string searchTerm = string.Empty;

    [ObservableProperty]
    private LocalizedOption<MediaType?>? selectedMediaTypeFilter;

    [ObservableProperty]
    private LocalizedOption<MediaStatus?>? selectedStatusFilter;

    public string PageTitle => T("Library.Title");

    public string SearchPlaceholder => T("Library.SearchPlaceholder");

    public string MediaTypeFilterPlaceholder => T("Library.MediaTypeFilterPlaceholder");

    public string StatusFilterPlaceholder => T("Library.StatusFilterPlaceholder");

    public string AddButtonText => T("Library.AddButton");

    public string EmptyLibraryText => T("Library.Empty");

    protected override void RefreshLocalizedProperties()
    {
        base.RefreshLocalizedProperties();
        ReloadFilterOptions();
        _ = LoadAsync();
    }

    partial void OnSelectedMediaTypeFilterChanged(LocalizedOption<MediaType?>? value)
    {
        if (!_suppressFilterReload)
        {
            _ = LoadAsync();
        }
    }

    partial void OnSelectedStatusFilterChanged(LocalizedOption<MediaStatus?>? value)
    {
        if (!_suppressFilterReload)
        {
            _ = LoadAsync();
        }
    }

    partial void OnSearchTermChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _ = LoadAsync();
        }
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var userId = await GetCurrentUserIdAsync();
            var items = await _mediaItemService.SearchMediaItemsAsync(new MediaItemSearchCriteria
            {
                UserId = userId,
                SearchTerm = SearchTerm,
                MediaType = SelectedMediaTypeFilter?.Value,
                Status = SelectedStatusFilter?.Value
            });

            MediaItems.Clear();
            foreach (var item in items)
            {
                MediaItems.Add(ToListItem(item));
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateMediaItemAsync()
    {
        await Shell.Current.GoToAsync(nameof(EditMediaItemPage));
    }

    [RelayCommand]
    private async Task OpenMediaItemAsync(MediaItemListItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        await Shell.Current.GoToAsync($"{nameof(MediaDetailsPage)}?id={item.Id}");
    }

    private void ReloadFilterOptions()
    {
        var selectedMediaType = SelectedMediaTypeFilter?.Value;
        var selectedStatus = SelectedStatusFilter?.Value;

        _suppressFilterReload = true;
        try
        {
            MediaTypeFilters.Clear();
            MediaTypeFilters.Add(new LocalizedOption<MediaType?>(null, T("Common.All")));
            foreach (var mediaType in Enum.GetValues<MediaType>())
            {
                MediaTypeFilters.Add(new LocalizedOption<MediaType?>(mediaType, T($"MediaType.{mediaType}")));
            }

            StatusFilters.Clear();
            StatusFilters.Add(new LocalizedOption<MediaStatus?>(null, T("Common.All")));
            foreach (var status in Enum.GetValues<MediaStatus>())
            {
                StatusFilters.Add(new LocalizedOption<MediaStatus?>(status, T($"MediaStatus.{status}")));
            }

            SelectedMediaTypeFilter = MediaTypeFilters.First(option => EqualityComparer<MediaType?>.Default.Equals(option.Value, selectedMediaType));
            SelectedStatusFilter = StatusFilters.First(option => EqualityComparer<MediaStatus?>.Default.Equals(option.Value, selectedStatus));
        }
        finally
        {
            _suppressFilterReload = false;
        }
    }

    private MediaItemListItemViewModel ToListItem(MediaItemDto item)
    {
        var type = T($"MediaType.{item.MediaType}");
        var status = T($"MediaStatus.{item.Status}");
        var progress = item.ProgressTotal.HasValue
            ? string.Format(T("Library.ProgressWithTotalFormat"), item.ProgressCurrent, item.ProgressTotal.Value)
            : string.Format(T("Library.ProgressFormat"), item.ProgressCurrent);
        var rating = item.Rating.HasValue
            ? string.Format(T("Library.RatingFormat"), item.Rating.Value)
            : T("Library.NoRating");

        return new MediaItemListItemViewModel(
            item.Id,
            item.Title,
            $"{type} · {status}",
            progress,
            rating,
            item.IsFavorite,
            T("Library.OpenButton"));
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        return await _authService.GetCurrentUserIdAsync() ?? "local-user";
    }
}
