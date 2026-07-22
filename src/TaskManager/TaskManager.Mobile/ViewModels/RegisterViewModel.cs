using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.Mobile.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

public partial class RegisterViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly IAppNavigationService _navigation;

    public RegisterViewModel(IAuthService authService, IAppNavigationService navigation)
    {
        _authService = authService;
        _navigation = navigation;
        Title = "Create workspace";
    }

    [ObservableProperty] private string _organizationName = string.Empty;
    [ObservableProperty] private string _firstName = string.Empty;
    [ObservableProperty] private string _lastName = string.Empty;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (IsBusy) return;
        ClearError();

        if (string.IsNullOrWhiteSpace(OrganizationName) ||
            string.IsNullOrWhiteSpace(Username) ||
            string.IsNullOrWhiteSpace(Email) ||
            string.IsNullOrWhiteSpace(Password) ||
            string.IsNullOrWhiteSpace(FirstName) ||
            string.IsNullOrWhiteSpace(LastName))
        {
            SetError("Please fill in all fields.");
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
            var result = await _authService.RegisterAsync(new WorkspaceRegistrationDto
            {
                OrganizationName = OrganizationName.Trim(),
                Username = Username.Trim(),
                Email = Email.Trim(),
                Password = Password,
                FirstName = FirstName.Trim(),
                LastName = LastName.Trim()
            });

            if (!result.Success)
            {
                SetError(result.Error ?? "Registration failed.");
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
