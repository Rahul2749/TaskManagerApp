using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TaskManager.Mobile.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

public partial class UsersViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly IAuthService _authService;

    public UsersViewModel(IApiService apiService, IAuthService authService)
    {
        _apiService = apiService;
        _authService = authService;
        Title = "Users";
    }

    public ObservableCollection<UserDto> Users { get; } = new();

    [ObservableProperty]
    private UserDto? _selectedUser;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ClearError();
            
            var user = await _authService.GetCurrentUserAsync();
            if (user == null || (user.Role != "Admin" && user.Role != "Manager"))
            {
                SetError("You don't have permission to view users.");
                return;
            }

            var users = await _apiService.GetUsersAsync();
            Users.Clear();
            if (users != null)
            {
                foreach (var u in users)
                {
                    Users.Add(u);
                }
            }
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
    private async Task CreateUserAsync()
    {
        await Shell.Current.GoToAsync("usereditor");
    }

    [RelayCommand]
    private async Task GoToUserEditorAsync(UserDto? user)
    {
        if (user == null) return;
        await Shell.Current.GoToAsync($"usereditor?UserId={user.Id}");
        SelectedUser = null;
    }
}
