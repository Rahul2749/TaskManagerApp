using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TaskManager.Mobile.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

public partial class TasksViewModel : BaseViewModel
{
    private const string AllFilter = "All";
    private readonly IApiService _apiService;
    private bool _suppressFilterReload;

    public TasksViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Tasks";
        StatusFilters = new[]
        {
            AllFilter, "NotAssigned", "Assigned", "InProgress", "Completed", "Tested", "Closed"
        };
        SelectedStatusFilter = AllFilter;
        ProjectFilters = new ObservableCollection<ProjectFilterOption>
        {
            new(null, AllFilter)
        };
        SelectedProjectFilter = ProjectFilters[0];
    }

    public IReadOnlyList<string> StatusFilters { get; }
    public ObservableCollection<ProjectFilterOption> ProjectFilters { get; }
    public ObservableCollection<TaskDto> Tasks { get; } = new();

    [ObservableProperty]
    private string _selectedStatusFilter = AllFilter;

    [ObservableProperty]
    private ProjectFilterOption? _selectedProjectFilter;

    partial void OnSelectedStatusFilterChanged(string value)
    {
        if (!_suppressFilterReload)
            _ = LoadAsync();
    }

    partial void OnSelectedProjectFilterChanged(ProjectFilterOption? value)
    {
        if (!_suppressFilterReload)
            _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
            return;

        try
        {
            if (!IsRefreshing) IsBusy = true;
            ClearError();

            await EnsureProjectFiltersAsync();

            int? projectId = SelectedProjectFilter?.Id;
            string? status = SelectedStatusFilter == AllFilter ? null : SelectedStatusFilter;

            var tasks = await _apiService.GetTasksAsync(projectId, status);
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
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        _suppressFilterReload = true;
        SelectedStatusFilter = AllFilter;
        if (ProjectFilters.Count > 0)
            SelectedProjectFilter = ProjectFilters[0];
        _suppressFilterReload = false;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task OpenTaskAsync(TaskDto task)
    {
        if (task.Id == null)
            return;

        await Shell.Current.GoToAsync($"taskdetail?id={task.Id}");
    }

    [RelayCommand]
    private async Task CreateTaskAsync()
    {
        await Shell.Current.GoToAsync("taskeditor?id=0");
    }

    private async Task EnsureProjectFiltersAsync()
    {
        if (ProjectFilters.Count > 1)
            return;

        try
        {
            var projects = await _apiService.GetProjectsAsync();
            _suppressFilterReload = true;
            var currentId = SelectedProjectFilter?.Id;
            ProjectFilters.Clear();
            ProjectFilters.Add(new ProjectFilterOption(null, AllFilter));
            if (projects != null)
            {
                foreach (var p in projects.OrderBy(p => p.Name))
                    ProjectFilters.Add(new ProjectFilterOption(p.Id, p.Name));
            }

            SelectedProjectFilter = ProjectFilters.FirstOrDefault(p => p.Id == currentId)
                                    ?? ProjectFilters[0];
            _suppressFilterReload = false;
        }
        catch
        {
            _suppressFilterReload = false;
        }
    }

    public sealed record ProjectFilterOption(int? Id, string Name)
    {
        public override string ToString() => Name;
    }
}
