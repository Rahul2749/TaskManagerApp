using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TaskManager.Mobile.Helpers;
using TaskManager.Mobile.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

public partial class ReportsViewModel : BaseViewModel
{
    private readonly IApiService _api;
    private readonly IAuthService _auth;

    public ReportsViewModel(IApiService api, IAuthService auth)
    {
        _api = api;
        _auth = auth;
        Title = "Reports";
    }

    [ObservableProperty] private bool _canView;
    [ObservableProperty] private int _totalTasks;
    [ObservableProperty] private int _completedTasks;
    [ObservableProperty] private int _inProgressTasks;
    [ObservableProperty] private int _overdueTasks;
    [ObservableProperty] private double _completionRate;
    [ObservableProperty] private decimal _hoursThisWeek;
    [ObservableProperty] private string _summarySubtitle = "Workspace analytics";

    public ObservableCollection<NamedCountDto> StatusBreakdown { get; } = new();
    public ObservableCollection<NamedCountDto> PriorityBreakdown { get; } = new();
    public ObservableCollection<ProjectTaskSummary> ProjectSummaries { get; } = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            if (!IsRefreshing) IsBusy = true;
            ClearError();

            var user = await _auth.GetCurrentUserAsync();
            CanView = AppRoles.IsOrgAdminOrManager(user?.Role);
            if (!CanView)
            {
                SetError("Reports are available to managers and admins.");
                return;
            }

            var data = await _api.GetWorkspaceAnalyticsAsync();
            if (data is null)
            {
                SetError("Could not load analytics.");
                return;
            }

            TotalTasks = data.TotalTasks;
            CompletedTasks = data.CompletedTasks;
            InProgressTasks = data.InProgressTasks;
            OverdueTasks = data.OverdueTasks;
            CompletionRate = data.CompletionRate;
            HoursThisWeek = data.HoursLoggedThisWeek;
            SummarySubtitle = $"{data.ActiveProjects} active projects · {data.CompletionRate:0.#}% complete";

            StatusBreakdown.Clear();
            foreach (var row in data.StatusBreakdown)
                StatusBreakdown.Add(row);

            PriorityBreakdown.Clear();
            foreach (var row in data.PriorityBreakdown)
                PriorityBreakdown.Add(row);

            ProjectSummaries.Clear();
            foreach (var row in data.ProjectSummaries.OrderByDescending(p => p.CompletionPercentage))
                ProjectSummaries.Add(row);
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
