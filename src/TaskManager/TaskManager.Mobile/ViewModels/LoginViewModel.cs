using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.Mobile.Configuration;
using TaskManager.Mobile.Services;
using TaskManager.Mobile.Views;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly IAppNavigationService _navigation;
    private readonly IDeepLinkService _deepLinks;

    public LoginViewModel(IAuthService authService, IAppNavigationService navigation, IDeepLinkService deepLinks)
    {
        _authService = authService;
        _navigation = navigation;
        _deepLinks = deepLinks;
        Title = "Sign in";
    }

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _ssoSlug = string.Empty;

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
            await _deepLinks.TryHandleAsync();
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
    private async Task StartSsoAsync()
    {
        if (IsBusy) return;
        ClearError();

        if (string.IsNullOrWhiteSpace(SsoSlug))
        {
            SetError("Enter your workspace slug for SSO.");
            return;
        }

        try
        {
            IsBusy = true;
            var slug = Uri.EscapeDataString(SsoSlug.Trim().ToLowerInvariant());
            var startUrl = new Uri(new Uri(ApiSettings.BaseUrl), $"api/sso/{slug}/start?client=mobile");
            var callbackUrl = new Uri("taskmanager://sso-callback");

            var authResult = await WebAuthenticator.Default.AuthenticateAsync(
                new WebAuthenticatorOptions
                {
                    Url = startUrl,
                    CallbackUrl = callbackUrl,
                    PrefersEphemeralWebBrowserSession = true
                });

            if (authResult.Properties.TryGetValue("error", out var ssoError) && !string.IsNullOrEmpty(ssoError))
            {
                SetError($"SSO failed: {ssoError}");
                return;
            }

            if (!authResult.Properties.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            {
                SetError("SSO did not return a login code.");
                return;
            }

            var result = await _authService.CompleteSsoLoginAsync(code);
            if (!result.Success)
            {
                SetError(result.Error ?? "SSO sign-in failed.");
                return;
            }

            var entitlements = MauiProgram.Services.GetRequiredService<IEntitlementService>();
            entitlements.Clear();
            await entitlements.EnsureLoadedAsync(forceReload: true);
            await _navigation.NavigateAfterAuthAsync(result.User?.NeedsOnboarding == true);
            await _deepLinks.TryHandleAsync();
        }
        catch (TaskCanceledException)
        {
            SetError("SSO sign-in was cancelled.");
        }
        catch (Exception ex)
        {
            SetError($"SSO could not start. ({ex.Message})");
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
