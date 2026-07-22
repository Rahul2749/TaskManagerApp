using TaskManager.Mobile.Views;

namespace TaskManager.Mobile.Services;

public class AppNavigationService : IAppNavigationService
{
    private readonly IServiceProvider _services;

    public AppNavigationService(IServiceProvider services) => _services = services;

    public Task GoToLoginAsync()
    {
        SetRootPage(new NavigationPage(_services.GetRequiredService<LoginPage>()));
        return Task.CompletedTask;
    }

    public Task GoToMainAsync()
    {
        SetRootPage(_services.GetRequiredService<AppShell>());
        return Task.CompletedTask;
    }

    public Task GoToOnboardingAsync()
    {
        SetRootPage(new NavigationPage(_services.GetRequiredService<OnboardingPage>()));
        return Task.CompletedTask;
    }

    public Task NavigateAfterAuthAsync(bool needsOnboarding) =>
        needsOnboarding ? GoToOnboardingAsync() : GoToMainAsync();

    private static void SetRootPage(Page page)
    {
        var app = Application.Current;
        if (app == null)
            return;

        if (app.Windows.Count > 0)
            app.Windows[0].Page = page;
#pragma warning disable CS0618
        else
            app.MainPage = page;
#pragma warning restore CS0618
    }
}
