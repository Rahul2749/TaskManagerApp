using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TaskManager.Mobile.Helpers;
using TaskManager.Mobile.Services;
using TaskManager.Shared.Billing;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

[QueryProperty(nameof(TaskId), "id")]
public partial class TaskDetailViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly IAuthService _authService;
    private readonly IEntitlementService _entitlements;
    private readonly IDeepLinkService _deepLinks;
    private int _projectId;

    public TaskDetailViewModel(
        IApiService apiService,
        IAuthService authService,
        IEntitlementService entitlements,
        IDeepLinkService deepLinks)
    {
        _apiService = apiService;
        _authService = authService;
        _entitlements = entitlements;
        _deepLinks = deepLinks;
        Title = "Task details";
        StatusOptions = new[]
        {
            "NotAssigned", "Assigned", "InProgress", "Completed", "Tested", "Closed"
        };
        RecurrenceOptions = new[] { "none", "daily", "weekly", "monthly" };
    }

    public IReadOnlyList<string> StatusOptions { get; }
    public IReadOnlyList<string> RecurrenceOptions { get; }
    public ObservableCollection<TaskHistoryDto> History { get; } = new();
    public ObservableCollection<SubtaskDto> Subtasks { get; } = new();
    public ObservableCollection<CommentDto> Comments { get; } = new();
    public ObservableCollection<TimeEntryDto> TimeEntries { get; } = new();
    public ObservableCollection<TaskDependencyDto> Dependencies { get; } = new();
    public ObservableCollection<TaskDto> ProjectTasks { get; } = new();

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
    [ObservableProperty] private bool _canManageAdvanced;
    [ObservableProperty] private bool _hasTimeTracking;
    [ObservableProperty] private bool _hasGantt;
    [ObservableProperty] private string _successMessage = string.Empty;
    [ObservableProperty] private decimal _actualHours;
    [ObservableProperty] private string _logHoursText = "1";
    [ObservableProperty] private string? _logNotes;
    [ObservableProperty] private TaskDto? _selectedPredecessor;
    [ObservableProperty] private string _recurrenceFrequency = "none";
    [ObservableProperty] private string _recurrenceIntervalText = "1";
    [ObservableProperty] private string _nextOccurrenceLabel = string.Empty;

    partial void OnTaskIdChanged(int value)
    {
        if (value > 0)
            _ = LoadAsync();
    }

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
            CanManageAdvanced = AppRoles.CanManageProjects(user?.Role);

            await _entitlements.EnsureLoadedAsync();
            HasTimeTracking = _entitlements.HasFeature(FeatureKeys.TimeTracking);
            HasGantt = _entitlements.HasFeature(FeatureKeys.TimelineGantt);

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
            ActualHours = task.ActualHours;
            _projectId = task.ProjectId;
            RecurrenceFrequency = string.IsNullOrWhiteSpace(task.RecurrenceFrequency) ? "none" : task.RecurrenceFrequency;
            RecurrenceIntervalText = Math.Max(1, task.RecurrenceInterval).ToString();
            NextOccurrenceLabel = task.NextOccurrenceAt.HasValue
                ? $"Next: {task.NextOccurrenceAt.Value.ToLocalTime():g}"
                : string.Empty;

            History.Clear();
            if (task.History != null)
            {
                foreach (var h in task.History.OrderByDescending(x => x.ChangedAt))
                    History.Add(h);
            }

            await LoadSubtasksAsync();
            await LoadCommentsAsync();
            await LoadPhase6Async();
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
    private async Task LogTimeAsync()
    {
        if (!HasTimeTracking)
        {
            SetError("Time tracking requires Professional+.");
            return;
        }

        if (!decimal.TryParse(LogHoursText, out var hours) || hours < 0.25m)
        {
            SetError("Enter hours (minimum 0.25).");
            return;
        }

        try
        {
            ClearError();
            var (entry, error) = await _apiService.CreateTimeEntryAsync(new CreateTimeEntryDto
            {
                TaskId = TaskId,
                WorkDate = DateTime.UtcNow.Date,
                Hours = hours,
                Notes = LogNotes
            });
            if (entry is null)
            {
                SetError(error ?? "Could not log time.");
                return;
            }

            LogNotes = null;
            SuccessMessage = "Time logged.";
            await LoadPhase6Async();
            var task = await _apiService.GetTaskAsync(TaskId);
            if (task is not null)
                ActualHours = task.ActualHours;
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
    }

    [RelayCommand]
    private async Task DeleteTimeEntryAsync(TimeEntryDto? entry)
    {
        if (entry is null) return;
        if (await _apiService.DeleteTimeEntryAsync(entry.Id))
        {
            SuccessMessage = "Entry removed.";
            await LoadPhase6Async();
            var task = await _apiService.GetTaskAsync(TaskId);
            if (task is not null)
                ActualHours = task.ActualHours;
        }
    }

    [RelayCommand]
    private async Task AddDependencyAsync()
    {
        if (!HasGantt)
        {
            SetError("Dependencies require Professional+ timeline.");
            return;
        }

        if (SelectedPredecessor?.Id is not int pred)
        {
            SetError("Pick a predecessor task.");
            return;
        }

        try
        {
            ClearError();
            var (dep, error) = await _apiService.CreateDependencyAsync(new CreateTaskDependencyDto
            {
                PredecessorTaskId = pred,
                SuccessorTaskId = TaskId
            });
            if (dep is null)
            {
                SetError(error ?? "Could not add dependency.");
                return;
            }

            SelectedPredecessor = null;
            SuccessMessage = "Dependency added.";
            await LoadPhase6Async();
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
    }

    [RelayCommand]
    private async Task RemoveDependencyAsync(TaskDependencyDto? dep)
    {
        if (dep is null) return;
        if (await _apiService.DeleteDependencyAsync(dep.Id))
        {
            SuccessMessage = "Dependency removed.";
            await LoadPhase6Async();
        }
    }

    [RelayCommand]
    private async Task SaveRecurrenceAsync()
    {
        if (!CanManageAdvanced)
        {
            SetError("Only managers and admins can set recurrence.");
            return;
        }

        if (!int.TryParse(RecurrenceIntervalText, out var interval) || interval < 1)
            interval = 1;

        try
        {
            ClearError();
            var updated = await _apiService.SetTaskRecurrenceAsync(TaskId, new SetRecurrenceDto
            {
                Frequency = RecurrenceFrequency,
                Interval = interval
            });
            if (updated is null)
            {
                SetError("Could not save recurrence.");
                return;
            }

            SuccessMessage = "Recurrence saved.";
            RecurrenceFrequency = updated.RecurrenceFrequency;
            NextOccurrenceLabel = updated.NextOccurrenceAt.HasValue
                ? $"Next: {updated.NextOccurrenceAt.Value.ToLocalTime():g}"
                : string.Empty;
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
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
    private async Task ShareAsync()
    {
        if (TaskId <= 0) return;
        var url = _deepLinks.GetTaskShareUrl(TaskId);
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = TaskTitle,
            Text = $"{TaskTitle}\n{url}",
            Uri = url
        });
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

    private async Task LoadPhase6Async()
    {
        TimeEntries.Clear();
        Dependencies.Clear();
        ProjectTasks.Clear();

        if (HasTimeTracking)
        {
            var entries = await _apiService.GetTaskTimeEntriesAsync(TaskId) ?? [];
            foreach (var e in entries)
                TimeEntries.Add(e);
        }

        if (HasGantt)
        {
            var deps = await _apiService.GetDependenciesAsync(taskId: TaskId) ?? [];
            foreach (var d in deps)
                Dependencies.Add(d);

            if (CanManageAdvanced && _projectId > 0)
            {
                var tasks = await _apiService.GetTasksAsync(_projectId) ?? [];
                foreach (var t in tasks.Where(t => t.Id.HasValue && t.Id != TaskId))
                    ProjectTasks.Add(t);
            }
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
