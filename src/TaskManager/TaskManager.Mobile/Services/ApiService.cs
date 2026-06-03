using System.Net.Http.Json;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.Services;

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;

    public ApiService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<List<UserDto>?> GetUsersAsync(string? role = null)
    {
        var url = role != null ? $"api/users?role={role}" : "api/users";
        return await _httpClient.GetFromJsonAsync<List<UserDto>>(url);
    }

    public Task<List<ProjectDto>?> GetProjectsAsync() =>
        _httpClient.GetFromJsonAsync<List<ProjectDto>>("api/projects");

    public Task<ProjectDto?> GetProjectAsync(int id) =>
        _httpClient.GetFromJsonAsync<ProjectDto>($"api/projects/{id}");

    public async Task<List<TaskDto>?> GetTasksAsync(int? projectId = null, string? status = null, int? assignedToId = null)
    {
        var queryParams = new List<string>();
        if (projectId.HasValue) queryParams.Add($"projectId={projectId}");
        if (!string.IsNullOrEmpty(status)) queryParams.Add($"status={status}");
        if (assignedToId.HasValue) queryParams.Add($"assignedToId={assignedToId}");

        var url = queryParams.Count > 0
            ? $"api/tasks?{string.Join("&", queryParams)}"
            : "api/tasks";

        return await _httpClient.GetFromJsonAsync<List<TaskDto>>(url);
    }

    public Task<TaskDto?> GetTaskAsync(int id) =>
        _httpClient.GetFromJsonAsync<TaskDto>($"api/tasks/{id}");

    public async Task<TaskDto?> UpdateTaskStatusAsync(int id, UpdateTaskStatusDto statusDto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/tasks/{id}/status", statusDto);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TaskDto>()
            : null;
    }

    public Task<DashboardDto?> GetDashboardDataAsync() =>
        _httpClient.GetFromJsonAsync<DashboardDto>("api/dashboard");
}
