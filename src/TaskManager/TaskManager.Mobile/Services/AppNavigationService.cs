using TaskManager.Mobile.Helpers;
using TaskManager.Mobile.Views;

namespace TaskManager.Mobile.Services;

public class AppNavigationService : IAppNavigationService
{
    private readonly IServiceProvider _services;

    public AppNavigationService(IServiceProvider services) => _services = services;

    public Task GoToLoginAsync()
    {
        KeyboardHelper.Hide();
        SetRootPage(WrapNavigation(_services.GetRequiredService<LoginPage>()));
        return Task.CompletedTask;
    }

    public Task GoToMainAsync()
    {
        KeyboardHelper.Hide();
        SetRootPage(_services.GetRequiredService<AppShell>());
        return Task.CompletedTask;
    }

    public Task GoToOnboardingAsync()
    {
        KeyboardHelper.Hide();
        SetRootPage(WrapNavigation(_services.GetRequiredService<OnboardingPage>()));
        return Task.CompletedTask;
    }

    public Task NavigateAfterAuthAsync(bool needsOnboarding) =>
        needsOnboarding ? GoToOnboardingAsync() : GoToMainAsync();

    private static NavigationPage WrapNavigation(Page root)
    {
        var nav = new NavigationPage(root);
        nav.Pushed += (_, _) => KeyboardHelper.Hide();
        nav.Popped += (_, _) => KeyboardHelper.Hide();
        return nav;
    }

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
