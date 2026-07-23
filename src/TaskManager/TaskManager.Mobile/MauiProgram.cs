using Microsoft.Extensions.Logging;
using TaskManager.Mobile.Configuration;
using TaskManager.Mobile.Services;
using TaskManager.Mobile.ViewModels;
using TaskManager.Mobile.Views;

namespace TaskManager.Mobile;

public static class MauiProgram
{
    public static IServiceProvider Services { get; private set; } = null!;

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        RegisterServices(builder.Services);
        builder
            .UseMauiApp(sp => new App(sp.GetRequiredService<IDeepLinkService>()))
            .ConfigureFonts(_ => { });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        Services = app.Services;
        return app;
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<ISecureTokenStorage, SecureTokenStorage>();
        services.AddSingleton<IAppNavigationService, AppNavigationService>();
        services.AddSingleton<IEntitlementService, EntitlementService>();
        services.AddSingleton<INotificationRealtimeService, NotificationRealtimeService>();
        services.AddSingleton<IDeepLinkService, DeepLinkService>();
        services.AddSingleton<IBiometricService, BiometricService>();

        services.AddHttpClient("TaskManagerAuth", client =>
        {
            client.BaseAddress = new Uri(ApiSettings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddTransient<AuthenticatedHttpMessageHandler>();
        services.AddHttpClient("TaskManagerApi", client =>
        {
            client.BaseAddress = new Uri(ApiSettings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(60);
        }).AddHttpMessageHandler<AuthenticatedHttpMessageHandler>();

        services.AddSingleton<IAuthService>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new AuthService(factory.CreateClient("TaskManagerAuth"), sp.GetRequiredService<ISecureTokenStorage>());
        });

        services.AddSingleton<IApiService>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new ApiService(factory.CreateClient("TaskManagerApi"));
        });

        services.AddTransient<LoginViewModel>();
        services.AddTransient<LockViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddTransient<ForgotPasswordViewModel>();
        services.AddTransient<ResetPasswordViewModel>();
        services.AddTransient<AcceptInviteViewModel>();
        services.AddTransient<OnboardingViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<TasksViewModel>();
        services.AddTransient<BoardViewModel>();
        services.AddTransient<CalendarViewModel>();
        services.AddTransient<TimelineViewModel>();
        services.AddTransient<TimesheetsViewModel>();
        services.AddTransient<AutomationsViewModel>();
        services.AddTransient<TemplatesViewModel>();
        services.AddTransient<NotificationsViewModel>();
        services.AddTransient<ActivityViewModel>();
        services.AddTransient<BillingViewModel>();
        services.AddTransient<TaskDetailViewModel>();
        services.AddTransient<ProjectsViewModel>();
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<TaskEditorViewModel>();
        services.AddTransient<ProjectEditorViewModel>();
        services.AddTransient<UsersViewModel>();
        services.AddTransient<UserEditorViewModel>();

        services.AddTransient<LoginPage>();
        services.AddTransient<LockPage>();
        services.AddTransient<RegisterPage>();
        services.AddTransient<ForgotPasswordPage>();
        services.AddTransient<ResetPasswordPage>();
        services.AddTransient<AcceptInvitePage>();
        services.AddTransient<OnboardingPage>();
        services.AddTransient<DashboardPage>();
        services.AddTransient<ReportsPage>();
        services.AddTransient<TasksPage>();
        services.AddTransient<BoardPage>();
        services.AddTransient<CalendarPage>();
        services.AddTransient<TimelinePage>();
        services.AddTransient<TimesheetsPage>();
        services.AddTransient<AutomationsPage>();
        services.AddTransient<TemplatesPage>();
        services.AddTransient<NotificationsPage>();
        services.AddTransient<ActivityPage>();
        services.AddTransient<BillingPage>();
        services.AddTransient<TaskDetailPage>();
        services.AddTransient<TaskEditorPage>();
        services.AddTransient<ProjectsPage>();
        services.AddTransient<ProjectEditorPage>();
        services.AddTransient<ProfilePage>();
        services.AddTransient<UsersPage>();
        services.AddTransient<UserEditorPage>();

        services.AddSingleton<AppShell>();
    }
}
