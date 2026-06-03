using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.Mobile.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

[QueryProperty(nameof(TaskId), "id")]
public partial class TaskDetailViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    public TaskDetailViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Task details";
        StatusOptions = new[]
        {
            "NotAssigned", "Assigned", "InProgress", "Completed", "Tested", "Closed"
        };
    }

    public IReadOnlyList<string> StatusOptions { get; }

    [ObservableProperty]
    private int _taskId;

    [ObservableProperty]
    private string _taskTitle = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _projectName = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _priority = string.Empty;

    [ObservableProperty]
    private DateTime? _dueDate;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (TaskId <= 0 || IsBusy)
            return;

        try
        {
            IsBusy = true;
            ClearError();

            var task = await _apiService.GetTaskAsync(TaskId);
            if (task == null)
            {
                SetError("Task not found.");
                return;
            }

            TaskTitle = task.Title;
            Description = task.Description ?? string.Empty;
            ProjectName = task.ProjectName ?? string.Empty;
            Status = task.Status;
            Priority = task.Priority;
            DueDate = task.DueDate;
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
