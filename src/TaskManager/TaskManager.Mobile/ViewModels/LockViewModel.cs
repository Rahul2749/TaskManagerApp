using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.Mobile.Services;

namespace TaskManager.Mobile.ViewModels;

public partial class LockViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly IBiometricService _biometric;
    private readonly IAppNavigationService _navigation;
    private readonly IDeepLinkService _deepLinks;

    public LockViewModel(
        IAuthService authService,
        IBiometricService biometric,
        IAppNavigationService navigation,
        IDeepLinkService deepLinks)
    {
        _authService = authService;
        _biometric = biometric;
        _navigation = navigation;
        _deepLinks = deepLinks;
        Title = "Unlock";
    }

    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _biometricAvailable;
    [ObservableProperty] private string _userLabel = "Welcome back";

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var user = await _authService.GetCurrentUserAsync();
        if (user is not null)
            UserLabel = string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName;

        BiometricAvailable = BiometricService.IsEnabled && await _biometric.IsAvailableAsync();
        if (BiometricAvailable)
            await UnlockWithBiometricAsync();
    }

    [RelayCommand]
    private async Task UnlockWithBiometricAsync()
    {
        if (IsBusy) return;
        ClearError();
        try
        {
            IsBusy = true;
            var ok = await _biometric.AuthenticateAsync("Unlock your workspace");
            if (ok)
                await EnterAppAsync();
            else
                SetError("Biometric unlock cancelled. Enter your password.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task UnlockWithPasswordAsync()
    {
        if (IsBusy) return;
        ClearError();

        var user = await _authService.GetCurrentUserAsync();
        if (user is null || string.IsNullOrWhiteSpace(Password))
        {
            SetError("Enter your password.");
            return;
        }

        try
        {
            IsBusy = true;
            var result = await _authService.LoginAsync(new Shared.DTOs.LoginDto
            {
                Username = user.Username,
                Password = Password
            });

            if (!result.Success)
            {
                SetError(result.Error ?? "Incorrect password.");
                return;
            }

            await EnterAppAsync();
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
    private async Task SignOutAsync()
    {
        await _authService.LogoutAsync();
        await _navigation.GoToLoginAsync();
    }

    private async Task EnterAppAsync()
    {
        var user = await _authService.GetCurrentUserAsync();
        await _navigation.NavigateAfterAuthAsync(user?.NeedsOnboarding == true);
        await _deepLinks.TryHandleAsync();
    }
}
