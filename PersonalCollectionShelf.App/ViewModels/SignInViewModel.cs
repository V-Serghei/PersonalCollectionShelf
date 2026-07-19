using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Services;

namespace PersonalCollectionShelf.App.ViewModels;

public partial class SignInViewModel : BaseViewModel
{
    private readonly IAuthService _authService;

    private string _email = string.Empty;
    private string _password = string.Empty;
    private bool _isPasswordHidden = true;
    private string? _statusKey;
    private string _statusMessage = string.Empty;

    public SignInViewModel(IAuthService authService, ILocalizationService localizationService)
        : base(localizationService)
    {
        _authService = authService;
    }

    public string PageTitle => T("Settings.Account.SignIn");

    public string Description => T("Settings.Account.Description");

    public string EmailPlaceholder => T("Settings.Account.EmailPlaceholder");

    public string PasswordPlaceholder => T("Settings.Account.PasswordPlaceholder");

    public string SignInButtonText => T("Settings.Account.SignIn");

    public string SignUpButtonText => T("Settings.Account.SignUp");

    public string CancelButtonText => T("Common.Cancel");

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public bool IsPasswordHidden
    {
        get => _isPasswordHidden;
        set => SetProperty(ref _isPasswordHidden, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    protected override void RefreshLocalizedProperties()
    {
        base.RefreshLocalizedProperties();

        if (_statusKey is not null)
        {
            StatusMessage = T(_statusKey);
        }
    }

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordHidden = !IsPasswordHidden;
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        await ExecuteAuthFlowAsync(_authService.SignInAsync);
    }

    [RelayCommand]
    private async Task SignUpAsync()
    {
        await ExecuteAuthFlowAsync(_authService.SignUpAsync);
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await CloseAsync();
    }

    private async Task ExecuteAuthFlowAsync(
        Func<string, string, CancellationToken, Task<AuthResultDto>> authAction)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await authAction(Email, Password, CancellationToken.None);

            if (result.Succeeded)
            {
                Password = string.Empty;
                await CloseAsync();
            }
            else
            {
                SetStatus(result.ErrorKey ?? "Auth.Error.Unknown");
            }
        }
        catch (Exception exception)
        {
            SetStatus("Auth.Error.Unknown");
            await CrashReporter.ReportAsync(exception, "SignInViewModel.ExecuteAuthFlowAsync");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static async Task CloseAsync()
    {
        if (Shell.Current?.Navigation.ModalStack.Count > 0)
        {
            await Shell.Current.Navigation.PopModalAsync();
        }
    }

    private void SetStatus(string key)
    {
        _statusKey = key;
        StatusMessage = T(key);
    }
}
