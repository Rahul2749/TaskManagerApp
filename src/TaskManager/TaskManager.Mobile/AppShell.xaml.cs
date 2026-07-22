using TaskManager.Mobile.Helpers;
using TaskManager.Mobile.Services;
using TaskManager.Mobile.Views;

namespace TaskManager.Mobile;

public partial class AppShell : Shell
{
    private readonly IAuthService _authService;

    public AppShell(IAuthService authService)
    {
        _authService = authService;
        InitializeComponent();
        Routing.RegisterRoute("taskdetail", typeof(TaskDetailPage));
        Routing.RegisterRoute("taskeditor", typeof(TaskEditorPage));
        Routing.RegisterRoute("projecteditor", typeof(ProjectEditorPage));
        Routing.RegisterRoute("usereditor", typeof(UserEditorPage));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ConfigureTabsForRoleAsync();
    }

    private async Task ConfigureTabsForRoleAsync()
    {
        var currentUser = await _authService.GetCurrentUserAsync();
        var canManage = AppRoles.IsOrgAdminOrManager(currentUser?.Role);

        ProjectsFlyoutItem.IsVisible = canManage;
        UsersFlyoutItem.IsVisible = canManage;
        TemplatesFlyoutItem.IsVisible = canManage;
    }
}
