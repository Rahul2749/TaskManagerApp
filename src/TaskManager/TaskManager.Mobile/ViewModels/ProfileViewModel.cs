using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.Mobile.Helpers;
using TaskManager.Mobile.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

public partial class ProfileViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly IAppNavigationService _navigation;
    private readonly IEntitlementService _entitlements;
    private readonly INotificationRealtimeService _notifications;
    private readonly IBiometricService _biometric;

    public ProfileViewModel(
        IAuthService authService,
        IAppNavigationService navigation,
        IEntitlementService entitlements,
        INotificationRealtimeService notifications,
        IBiometricService biometric)
    {
        _authService = authService;
        _navigation = navigation;
        _entitlements = entitlements;
        _notifications = notifications;
        _biometric = biometric;
        Title = "Profile";
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RoleDisplay))]
    [NotifyPropertyChangedFor(nameof(Initials))]
    [NotifyPropertyChangedFor(nameof(OrgLabel))]
    private UserDto? _user;

    [ObservableProperty] private bool _biometricUnlockEnabled;
    [ObservableProperty] private bool _biometricAvailable;
    [ObservableProperty] private TwoFactorStatusDto? _twoFactorStatus;
    [ObservableProperty] private string _twoFactorPassword = string.Empty;
    [ObservableProperty] private string _twoFactorCode = string.Empty;
    [ObservableProperty] private string? _twoFactorChallenge;
    [ObservableProperty] private bool _isEnableChallenge;
    [ObservableProperty] private string _recoveryCodesText = string.Empty;

    public string RoleDisplay => AppRoles.DisplayName(User?.Role);

    public string Initials
    {
        get
        {
            if (User is null) return "?";
            var first = string.IsNullOrWhiteSpace(User.FirstName) ? User.Username : User.FirstName;
            return string.IsNullOrEmpty(first) ? "?" : first[..1].ToUpperInvariant();
        }
    }

    public string OrgLabel =>
        string.IsNullOrWhiteSpace(User?.OrganizationName) ? "Your workspace" : User!.OrganizationName!;

    public bool TwoFactorEnabled => TwoFactorStatus?.Enabled == true;
    public bool CanEnableTwoFactor => TwoFactorStatus is { Enabled: false } && string.IsNullOrWhiteSpace(TwoFactorChallenge);
    public bool CanDisableTwoFactor => TwoFactorStatus is { Enabled: true } && string.IsNullOrWhiteSpace(TwoFactorChallenge);
    public bool IsTwoFactorChallengeVisible => !string.IsNullOrWhiteSpace(TwoFactorChallenge);
    public bool HasRecoveryCodes => !string.IsNullOrWhiteSpace(RecoveryCodesText);

    partial void OnTwoFactorStatusChanged(TwoFactorStatusDto? value)
    {
        OnPropertyChanged(nameof(TwoFactorEnabled));
        OnPropertyChanged(nameof(CanEnableTwoFactor));
        OnPropertyChanged(nameof(CanDisableTwoFactor));
    }

    partial void OnTwoFactorChallengeChanged(string? value)
    {
        OnPropertyChanged(nameof(CanEnableTwoFactor));
        OnPropertyChanged(nameof(CanDisableTwoFactor));
        OnPropertyChanged(nameof(IsTwoFactorChallengeVisible));
    }

    partial void OnRecoveryCodesTextChanged(string value) => OnPropertyChanged(nameof(HasRecoveryCodes));

    [RelayCommand]
    private async Task LoadAsync()
    {
        User = await _authService.GetCurrentUserAsync();
        TwoFactorStatus = await _authService.GetTwoFactorStatusAsync();
        BiometricAvailable = await _biometric.IsAvailableAsync();
        BiometricUnlockEnabled = BiometricService.IsEnabled;
    }

    partial void OnBiometricUnlockEnabledChanged(bool value)
    {
        BiometricService.IsEnabled = value;
    }

    [RelayCommand]
    private async Task StartEnableTwoFactorAsync()
    {
        ClearError();
        var result = await _authService.StartTwoFactorEnableAsync(TwoFactorPassword);
        if (result.Challenge is null) { SetError(result.Error ?? "Could not start two-factor setup."); return; }
        TwoFactorChallenge = result.Challenge.ChallengeToken;
        IsEnableChallenge = true;
        TwoFactorPassword = string.Empty;
    }

    [RelayCommand]
    private async Task StartDisableTwoFactorAsync()
    {
        ClearError();
        var result = await _authService.StartTwoFactorDisableAsync(TwoFactorPassword);
        if (result.Challenge is null) { SetError(result.Error ?? "Could not start two-factor removal."); return; }
        TwoFactorChallenge = result.Challenge.ChallengeToken;
        IsEnableChallenge = false;
        TwoFactorPassword = string.Empty;
    }

    [RelayCommand]
    private async Task VerifyTwoFactorAsync()
    {
        if (string.IsNullOrWhiteSpace(TwoFactorChallenge)) return;
        ClearError();
        if (IsEnableChallenge)
        {
            var result = await _authService.VerifyTwoFactorEnableAsync(TwoFactorChallenge, TwoFactorCode);
            if (result.RecoveryCodes is null) { SetError(result.Error ?? "The code is invalid or expired."); return; }
            RecoveryCodesText = string.Join(Environment.NewLine, result.RecoveryCodes.RecoveryCodes);
        }
        else
        {
            var result = await _authService.VerifyTwoFactorDisableAsync(TwoFactorChallenge, TwoFactorCode);
            if (!result.Success) { SetError(result.Error ?? "The code is invalid or expired."); return; }
        }

        TwoFactorChallenge = null;
        TwoFactorCode = string.Empty;
        TwoFactorStatus = await _authService.GetTwoFactorStatusAsync();
    }

    [RelayCommand]
    private void CancelTwoFactorChange()
    {
        TwoFactorChallenge = null;
        TwoFactorCode = string.Empty;
        TwoFactorPassword = string.Empty;
        ClearError();
    }

    [RelayCommand]
    private void DismissRecoveryCodes() => RecoveryCodesText = string.Empty;

    [RelayCommand]
    private async Task GoToBillingAsync() => await Shell.Current.GoToAsync("//billing");

    [RelayCommand]
    private async Task GoToNotificationsAsync() => await Shell.Current.GoToAsync("//notifications");

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _notifications.DisconnectAsync();
        await _authService.LogoutAsync();
        _entitlements.Clear();
        await _navigation.GoToLoginAsync();
    }
}
