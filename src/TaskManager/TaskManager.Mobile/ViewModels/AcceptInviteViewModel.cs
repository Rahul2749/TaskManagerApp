using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.Mobile.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

[QueryProperty(nameof(Token), nameof(Token))]
public partial class AcceptInviteViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly IAppNavigationService _navigation;

    public AcceptInviteViewModel(IAuthService authService, IAppNavigationService navigation)
    {
        _authService = authService;
        _navigation = navigation;
        Title = "Accept invite";
    }

    [ObservableProperty] private string _token = string.Empty;
    [ObservableProperty] private string _inviteSummary = string.Empty;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _firstName = string.Empty;
    [ObservableProperty] private string _lastName = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _previewLoaded;

    partial void OnTokenChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            _ = LoadPreviewAsync();
    }

    [RelayCommand]
    private async Task LoadPreviewAsync()
    {
        ClearError();
        PreviewLoaded = false;
        InviteSummary = string.Empty;

        if (string.IsNullOrWhiteSpace(Token))
        {
            SetError("Paste the invite token from your email.");
            return;
        }

        try
        {
            IsBusy = true;
            var preview = await _authService.PreviewInviteAsync(Token.Trim());
            if (preview is null)
            {
                SetError("Invite is invalid or expired.");
                return;
            }

            InviteSummary = $"{preview.OrganizationName} · {preview.Email} · {preview.Role}";
            PreviewLoaded = true;
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AcceptAsync()
    {
        if (IsBusy) return;
        ClearError();

        if (string.IsNullOrWhiteSpace(Token) ||
            string.IsNullOrWhiteSpace(Username) ||
            string.IsNullOrWhiteSpace(Password) ||
            string.IsNullOrWhiteSpace(FirstName) ||
            string.IsNullOrWhiteSpace(LastName))
        {
            SetError("Fill in all fields and verify the invite token.");
            return;
        }

        if (Password.Length < 8)
        {
            SetError("Password must be at least 8 characters.");
            return;
        }

        try
        {
            IsBusy = true;
            var result = await _authService.AcceptInviteAsync(new AcceptInviteDto
            {
                Token = Token.Trim(),
                Username = Username.Trim(),
                Password = Password,
                FirstName = FirstName.Trim(),
                LastName = LastName.Trim()
            });

            if (!result.Success)
            {
                SetError(result.Error ?? "Could not accept invite.");
                return;
            }

            await _navigation.NavigateAfterAuthAsync(result.User?.NeedsOnboarding == true);
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
