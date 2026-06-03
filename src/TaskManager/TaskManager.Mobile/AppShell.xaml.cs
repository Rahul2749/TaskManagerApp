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
        bool isAdminOrManager = currentUser?.Role == "Admin" || currentUser?.Role == "Manager";

        ProjectsFlyoutItem.IsVisible = isAdminOrManager;
        UsersFlyoutItem.IsVisible = isAdminOrManager;
    }
}
