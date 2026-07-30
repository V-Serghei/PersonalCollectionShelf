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

            var uniquePeople = item.Contributions
                .GroupBy(value => value.PersonId)
                .Select(personCredits =>
                {
                    var orderedCredits = personCredits
                        .OrderBy(value => RoleOrder(value.Role))
                        .ThenBy(value => value.SortOrder)
                        .ToList();
                    var primaryCredit = orderedCredits[0];
                    var roles = orderedCredits
                        .Select(value => value.Role)
                        .Distinct()
                        .Select(value => T($"ContributionRole.{value}"));
                    var creditDetails = orderedCredits
                        .Select(value => value.Details)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase);
                    var details = string.Join("  ·  ", roles.Concat(creditDetails!));
                    var creditedAs = orderedCredits
                        .Select(value => value.CreditedAs)
                        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) &&
                                                 !string.Equals(value, primaryCredit.PersonName, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
                    return new
                    {
                        PrimaryRole = primaryCredit.Role,
                        Item = new MediaContributorItemViewModel(
                            primaryCredit.PersonId,
                            primaryCredit.PersonName,
                            details,
                            creditedAs)
                    };
                })
                .ToList();

            foreach (var group in uniquePeople
                         .GroupBy(value => value.PrimaryRole)
                         .OrderBy(group => RoleOrder(group.Key)))
            {
                var people = group
                    .Select(value => value.Item)
                    .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
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
