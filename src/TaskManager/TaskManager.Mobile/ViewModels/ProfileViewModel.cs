using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.Mobile.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

public partial class ProfileViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly IAppNavigationService _navigation;

    public ProfileViewModel(IAuthService authService, IAppNavigationService navigation)
    {
        _authService = authService;
        _navigation = navigation;
        Title = "Profile";
    }

    [ObservableProperty]
    private UserDto? _user;

    [RelayCommand]
    private async Task LoadAsync()
    {
        User = await _authService.GetCurrentUserAsync();
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authService.LogoutAsync();
        await _navigation.GoToLoginAsync();
    }
}
