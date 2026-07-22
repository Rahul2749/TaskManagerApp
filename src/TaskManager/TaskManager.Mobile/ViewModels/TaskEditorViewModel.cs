using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TaskManager.Mobile.Helpers;
using TaskManager.Mobile.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

[QueryProperty(nameof(TaskId), "id")]
public partial class TaskEditorViewModel : BaseViewModel
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
    private DateTime? _dueDate;

    [ObservableProperty]
    private ProjectDto? _selectedProject;

    [ObservableProperty]
    private UserDto? _selectedUser;

    [ObservableProperty]
    private bool _canAssignProjects = false;

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

            // Load options
            var projects = await _apiService.GetProjectsAsync();
            if (projects != null)
            {
                Projects.Clear();
                foreach (var p in projects) Projects.Add(p);
            }

            var users = await _apiService.GetUsersAsync();
            if (users != null)
            {
                Users.Clear();
                foreach (var u in users) Users.Add(u);
            }

            // Load existing task if editing
            if (TaskId > 0)
            {
                var task = await _apiService.GetTaskAsync(TaskId);
                if (task != null)
                {
                    TaskTitle = task.Title;
                    Description = task.Description ?? string.Empty;
                    Priority = task.Priority;
                    Status = task.Status;
                    DueDate = task.DueDate;
                    SelectedProject = Projects.FirstOrDefault(p => p.Id == task.ProjectId);
                    SelectedUser = Users.FirstOrDefault(u => u.Id == task.AssignedToId);
                }
            }
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

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(TaskTitle))
        {
            SetError("Title is required.");
            return;
        }

        if (SelectedProject == null)
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
                Title = TaskTitle,
                Description = Description,
                Priority = Priority,
                Status = Status,
                DueDate = DueDate,
                ProjectId = SelectedProject.Id ?? 0,
                AssignedToId = SelectedUser?.Id
            };

            TaskDto? result;
            if (TaskId > 0)
            {
                result = await _apiService.UpdateTaskAsync(TaskId, taskDto);
            }
            else
            {
                result = await _apiService.CreateTaskAsync(taskDto);
            }

            if (result == null)
            {
                SetError("Failed to save task.");
                return;
            }

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
}
