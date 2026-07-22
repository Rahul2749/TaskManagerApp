using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TaskManager.Mobile.Services;
using TaskManager.Shared.Billing;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

public partial class BoardViewModel : BaseViewModel
{
    private static readonly string[] ColumnStatuses =
    {
        "NotAssigned", "Assigned", "InProgress", "Completed", "Tested", "Closed"
    };

    private readonly IApiService _api;
    private readonly IEntitlementService _entitlements;

    public BoardViewModel(IApiService api, IEntitlementService entitlements)
    {
        _api = api;
        _entitlements = entitlements;
        Title = "Board";
        foreach (var status in ColumnStatuses)
            Columns.Add(new BoardColumn(status));
    }

    public ObservableCollection<BoardColumn> Columns { get; } = new();
    public ObservableCollection<ProjectDto> Projects { get; } = new();

    [ObservableProperty] private bool _hasAccess;
    [ObservableProperty] private string _upgradeMessage = string.Empty;
    [ObservableProperty] private ProjectDto? _selectedProject;
    [ObservableProperty] private string _successMessage = string.Empty;

    partial void OnSelectedProjectChanged(ProjectDto? value)
    {
        if (HasAccess)
            _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ClearError();
            SuccessMessage = string.Empty;

            await _entitlements.EnsureLoadedAsync();
            HasAccess = _entitlements.HasFeature(FeatureKeys.BoardView);
            if (!HasAccess)
            {
                UpgradeMessage = "Kanban board requires a plan with board_view. Upgrade on the web Billing page.";
                return;
            }

            UpgradeMessage = string.Empty;

            if (Projects.Count == 0)
            {
                var projects = await _api.GetProjectsAsync() ?? [];
                Projects.Clear();
                Projects.Add(new ProjectDto { Id = null, Name = "All projects" });
                foreach (var p in projects.OrderBy(p => p.Name))
                    Projects.Add(p);
                SelectedProject ??= Projects[0];
            }

            var projectId = SelectedProject?.Id;
            var tasks = await _api.GetTasksAsync(projectId) ?? [];

            foreach (var col in Columns)
                col.Tasks.Clear();

            foreach (var task in tasks)
            {
                var col = Columns.FirstOrDefault(c =>
                    string.Equals(c.Status, task.Status, StringComparison.OrdinalIgnoreCase));
                (col ?? Columns[0]).Tasks.Add(task);
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

    [RelayCommand]
    private async Task AdvanceAsync(TaskDto? task)
    {
        if (task?.Id is not int id)
            return;

        var index = Array.FindIndex(ColumnStatuses,
            s => string.Equals(s, task.Status, StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index >= ColumnStatuses.Length - 1)
            return;

        await MoveToStatusAsync(task, ColumnStatuses[index + 1]);
    }

    [RelayCommand]
    private async Task RetreatAsync(TaskDto? task)
    {
        if (task?.Id is not int id)
            return;

        var index = Array.FindIndex(ColumnStatuses,
            s => string.Equals(s, task.Status, StringComparison.OrdinalIgnoreCase));
        if (index <= 0)
            return;

        await MoveToStatusAsync(task, ColumnStatuses[index - 1]);
    }

    [RelayCommand]
    private async Task OpenTaskAsync(TaskDto? task)
    {
        if (task?.Id is not int id)
            return;
        await Shell.Current.GoToAsync($"taskdetail?id={id}");
    }

    private async Task MoveToStatusAsync(TaskDto task, string newStatus)
    {
        try
        {
            ClearError();
            var updated = await _api.UpdateTaskStatusAsync(task.Id!.Value, new UpdateTaskStatusDto
            {
                Status = newStatus,
                Comment = "Moved on mobile board"
            });

            if (updated is null)
            {
                SetError("Could not move task.");
                return;
            }

            SuccessMessage = $"Moved to {newStatus}.";
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
            return;
        }

        await LoadAsync();
    }
}

public sealed class BoardColumn
{
    public BoardColumn(string status)
    {
        Status = status;
        Title = status switch
        {
            "NotAssigned" => "Not assigned",
            "InProgress" => "In progress",
            _ => status
        };
    }

    public string Status { get; }
    public string Title { get; }
    public ObservableCollection<TaskDto> Tasks { get; } = new();
}
