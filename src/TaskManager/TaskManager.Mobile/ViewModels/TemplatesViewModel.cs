using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TaskManager.Mobile.Helpers;
using TaskManager.Mobile.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

public partial class TemplatesViewModel : BaseViewModel
{
    private readonly IApiService _api;
    private readonly IAuthService _auth;

    public TemplatesViewModel(IApiService api, IAuthService auth)
    {
        _api = api;
        _auth = auth;
        Title = "Templates";
    }

    public ObservableCollection<TaskTemplateDto> Templates { get; } = new();
    public ObservableCollection<ProjectDto> Projects { get; } = new();

    [ObservableProperty] private bool _canManage;
    [ObservableProperty] private TaskTemplateDto? _selectedTemplate;
    [ObservableProperty] private ProjectDto? _selectedProject;
    [ObservableProperty] private string _successMessage = string.Empty;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ClearError();
            SuccessMessage = string.Empty;

            var user = await _auth.GetCurrentUserAsync();
            CanManage = AppRoles.CanManageProjects(user?.Role);
            if (!CanManage)
            {
                SetError("Templates are available to managers and admins.");
                return;
            }

            var templates = await _api.GetTaskTemplatesAsync() ?? [];
            Templates.Clear();
            foreach (var t in templates.OrderBy(t => t.Name))
                Templates.Add(t);

            var projects = await _api.GetProjectsAsync() ?? [];
            Projects.Clear();
            foreach (var p in projects.Where(p => p.Id.HasValue).OrderBy(p => p.Name))
                Projects.Add(p);

            SelectedProject ??= Projects.FirstOrDefault();
            SelectedTemplate ??= Templates.FirstOrDefault();
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
    private async Task ApplyAsync()
    {
        if (SelectedTemplate is null || SelectedProject?.Id is not int projectId)
        {
            SetError("Select a template and a project.");
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();
            var created = await _api.ApplyTaskTemplateAsync(SelectedTemplate.Id, new ApplyTaskTemplateDto
            {
                ProjectId = projectId
            });

            if (created is null)
            {
                SetError("Could not apply template.");
                return;
            }

            SuccessMessage = $"Created task “{created.Title}”.";
            if (created.Id is int id)
                await Shell.Current.GoToAsync($"taskdetail?id={id}");
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
