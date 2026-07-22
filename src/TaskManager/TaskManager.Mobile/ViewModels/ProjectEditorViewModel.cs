using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TaskManager.Mobile.Helpers;
using TaskManager.Mobile.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

[QueryProperty(nameof(ProjectId), "id")]
public partial class ProjectEditorViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly IAuthService _authService;

    public ProjectEditorViewModel(IApiService apiService, IAuthService authService)
    {
        _apiService = apiService;
        _authService = authService;
        
        StatusOptions = new[] { "Active", "OnHold", "Completed", "Cancelled" };
    }

    public IReadOnlyList<string> StatusOptions { get; }

    public ObservableCollection<UserDto> Managers { get; } = new();

    private int _projectId;
    public int ProjectId
    {
        get => _projectId;
        set
        {
            SetProperty(ref _projectId, value);
            Title = _projectId == 0 ? "Create Project" : "Edit Project";
        }
    }

    [ObservableProperty]
    private string _projectName = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _status = "Active";

    [ObservableProperty]
    private DateTime? _startDate = DateTime.Today;

    [ObservableProperty]
    private DateTime? _endDate = DateTime.Today.AddMonths(1);

    [ObservableProperty]
    private UserDto? _selectedManager;

    [ObservableProperty]
    private bool _canAssignManager = false;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ClearError();

            var currentUser = await _authService.GetCurrentUserAsync();
            CanAssignManager = AppRoles.IsOrgAdmin(currentUser?.Role);

            if (CanAssignManager)
            {
                var users = await _apiService.GetUsersAsync("Manager");
                if (users != null)
                {
                    Managers.Clear();
                    foreach (var u in users) Managers.Add(u);
                }
            }

            if (ProjectId > 0)
            {
                var project = await _apiService.GetProjectAsync(ProjectId);
                if (project != null)
                {
                    ProjectName = project.Name;
                    Description = project.Description ?? string.Empty;
                    Status = project.Status;
                    StartDate = project.StartDate;
                    EndDate = project.EndDate;
                    SelectedManager = Managers.FirstOrDefault(m => m.Id == project.ManagerId);
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

        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            SetError("Project name is required.");
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();

            var projectDto = new ProjectDto
            {
                Id = ProjectId > 0 ? ProjectId : null,
                Name = ProjectName,
                Description = Description,
                Status = Status,
                StartDate = StartDate,
                EndDate = EndDate,
                ManagerId = SelectedManager?.Id
            };

            ProjectDto? result;
            if (ProjectId > 0)
            {
                result = await _apiService.UpdateProjectAsync(ProjectId, projectDto);
            }
            else
            {
                result = await _apiService.CreateProjectAsync(projectDto);
            }

            if (result == null)
            {
                SetError("Failed to save project.");
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

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (ProjectId <= 0) return;

        bool confirm = await Shell.Current.DisplayAlert("Delete Project", "Are you sure you want to delete this project? All associated tasks will also be deleted.", "Yes", "No");
        if (!confirm) return;

        try
        {
            IsBusy = true;
            bool success = await _apiService.DeleteProjectAsync(ProjectId);
            if (success)
            {
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                SetError("Failed to delete project.");
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
}
