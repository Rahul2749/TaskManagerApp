using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.Mobile.Helpers;
using TaskManager.Mobile.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

[QueryProperty(nameof(UserId), nameof(UserId))]
public partial class UserEditorViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly IAuthService _authService;

    public UserEditorViewModel(IApiService apiService, IAuthService authService)
    {
        _apiService = apiService;
        _authService = authService;
        Roles = new ObservableRoles();
    }

    [ObservableProperty]
    private int _userId;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _firstName = string.Empty;

    [ObservableProperty]
    private string _lastName = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _selectedRole = AppRoles.User;

    [ObservableProperty]
    private bool _isEditing;

    public ObservableRoles Roles { get; }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ClearError();

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null || !AppRoles.CanManageUsers(currentUser.Role))
            {
                SetError("You do not have permission to manage users.");
                return;
            }

            Roles.Replace(AppRoles.AssignableRoles(currentUser.Role));
            if (Roles.Count > 0 && !Roles.Contains(SelectedRole))
                SelectedRole = Roles[0];

            if (UserId > 0)
            {
                Title = "Edit User";
                IsEditing = true;

                var user = await _apiService.GetUserAsync(UserId);
                if (user != null)
                {
                    Username = user.Username;
                    Email = user.Email;
                    FirstName = user.FirstName;
                    LastName = user.LastName;
                    SelectedRole = user.Role;
                    if (!Roles.Contains(SelectedRole))
                        Roles.Add(SelectedRole);
                }
                else
                {
                    SetError("User not found.");
                }
            }
            else
            {
                Title = "New User";
                IsEditing = false;
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
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Email) ||
            string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName))
        {
            SetError("Please fill in all required fields.");
            return;
        }

        if (!IsEditing && string.IsNullOrWhiteSpace(Password))
        {
            SetError("Password is required for new users.");
            return;
        }

        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ClearError();

            var registerDto = new RegisterDto
            {
                Username = Username,
                Email = Email,
                Password = Password ?? string.Empty,
                FirstName = FirstName,
                LastName = LastName,
                Role = SelectedRole
            };

            if (IsEditing)
                await _apiService.UpdateUserAsync(UserId, registerDto);
            else
                await _apiService.CreateUserAsync(registerDto);

            await Shell.Current.GoToAsync("..");
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
    private async Task DeleteAsync()
    {
        if (UserId <= 0) return;

        bool confirm = await Shell.Current.DisplayAlert(
            "Deactivate User",
            "Are you sure you want to deactivate this user?",
            "Yes",
            "No");
        if (!confirm) return;

        try
        {
            IsBusy = true;
            bool success = await _apiService.DeleteUserAsync(UserId);
            if (success)
                await Shell.Current.GoToAsync("..");
            else
                SetError("Failed to deactivate user.");
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

    /// <summary>Mutable list for Picker ItemsSource when assignable roles change.</summary>
    public sealed class ObservableRoles : System.Collections.ObjectModel.ObservableCollection<string>
    {
        public void Replace(IEnumerable<string> items)
        {
            Clear();
            foreach (var item in items)
                Add(item);
        }
    }
}
