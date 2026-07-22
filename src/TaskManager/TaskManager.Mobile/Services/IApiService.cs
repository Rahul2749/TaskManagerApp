using TaskManager.Shared.DTOs;
using TaskManager.Shared.DTOs.Billing;

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

    // Comments & subtasks
    Task<List<CommentDto>?> GetCommentsAsync(int taskId);
    Task<CommentDto?> CreateCommentAsync(int taskId, string body, int? parentCommentId = null);
    Task<List<SubtaskDto>?> GetSubtasksAsync(int taskId);
    Task<SubtaskDto?> CreateSubtaskAsync(int taskId, string title);
    Task<SubtaskDto?> UpdateSubtaskAsync(int taskId, SubtaskDto subtask);
    Task<bool> DeleteSubtaskAsync(int taskId, int subtaskId);

    // Billing / templates
    Task<SubscriptionDto?> GetSubscriptionAsync();
    Task<List<TaskTemplateDto>?> GetTaskTemplatesAsync();
    Task<TaskDto?> ApplyTaskTemplateAsync(int templateId, ApplyTaskTemplateDto dto);
}
