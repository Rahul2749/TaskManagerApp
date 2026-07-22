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

    public ProfileViewModel(
        IAuthService authService,
        IAppNavigationService navigation,
        IEntitlementService entitlements)
    {
        _authService = authService;
        _navigation = navigation;
        _entitlements = entitlements;
        Title = "Profile";
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RoleDisplay))]
    private UserDto? _user;

    public string RoleDisplay => AppRoles.DisplayName(User?.Role);

    [RelayCommand]
    private async Task LoadAsync()
    {
        User = await _authService.GetCurrentUserAsync();
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authService.LogoutAsync();
        _entitlements.Clear();
        await _navigation.GoToLoginAsync();
    }
}
