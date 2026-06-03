using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.Mobile.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly IAppNavigationService _navigation;

    public LoginViewModel(IAuthService authService, IAppNavigationService navigation)
    {
        _authService = authService;
        _navigation = navigation;
        Title = "Sign in";
    }

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy)
            return;

        ClearError();

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            SetError("Enter username and password.");
            return;
        }

        try
        {
            IsBusy = true;
            var success = await _authService.LoginAsync(new LoginDto
            {
                Username = Username.Trim(),
                Password = Password
            });

            if (!success)
            {
                SetError("Invalid username or password.");
                return;
            }

            await _navigation.GoToMainAsync();
        }
        catch (Exception ex)
        {
            SetError($"Could not connect to the API. Check the server URL and network. ({ex.Message})");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
