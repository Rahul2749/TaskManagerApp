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
        
        // Show a temporary loading page to avoid blocking the UI thread
        var loadingPage = new ContentPage 
        { 
            Content = new ActivityIndicator { IsRunning = true, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center } 
        };
        
        var window = new Window(loadingPage);

        // Perform the async SecureStorage check asynchronously
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

            Page targetPage = hasSession
                ? services.GetRequiredService<AppShell>()
                : new NavigationPage(services.GetRequiredService<LoginPage>());

            window.Page = targetPage;
        });

        return window;
    }
}
