using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TaskManager.Mobile.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

public partial class TasksViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    public TasksViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Tasks";
    }

    public ObservableCollection<TaskDto> Tasks { get; } = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            ClearError();

            var tasks = await _apiService.GetTasksAsync();
            Tasks.Clear();

            if (tasks != null)
            {
                foreach (var task in tasks.OrderByDescending(t => t.DueDate))
                    Tasks.Add(task);
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
    private async Task OpenTaskAsync(TaskDto task)
    {
        if (task.Id == null)
            return;

        await Shell.Current.GoToAsync($"taskdetail?id={task.Id}");
    }
}
