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
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ConfigureTabsForRoleAsync();
    }

    private async Task ConfigureTabsForRoleAsync()
    {
        var user = await _authService.GetCurrentUserAsync();
        var role = user?.Role ?? "User";
        var showProjects = role is "Admin" or "Manager";
        
        // Remove the projects tab if it's currently in the list
        var existingTab = MainTabBar.Items.FirstOrDefault(i => i.Route == "projects");
        if (existingTab != null)
        {
            MainTabBar.Items.Remove(existingTab);
        }

        // Dynamically add it back if authorized, avoiding the IsVisible Android MAUI bug
        if (showProjects)
        {
            var projectsTab = new ShellContent
            {
                Title = "Projects",
                ContentTemplate = new DataTemplate(typeof(Views.ProjectsPage)),
                Route = "projects"
            };
            
            // Insert it before the Profile tab
            MainTabBar.Items.Insert(2, projectsTab);
        }
    }
}
