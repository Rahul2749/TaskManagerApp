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
    }
}
