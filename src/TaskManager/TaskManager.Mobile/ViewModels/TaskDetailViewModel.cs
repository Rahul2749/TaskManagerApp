using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TaskManager.Mobile.Helpers;
using TaskManager.Mobile.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

[QueryProperty(nameof(TaskId), "id")]
public partial class TaskDetailViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly IAuthService _authService;

    public TaskDetailViewModel(IApiService apiService, IAuthService authService)
    {
        _apiService = apiService;
        _authService = authService;
        Title = "Task details";
        StatusOptions = new[]
        {
            "NotAssigned", "Assigned", "InProgress", "Completed", "Tested", "Closed"
        };
    }

    public IReadOnlyList<string> StatusOptions { get; }
    public ObservableCollection<TaskHistoryDto> History { get; } = new();
    public ObservableCollection<SubtaskDto> Subtasks { get; } = new();
    public ObservableCollection<CommentDto> Comments { get; } = new();

    [ObservableProperty] private int _taskId;
    [ObservableProperty] private string _taskTitle = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _projectName = string.Empty;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private string _priority = string.Empty;
    [ObservableProperty] private DateTime? _dueDate;
    [ObservableProperty] private string _newComment = string.Empty;
    [ObservableProperty] private string _newSubtaskTitle = string.Empty;
    [ObservableProperty] private string _subtaskProgress = string.Empty;
    [ObservableProperty] private bool _canCreateSubtasks;
    [ObservableProperty] private string _successMessage = string.Empty;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy || TaskId <= 0) return;

        try
        {
            IsBusy = true;
            ClearError();
            SuccessMessage = string.Empty;

            var user = await _authService.GetCurrentUserAsync();
            CanCreateSubtasks = AppRoles.CanManageProjects(user?.Role);

            var task = await _apiService.GetTaskAsync(TaskId);
            if (task == null)
            {
                SetError("Task not found.");
                return;
            }

            TaskTitle = task.Title;
            ProjectName = task.ProjectName ?? "No Project";
            Description = task.Description ?? string.Empty;
            Priority = task.Priority;
            Status = task.Status;
            DueDate = task.DueDate;

            History.Clear();
            if (task.History != null)
            {
                foreach (var h in task.History.OrderByDescending(x => x.ChangedAt))
                    History.Add(h);
            }

            await LoadSubtasksAsync();
            await LoadCommentsAsync();
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
    private async Task UpdateStatusAsync()
    {
        if (TaskId <= 0 || string.IsNullOrEmpty(Status))
            return;

        try
        {
            IsBusy = true;
            ClearError();

            var updated = await _apiService.UpdateTaskStatusAsync(TaskId,
                new UpdateTaskStatusDto { Status = Status });

            if (updated == null)
            {
                SetError("Could not update task status.");
                return;
            }

            SuccessMessage = "Status updated.";
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }

        await LoadAsync();
    }

    [RelayCommand]
    private async Task AddCommentAsync()
    {
        if (TaskId <= 0 || string.IsNullOrWhiteSpace(NewComment))
        {
            SetError("Write a comment first. Tip: use @username to mention someone.");
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();
            var created = await _apiService.CreateCommentAsync(TaskId, NewComment.Trim());
            if (created is null)
            {
                SetError("Could not post comment.");
                return;
            }

            NewComment = string.Empty;
            SuccessMessage = "Comment posted.";
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }

        await LoadCommentsAsync();
    }

    [RelayCommand]
    private async Task ToggleSubtaskAsync(SubtaskDto? subtask)
    {
        if (subtask?.Id is not int || TaskId <= 0)
            return;

        try
        {
            ClearError();
            var payload = new SubtaskDto
            {
                Id = subtask.Id,
                TaskId = TaskId,
                Title = subtask.Title,
                IsCompleted = !subtask.IsCompleted,
                SortOrder = subtask.SortOrder,
                AssignedToId = subtask.AssignedToId,
                DueDate = subtask.DueDate
            };

            var updated = await _apiService.UpdateSubtaskAsync(TaskId, payload);
            if (updated is null)
            {
                SetError("Could not update checklist item.");
                return;
            }

            var index = Subtasks.ToList().FindIndex(s => s.Id == updated.Id);
            if (index >= 0)
                Subtasks[index] = updated;
            UpdateSubtaskProgress();
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
    }

    [RelayCommand]
    private async Task AddSubtaskAsync()
    {
        if (!CanCreateSubtasks)
        {
            SetError("Only managers and admins can add checklist items.");
            return;
        }

        if (TaskId <= 0 || string.IsNullOrWhiteSpace(NewSubtaskTitle))
        {
            SetError("Enter a checklist item title.");
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();
            var created = await _apiService.CreateSubtaskAsync(TaskId, NewSubtaskTitle.Trim());
            if (created is null)
            {
                SetError("Could not add checklist item.");
                return;
            }

            NewSubtaskTitle = string.Empty;
            SuccessMessage = "Checklist item added.";
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }

        await LoadSubtasksAsync();
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        await Shell.Current.GoToAsync($"taskeditor?id={TaskId}");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (TaskId <= 0) return;

        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Delete Task", "Are you sure you want to delete this task?", "Yes", "No");
        if (!confirm) return;

        try
        {
            IsBusy = true;
            bool success = await _apiService.DeleteTaskAsync(TaskId);
            if (success)
                await Shell.Current.GoToAsync("..");
            else
                SetError("Failed to delete task.");
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

    private async Task LoadSubtasksAsync()
    {
        Subtasks.Clear();
        var items = await _apiService.GetSubtasksAsync(TaskId);
        if (items == null) return;
        foreach (var s in items.OrderBy(x => x.SortOrder))
            Subtasks.Add(s);
        UpdateSubtaskProgress();
    }

    private async Task LoadCommentsAsync()
    {
        Comments.Clear();
        var items = await _apiService.GetCommentsAsync(TaskId);
        if (items == null) return;
        foreach (var c in items.OrderBy(x => x.CreatedAt))
            Comments.Add(c);
    }

    private void UpdateSubtaskProgress()
    {
        if (Subtasks.Count == 0)
        {
            SubtaskProgress = string.Empty;
            return;
        }

        var done = Subtasks.Count(s => s.IsCompleted);
        SubtaskProgress = $"{done}/{Subtasks.Count} done";
    }
}
