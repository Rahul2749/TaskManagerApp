using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.Services;

public interface IApiService
{
    Task<List<UserDto>?> GetUsersAsync(string? role = null);
    Task<List<ProjectDto>?> GetProjectsAsync();
    Task<ProjectDto?> GetProjectAsync(int id);
    Task<List<TaskDto>?> GetTasksAsync(int? projectId = null, string? status = null, int? assignedToId = null);
    Task<TaskDto?> GetTaskAsync(int id);
    Task<TaskDto?> UpdateTaskStatusAsync(int id, UpdateTaskStatusDto statusDto);
    Task<TaskDto?> CreateTaskAsync(TaskDto task);
    Task<TaskDto?> UpdateTaskAsync(int id, TaskDto task);
    Task<bool> DeleteTaskAsync(int id);
    Task<ProjectDto?> CreateProjectAsync(ProjectDto project);
    Task<ProjectDto?> UpdateProjectAsync(int id, ProjectDto project);
    Task<bool> DeleteProjectAsync(int id);
    Task<DashboardDto?> GetDashboardDataAsync();
    // Users
    Task<UserDto?> GetUserAsync(int id);
    Task<UserDto?> CreateUserAsync(RegisterDto registerDto);
    Task<UserDto?> UpdateUserAsync(int id, RegisterDto updateDto);
    Task<bool> DeleteUserAsync(int id);

    // Onboarding / org / invites
    Task<OnboardingStatusDto?> GetOnboardingStatusAsync();
    Task<OnboardingStatusDto?> CompleteOnboardingAsync();
    Task<OrganizationSettingsDto?> GetOrganizationSettingsAsync();
    Task<(OrganizationSettingsDto? Settings, string? Error)> UpdateOrganizationSettingsAsync(UpdateOrganizationSettingsDto dto);
    Task<(OrganizationInviteDto? Invite, string? Error)> CreateInviteAsync(CreateInviteDto dto);
}
