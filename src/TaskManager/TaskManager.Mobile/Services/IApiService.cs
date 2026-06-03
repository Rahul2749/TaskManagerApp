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
    Task<DashboardDto?> GetDashboardDataAsync();
}
