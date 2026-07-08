using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TaskManager.Mobile.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

public partial class ProjectsViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    public ProjectsViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Projects";
    }

    public ObservableCollection<ProjectDto> Projects { get; } = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
            return;

        try
        {
            if (!IsRefreshing) IsBusy = true;
            ClearError();

            var projects = await _apiService.GetProjectsAsync();
            Projects.Clear();

            if (projects != null)
            {
                foreach (var project in projects.OrderBy(p => p.Name))
                    Projects.Add(project);
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
    private async Task OpenProjectAsync(ProjectDto project)
    {
        if (project.Id == null) return;
        await Shell.Current.GoToAsync($"projecteditor?id={project.Id}");
    }

    [RelayCommand]
    private async Task CreateProjectAsync()
    {
        await Shell.Current.GoToAsync("projecteditor?id=0");
    }
}
