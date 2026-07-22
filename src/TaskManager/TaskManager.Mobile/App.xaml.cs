using TaskManager.Mobile.Services;
using TaskManager.Mobile.Views;

namespace TaskManager.Mobile;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
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
            window.Page = user?.NeedsOnboarding == true
                ? new NavigationPage(services.GetRequiredService<OnboardingPage>())
                : services.GetRequiredService<AppShell>();
        });

        return window;
    }
}
