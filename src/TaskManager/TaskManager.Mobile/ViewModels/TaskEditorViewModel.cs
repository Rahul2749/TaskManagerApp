using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TaskManager.Mobile.Helpers;
using TaskManager.Mobile.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

[QueryProperty(nameof(TaskId), "id")]
public partial class TaskEditorViewModel : DirtyFormViewModel
{
    private readonly IApiService _apiService;
    private readonly IAuthService _authService;

    public TaskEditorViewModel(IApiService apiService, IAuthService authService)
    {
        _apiService = apiService;
        _authService = authService;

        PriorityOptions = new[] { "Low", "Medium", "High" };
        StatusOptions = new[] { "NotAssigned", "Assigned", "InProgress", "Completed", "Tested", "Closed" };
    }

    public IReadOnlyList<string> PriorityOptions { get; }
    public IReadOnlyList<string> StatusOptions { get; }

    public ObservableCollection<ProjectDto> Projects { get; } = new();
    public ObservableCollection<UserDto> Users { get; } = new();

    private int _taskId;
    public int TaskId
    {
        get => _taskId;
        set
        {
            SetProperty(ref _taskId, value);
            Title = _taskId == 0 ? "Create Task" : "Edit Task";
        }
    }

    [ObservableProperty]
    private string _taskTitle = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _priority = "Medium";

    [ObservableProperty]
    private string _status = "NotAssigned";

    [ObservableProperty]
    private DateTime? _dueDate = DateTime.Today;

    [ObservableProperty]
    private ProjectDto? _selectedProject;

    [ObservableProperty]
    private UserDto? _selectedUser;

    [ObservableProperty]
    private bool _canAssignProjects = false;

    [ObservableProperty]
    private string _assigneeHint = string.Empty;

    partial void OnSelectedProjectChanged(ProjectDto? value) =>
        _ = ReloadAssigneesForProjectAsync(value?.Id);

    partial void OnSelectedUserChanged(UserDto? value)
    {
        if (value is not null && Status == "NotAssigned")
            Status = "Assigned";
        else if (value is null && Status == "Assigned")
            Status = "NotAssigned";
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ClearError();

            var currentUser = await _authService.GetCurrentUserAsync();
            CanAssignProjects = AppRoles.CanManageProjects(currentUser?.Role);

            var projects = await _apiService.GetProjectsAsync();
            if (projects != null)
            {
                Projects.Clear();
                foreach (var p in projects) Projects.Add(p);
            }

            if (TaskId > 0)
            {
                var task = await _apiService.GetTaskAsync(TaskId);
                if (task != null)
                {
                    TaskTitle = task.Title;
                    Description = task.Description ?? string.Empty;
                    Priority = task.Priority;
                    Status = task.Status;
                    DueDate = task.DueDate?.Date ?? DateTime.Today;
                    SelectedProject = Projects.FirstOrDefault(p => p.Id == task.ProjectId);
                    await ReloadAssigneesForProjectAsync(task.ProjectId, preserveUserId: task.AssignedToId);
                }
            }
            else
            {
                await ReloadAssigneesForProjectAsync(SelectedProject?.Id);
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
            MarkClean();
        }
    }

    private async Task ReloadAssigneesForProjectAsync(int? projectId, int? preserveUserId = null)
    {
        Users.Clear();
        AssigneeHint = string.Empty;

        if (!projectId.HasValue || projectId.Value <= 0)
        {
            SelectedUser = null;
            AssigneeHint = "Select a project first to choose an assignee.";
            return;
        }

        try
        {
            // Prefer people already on the project; fall back to org users so managers can assign.
            var projectUsers = await _apiService.GetProjectUsersAsync(projectId.Value) ?? [];
            var orgUsers = await _apiService.GetUsersAsync("User") ?? [];

            var byId = new Dictionary<int, UserDto>();
            foreach (var u in projectUsers)
                byId[u.Id] = u;
            foreach (var u in orgUsers)
                byId.TryAdd(u.Id, u);

            foreach (var u in byId.Values.OrderBy(u => u.Username))
                Users.Add(u);

            if (Users.Count == 0)
            {
                AssigneeHint = "No users available. Invite teammates, then assign them here.";
                SelectedUser = null;
                return;
            }

            AssigneeHint = projectUsers.Count == 0
                ? "Assignee will be added to this project when you save."
                : "Showing project members and other users in your workspace.";

            var keepId = preserveUserId ?? SelectedUser?.Id;
            SelectedUser = keepId.HasValue
                ? Users.FirstOrDefault(u => u.Id == keepId.Value)
                : null;
        }
        catch (Exception ex)
        {
            AssigneeHint = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(TaskTitle))
        {
            SetError("Title is required.");
            return;
        }

        if (SelectedProject?.Id is null or <= 0)
        {
            SetError("Project is required.");
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();

            var taskDto = new TaskDto
            {
                Id = TaskId > 0 ? TaskId : null,
                Title = TaskTitle.Trim(),
                Description = Description,
                Priority = Priority,
                Status = SelectedUser is null ? "NotAssigned" : (Status == "NotAssigned" ? "Assigned" : Status),
                DueDate = DueDate.HasValue
                    ? DateTime.SpecifyKind(DueDate.Value.Date, DateTimeKind.Utc)
                    : null,
                ProjectId = SelectedProject.Id.Value,
                AssignedToId = SelectedUser?.Id
            };

            TaskDto? result;
            if (TaskId > 0)
                result = await _apiService.UpdateTaskAsync(TaskId, taskDto);
            else
                result = await _apiService.CreateTaskAsync(taskDto);

            if (result == null)
            {
                SetError("Failed to save task.");
                return;
            }

            AllowLeaveWithoutPrompt();
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected override string BuildSnapshot() =>
        string.Join('\u001f',
            TaskTitle?.Trim() ?? string.Empty,
            Description?.Trim() ?? string.Empty,
            Priority ?? string.Empty,
            Status ?? string.Empty,
            SnapshotDate(DueDate),
            SelectedProject?.Id?.ToString() ?? string.Empty,
            SelectedUser?.Id.ToString() ?? string.Empty);
}
