using TaskManager.Shared.DTOs;

namespace TaskManager.Client.Services
{
    public interface IApiService
    {
        // Users
        Task<List<UserDto>?> GetUsersAsync(string? role = null);
        Task<UserDto?> GetUserAsync(int id);
        Task<UserDto?> CreateUserAsync(RegisterDto registerDto);
        Task<UserDto?> UpdateUserAsync(int id, RegisterDto registerDto);
        Task<bool> DeleteUserAsync(int id);

        // Projects
        Task<List<ProjectDto>?> GetProjectsAsync();
        Task<ProjectDto?> GetProjectAsync(int id);
        Task<ProjectDto?> CreateProjectAsync(ProjectDto projectDto);
        Task<ProjectDto?> UpdateProjectAsync(int id, ProjectDto projectDto);
        Task<bool> DeleteProjectAsync(int id);
        Task<bool> AssignUsersToProjectAsync(int projectId, List<int> userIds);
        Task<List<UserDto>?> GetProjectUsersAsync(int projectId);

        // Tasks
        Task<List<TaskDto>?> GetTasksAsync(int? projectId = null, string? status = null, int? assignedToId = null);
        Task<TaskDto?> GetTaskAsync(int id);
        Task<TaskDto?> CreateTaskAsync(TaskDto taskDto);
        Task<TaskDto?> UpdateTaskAsync(int id, TaskDto taskDto);
        Task<TaskDto?> UpdateTaskStatusAsync(int id, UpdateTaskStatusDto statusDto);
        Task<bool> DeleteTaskAsync(int id);

        // Dashboard
        Task<DashboardDto?> GetDashboardDataAsync();

        // Platform administration
        Task<PlatformSummaryDto?> GetPlatformSummaryAsync();
        Task<List<PlatformOrganizationDto>?> GetPlatformOrganizationsAsync();
        Task<bool> UpdateOrganizationStatusAsync(int organizationId, string status);

        // Invites
        Task<List<OrganizationInviteDto>?> GetInvitesAsync();
        Task<(OrganizationInviteDto? Invite, string? Error)> CreateInviteAsync(CreateInviteDto dto);
        Task<bool> RevokeInviteAsync(int id);
        Task<(string? Email, string? Role, string? OrganizationName, string? Error)> PreviewInviteAsync(string token);

        // Organization settings / onboarding
        Task<OrganizationSettingsDto?> GetOrganizationSettingsAsync();
        Task<(OrganizationSettingsDto? Settings, string? Error)> UpdateOrganizationSettingsAsync(UpdateOrganizationSettingsDto dto);
        Task<OnboardingStatusDto?> GetOnboardingStatusAsync();
        Task<OnboardingStatusDto?> CompleteOnboardingAsync();

        // Saved views
        Task<List<SavedViewDto>?> GetSavedViewsAsync(string entityType = "task");
        Task<SavedViewDto?> CreateSavedViewAsync(CreateSavedViewDto dto);
        Task<bool> DeleteSavedViewAsync(int id);

        // Custom fields
        Task<List<CustomFieldDefinitionDto>?> GetCustomFieldDefinitionsAsync(int? projectId = null);
        Task<(CustomFieldDefinitionDto? Field, string? Error)> CreateCustomFieldAsync(UpsertCustomFieldDefinitionDto dto);
        Task<bool> DeleteCustomFieldAsync(int id);
        Task<List<CustomFieldValueDto>?> GetTaskCustomFieldsAsync(int taskId);
        Task<bool> SetTaskCustomFieldsAsync(int taskId, SetCustomFieldValuesDto dto);

        // Templates
        Task<List<TaskTemplateDto>?> GetTaskTemplatesAsync();
        Task<TaskTemplateDto?> CreateTaskTemplateAsync(UpsertTaskTemplateDto dto);
        Task<bool> DeleteTaskTemplateAsync(int id);
        Task<TaskDto?> ApplyTaskTemplateAsync(int id, ApplyTaskTemplateDto dto);
        Task<List<ProjectTemplateDto>?> GetProjectTemplatesAsync();
        Task<ProjectTemplateDto?> CreateProjectTemplateAsync(UpsertProjectTemplateDto dto);
        Task<bool> DeleteProjectTemplateAsync(int id);
        Task<ProjectDto?> ApplyProjectTemplateAsync(int id, ApplyProjectTemplateDto dto);
    }
}
