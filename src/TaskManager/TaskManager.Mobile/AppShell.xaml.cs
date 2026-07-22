using TaskManager.Mobile.Helpers;
using TaskManager.Mobile.Services;
using TaskManager.Mobile.Views;

namespace TaskManager.Mobile;

public partial class AppShell : Shell
{
    private readonly IAuthService _authService;
    private readonly INotificationRealtimeService _notifications;

    public AppShell(IAuthService authService, INotificationRealtimeService notifications)
    {
        _authService = authService;
        _notifications = notifications;
        InitializeComponent();
        Routing.RegisterRoute("taskdetail", typeof(TaskDetailPage));
        Routing.RegisterRoute("taskeditor", typeof(TaskEditorPage));
        Routing.RegisterRoute("projecteditor", typeof(ProjectEditorPage));
        Routing.RegisterRoute("usereditor", typeof(UserEditorPage));
        _notifications.Changed += OnNotificationsChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ConfigureTabsForRoleAsync();
        await _notifications.EnsureConnectedAsync();
        UpdateNotificationsTitle();
    }

    private async Task ConfigureTabsForRoleAsync()
    {
        var currentUser = await _authService.GetCurrentUserAsync();
        var canManage = AppRoles.IsOrgAdminOrManager(currentUser?.Role);

        ProjectsFlyoutItem.IsVisible = canManage;
        UsersFlyoutItem.IsVisible = canManage;
        TemplatesFlyoutItem.IsVisible = canManage;
    }

    private void OnNotificationsChanged() =>
        MainThread.BeginInvokeOnMainThread(UpdateNotificationsTitle);

    private void UpdateNotificationsTitle()
    {
        var count = _notifications.UnreadCount;
        NotificationsFlyoutItem.Title = count > 0 ? $"Notifications ({count})" : "Notifications";
    }
}
