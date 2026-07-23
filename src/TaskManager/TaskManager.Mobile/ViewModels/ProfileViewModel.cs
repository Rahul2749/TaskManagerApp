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

    [RelayCommand]
    private async Task LoadAsync()
    {
        User = await _authService.GetCurrentUserAsync();
        BiometricAvailable = await _biometric.IsAvailableAsync();
        BiometricUnlockEnabled = BiometricService.IsEnabled;
    }

    partial void OnBiometricUnlockEnabledChanged(bool value)
    {
        BiometricService.IsEnabled = value;
    }

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
