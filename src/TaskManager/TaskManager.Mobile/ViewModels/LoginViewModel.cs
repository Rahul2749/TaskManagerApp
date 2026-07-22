using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.Mobile.Services;
using TaskManager.Mobile.Views;
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
            var result = await _authService.LoginAsync(new LoginDto
            {
                Username = Username.Trim(),
                Password = Password
            });

            if (!result.Success)
            {
                SetError(result.Error ?? "Invalid username or password.");
                return;
            }

            var entitlements = MauiProgram.Services.GetRequiredService<IEntitlementService>();
            entitlements.Clear();
            await entitlements.EnsureLoadedAsync(forceReload: true);

            await _navigation.NavigateAfterAuthAsync(result.User?.NeedsOnboarding == true);
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

    [RelayCommand]
    private Task GoToRegisterAsync() =>
        PushAsync(MauiProgram.Services.GetRequiredService<RegisterPage>());

    [RelayCommand]
    private Task GoToForgotPasswordAsync() =>
        PushAsync(MauiProgram.Services.GetRequiredService<ForgotPasswordPage>());

    [RelayCommand]
    private Task GoToAcceptInviteAsync() =>
        PushAsync(MauiProgram.Services.GetRequiredService<AcceptInvitePage>());

    [RelayCommand]
    private Task GoToResetPasswordAsync() =>
        PushAsync(MauiProgram.Services.GetRequiredService<ResetPasswordPage>());

    private static async Task PushAsync(Page page)
    {
        if (Application.Current?.Windows.FirstOrDefault()?.Page is NavigationPage nav)
            await nav.PushAsync(page);
#pragma warning disable CS0618
        else if (Application.Current?.MainPage is NavigationPage legacy)
            await legacy.PushAsync(page);
#pragma warning restore CS0618
    }
}
