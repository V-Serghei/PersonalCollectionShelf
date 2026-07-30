using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Services;

namespace PersonalCollectionShelf.App.ViewModels;

public partial class ProfileViewModel(IAuthService auth, IMediaItemService mediaItems, ILocalizationService localization) : BaseViewModel(localization)
{
    [ObservableProperty] public partial string DisplayName { get; set; } = string.Empty;
    [ObservableProperty] public partial string ItemCountText { get; set; } = string.Empty;
    [ObservableProperty] public partial string Initial { get; set; } = "S";

    public string PageTitle => T("Profile.Title");
    public string AccountSectionTitle => T("Profile.Account");
    public string OpenSettingsText => T("Profile.OpenSettings");
    public string OpenPeopleText => T("Profile.OpenPeople");

    public async Task LoadAsync()
    {
        var email = await auth.GetSignedInEmailAsync();
        var userId = await auth.GetCurrentUserIdAsync() ?? "local-user";
        DisplayName = email ?? T("Shell.LocalProfile");
        Initial = string.IsNullOrWhiteSpace(email) ? "S" : char.ToUpperInvariant(email[0]).ToString();
        ItemCountText = string.Format(T("Shell.ItemsCollected"), await mediaItems.GetLibraryItemCountAsync(userId));
    }

    [RelayCommand] private Task OpenSettingsAsync() => Shell.Current.GoToAsync("//Settings");
    [RelayCommand] private Task OpenPeopleAsync() => Shell.Current.GoToAsync("//People");
}
