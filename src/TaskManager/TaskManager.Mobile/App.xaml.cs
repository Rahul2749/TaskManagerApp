using TaskManager.Mobile.Services;
using TaskManager.Mobile.Views;

namespace TaskManager.Mobile;

public partial class App : Application
{
    private readonly IDeepLinkService _deepLinks;

    public App(IDeepLinkService deepLinks)
    {
        InitializeComponent();
        _deepLinks = deepLinks;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var services = MauiProgram.Services;

        var loadingPage = new ContentPage
        {
            Content = new ActivityIndicator
            {
                IsRunning = true,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center
            }
        };

        var window = new Window(loadingPage);

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var storage = services.GetRequiredService<ISecureTokenStorage>();
            bool hasSession = false;

            try
            {
                hasSession = await storage.HasSessionAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Session check failed: {ex.Message}");
            }

            if (!hasSession)
            {
                window.Page = new NavigationPage(services.GetRequiredService<LoginPage>());
                return;
            }

            var user = await storage.GetCurrentUserAsync();
            if (user?.NeedsOnboarding == true)
            {
                window.Page = new NavigationPage(services.GetRequiredService<OnboardingPage>());
                return;
            }

            if (BiometricService.IsEnabled)
            {
                window.Page = new NavigationPage(services.GetRequiredService<LockPage>());
                return;
            }

            window.Page = services.GetRequiredService<AppShell>();
            await _deepLinks.TryHandleAsync();
        });

        return window;
    }

    protected override void OnAppLinkRequestReceived(Uri uri)
    {
        base.OnAppLinkRequestReceived(uri);
        _ = HandleAppLinkAsync(uri);
    }

    public Task HandleAppLinkAsync(Uri uri)
    {
        _deepLinks.SetPending(uri);
        return _deepLinks.TryHandleAsync(uri);
    }
}
