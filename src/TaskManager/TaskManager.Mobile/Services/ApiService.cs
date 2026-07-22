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

    public async Task<TaskDto?> CreateTaskAsync(TaskDto task)
    {
        var response = await _httpClient.PostAsJsonAsync("api/tasks", task);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TaskDto>()
            : null;
    }

    public async Task<TaskDto?> UpdateTaskAsync(int id, TaskDto task)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/tasks/{id}", task);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TaskDto>()
            : null;
    }

    public async Task<bool> DeleteTaskAsync(int id)
    {
        var response = await _httpClient.DeleteAsync($"api/tasks/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<ProjectDto?> CreateProjectAsync(ProjectDto project)
    {
        var response = await _httpClient.PostAsJsonAsync("api/projects", project);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ProjectDto>()
            : null;
    }

    public async Task<ProjectDto?> UpdateProjectAsync(int id, ProjectDto project)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/projects/{id}", project);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ProjectDto>()
            : null;
    }

    public async Task<bool> DeleteProjectAsync(int id)
    {
        var response = await _httpClient.DeleteAsync($"api/projects/{id}");
        return response.IsSuccessStatusCode;
    }

    public Task<DashboardDto?> GetDashboardDataAsync() =>
        _httpClient.GetFromJsonAsync<DashboardDto>("api/dashboard");

    public Task<UserDto?> GetUserAsync(int id) =>
        _httpClient.GetFromJsonAsync<UserDto>($"api/users/{id}");

    public async Task<UserDto?> CreateUserAsync(RegisterDto registerDto)
    {
        var response = await _httpClient.PostAsJsonAsync("api/users", registerDto);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<UserDto>()
            : null;
    }

    public async Task<UserDto?> UpdateUserAsync(int id, RegisterDto updateDto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/users/{id}", updateDto);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<UserDto>()
            : null;
    }

    public async Task<bool> DeleteUserAsync(int id)
    {
        var response = await _httpClient.DeleteAsync($"api/users/{id}");
        return response.IsSuccessStatusCode;
    }

    public Task<OnboardingStatusDto?> GetOnboardingStatusAsync() =>
        _httpClient.GetFromJsonAsync<OnboardingStatusDto>("api/organizations/current/onboarding");

    public async Task<OnboardingStatusDto?> CompleteOnboardingAsync()
    {
        var response = await _httpClient.PostAsync("api/organizations/current/complete-onboarding", null);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<OnboardingStatusDto>()
            : null;
    }

    public Task<OrganizationSettingsDto?> GetOrganizationSettingsAsync() =>
        _httpClient.GetFromJsonAsync<OrganizationSettingsDto>("api/organizations/current");

    public async Task<(OrganizationSettingsDto? Settings, string? Error)> UpdateOrganizationSettingsAsync(
        UpdateOrganizationSettingsDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync("api/organizations/current", dto);
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<OrganizationSettingsDto>(), null);

        var err = await response.Content.ReadAsStringAsync();
        return (null, string.IsNullOrWhiteSpace(err) ? "Could not update workspace." : err);
    }

    public async Task<(OrganizationInviteDto? Invite, string? Error)> CreateInviteAsync(CreateInviteDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync("api/invites", dto);
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<OrganizationInviteDto>(), null);

        var err = await response.Content.ReadAsStringAsync();
        return (null, string.IsNullOrWhiteSpace(err) ? "Could not send invite." : err);
    }
}
