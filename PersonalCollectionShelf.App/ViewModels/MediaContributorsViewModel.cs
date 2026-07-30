using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.ViewModels;

public sealed record MediaContributorItemViewModel(Guid PersonId, string Name, string Details, string CreditedAs);

public sealed record MediaContributorGroupViewModel(
    ContributionRole Role,
    string Label,
    IReadOnlyList<MediaContributorItemViewModel> People);

public partial class MediaContributorsViewModel(
    IMediaItemService mediaItems,
    IAuthService auth,
    ILocalizationService localization) : BaseViewModel(localization)
{
    private Guid? _mediaItemId;
    private string _title = string.Empty;

    public ObservableCollection<MediaContributorGroupViewModel> Groups { get; } = [];

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string PageTitle => T("Contributors.Title");
    public string BackText => T("Common.Back");
    public string EmptyText => T("Contributors.Empty");

    public async Task LoadAsync(Guid mediaItemId)
    {
        if (IsBusy) return;
        _mediaItemId = mediaItemId;
        IsBusy = true;
        try
        {
            var userId = await auth.GetCurrentUserIdAsync() ?? "local-user";
            var item = await mediaItems.GetMediaItemAsync(mediaItemId, userId);
            Title = item?.Title ?? PageTitle;
            Groups.Clear();
            if (item is null) return;

            foreach (var group in item.Contributions
                         .GroupBy(value => value.Role)
                         .OrderBy(group => RoleOrder(group.Key)))
            {
                var people = group
                    .OrderBy(value => value.SortOrder)
                    .ThenBy(value => value.PersonName, StringComparer.OrdinalIgnoreCase)
                    .Select(value => new MediaContributorItemViewModel(
                        value.PersonId,
                        value.PersonName,
                        value.Details ?? string.Empty,
                        string.IsNullOrWhiteSpace(value.CreditedAs) || string.Equals(value.CreditedAs, value.PersonName, StringComparison.OrdinalIgnoreCase)
                            ? string.Empty
                            : value.CreditedAs!))
                    .ToList();
                Groups.Add(new MediaContributorGroupViewModel(group.Key, T($"ContributionRole.{group.Key}"), people));
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task OpenPersonAsync(MediaContributorItemViewModel person) => AppNavigation.OpenPersonAsync(person.PersonId);

    [RelayCommand]
    private Task BackAsync() => AppNavigation.CloseAsync();

    private static int RoleOrder(ContributionRole role) => role switch
    {
        ContributionRole.Director => 0,
        ContributionRole.Author => 1,
        ContributionRole.Developer => 2,
        ContributionRole.Producer => 3,
        ContributionRole.Screenwriter => 4,
        ContributionRole.Cinematographer => 5,
        ContributionRole.Composer => 6,
        ContributionRole.CastingDirector => 7,
        ContributionRole.ProductionDesigner => 8,
        ContributionRole.Illustrator => 9,
        ContributionRole.Artist => 10,
        ContributionRole.Translator => 11,
        ContributionRole.Editor => 12,
        ContributionRole.Actor => 13,
        ContributionRole.VoiceActor => 14,
        _ => 99
    };
}
