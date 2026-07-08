using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TaskManager.Mobile.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

public partial class DashboardViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly IAuthService _authService;

    public DashboardViewModel(IApiService apiService, IAuthService authService)
    {
        _apiService = apiService;
        _authService = authService;
        Title = "Dashboard";
    }

    [ObservableProperty]
    private string _welcomeText = string.Empty;

    [ObservableProperty]
    private int _totalTasks;

    [ObservableProperty]
    private int _completedTasks;

    [ObservableProperty]
    private int _inProgressTasks;

    [ObservableProperty]
    private int _overdueTasks;

    [ObservableProperty]
    private int _totalProjects;

    public ObservableCollection<TaskDto> RecentTasks { get; } = new();
    public ObservableCollection<ProjectTaskSummary> ProjectSummaries { get; } = new();
    public ObservableCollection<TaskDto> UpcomingDeadlines { get; } = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
            return;

        try
        {
            if (!IsRefreshing) IsBusy = true;
            ClearError();

            var user = await _authService.GetCurrentUserAsync();
            WelcomeText = user != null
                ? $"Hello, {user.FullName ?? user.Username}"
                : "Hello";

            var dashboard = await _apiService.GetDashboardDataAsync();
            if (dashboard == null)
            {
                SetError("Could not load dashboard.");
                return;
            }

            TotalTasks = dashboard.TotalTasks;
            CompletedTasks = dashboard.CompletedTasks;
            InProgressTasks = dashboard.InProgressTasks;
            OverdueTasks = dashboard.OverdueTasks;
            TotalProjects = dashboard.TotalProjects;

            RecentTasks.Clear();
            foreach (var task in dashboard.RecentTasks.Take(5))
                RecentTasks.Add(task);

            ProjectSummaries.Clear();
            if (dashboard.ProjectSummaries != null)
            {
                foreach (var summary in dashboard.ProjectSummaries)
                    ProjectSummaries.Add(summary);
            }

            UpcomingDeadlines.Clear();
            if (dashboard.UpcomingDeadlines != null)
            {
                foreach (var task in dashboard.UpcomingDeadlines.Take(5))
                    UpcomingDeadlines.Add(task);
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }
}
