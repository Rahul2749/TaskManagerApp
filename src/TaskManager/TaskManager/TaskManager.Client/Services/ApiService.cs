using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaskManager.Shared.DTOs;

namespace TaskManager.Client.Services
{
    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly LocalStorageService _localStorage;
        private readonly IAuthService _authService;

        public ApiService(HttpClient httpClient, LocalStorageService localStorage, IAuthService authService)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
            _authService = authService;
        }

        private async Task<HttpClient> GetAuthenticatedClientAsync()
        {
            var token = await _localStorage.GetItemAsync<string>("accessToken");

            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }

            return _httpClient;
        }

        // Users
        public async Task<List<UserDto>?> GetUsersAsync(string? role = null)
        {
            var client = await GetAuthenticatedClientAsync();
            var url = role != null ? $"api/users?role={role}" : "api/users";
            return await client.GetFromJsonAsync<List<UserDto>>(url);
        }

        public async Task<UserDto?> GetUserAsync(int id)
        {
            var client = await GetAuthenticatedClientAsync();
            return await client.GetFromJsonAsync<UserDto>($"api/users/{id}");
        }

        public async Task<UserDto?> CreateUserAsync(RegisterDto registerDto)
        {
            var client = await GetAuthenticatedClientAsync();
            var response = await client.PostAsJsonAsync("api/users", registerDto);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<UserDto>() : null;
        }

        public async Task<UserDto?> UpdateUserAsync(int id, RegisterDto registerDto)
        {
            var client = await GetAuthenticatedClientAsync();
            var response = await client.PutAsJsonAsync($"api/users/{id}", registerDto);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<UserDto>() : null;
        }

        public async Task<bool> DeleteUserAsync(int id)
        {
            var client = await GetAuthenticatedClientAsync();
            var response = await client.DeleteAsync($"api/users/{id}");
            return response.IsSuccessStatusCode;
        }

        // Projects
        public async Task<List<ProjectDto>?> GetProjectsAsync()
        {
            var client = await GetAuthenticatedClientAsync();
            return await client.GetFromJsonAsync<List<ProjectDto>>("api/projects");
        }

        public async Task<ProjectDto?> GetProjectAsync(int id)
        {
            var client = await GetAuthenticatedClientAsync();
            return await client.GetFromJsonAsync<ProjectDto>($"api/projects/{id}");
        }

        public async Task<ProjectDto?> CreateProjectAsync(ProjectDto projectDto)
        {
            var client = await GetAuthenticatedClientAsync();
            var response = await client.PostAsJsonAsync("api/projects", projectDto);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ProjectDto>() : null;
        }

        public async Task<ProjectDto?> UpdateProjectAsync(int id, ProjectDto projectDto)
        {
            var client = await GetAuthenticatedClientAsync();
            var response = await client.PutAsJsonAsync($"api/projects/{id}", projectDto);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ProjectDto>() : null;
        }

        public async Task<bool> DeleteProjectAsync(int id)
        {
            var client = await GetAuthenticatedClientAsync();
            var response = await client.DeleteAsync($"api/projects/{id}");
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> AssignUsersToProjectAsync(int projectId, List<int> userIds)
        {
            var client = await GetAuthenticatedClientAsync();
            var response = await client.PostAsJsonAsync($"api/projects/{projectId}/users",
                new ProjectUserMappingDto { ProjectId = projectId, UserIds = userIds });
            return response.IsSuccessStatusCode;
        }

        public async Task<List<UserDto>?> GetProjectUsersAsync(int projectId)
        {
            var client = await GetAuthenticatedClientAsync();
            return await client.GetFromJsonAsync<List<UserDto>>($"api/projects/{projectId}/users");
        }

        // Tasks
        public async Task<List<TaskDto>?> GetTasksAsync(int? projectId = null, string? status = null, int? assignedToId = null)
        {
            var client = await GetAuthenticatedClientAsync();
            var queryParams = new List<string>();

            if (projectId.HasValue) queryParams.Add($"projectId={projectId}");
            if (!string.IsNullOrEmpty(status)) queryParams.Add($"status={status}");
            if (assignedToId.HasValue) queryParams.Add($"assignedToId={assignedToId}");

            var url = queryParams.Any() ? $"api/tasks?{string.Join("&", queryParams)}" : "api/tasks";
            return await client.GetFromJsonAsync<List<TaskDto>>(url);
        }

        public async Task<TaskDto?> GetTaskAsync(int id)
        {
            var client = await GetAuthenticatedClientAsync();
            return await client.GetFromJsonAsync<TaskDto>($"api/tasks/{id}");
        }

        public async Task<TaskDto?> CreateTaskAsync(TaskDto taskDto)
        {
            var client = await GetAuthenticatedClientAsync();
            var response = await client.PostAsJsonAsync("api/tasks", taskDto);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<TaskDto>() : null;
        }

        public async Task<TaskDto?> UpdateTaskAsync(int id, TaskDto taskDto)
        {
            var client = await GetAuthenticatedClientAsync();
            var response = await client.PutAsJsonAsync($"api/tasks/{id}", taskDto);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<TaskDto>() : null;
        }

        public async Task<TaskDto?> UpdateTaskStatusAsync(int id, UpdateTaskStatusDto statusDto)
        {
            var client = await GetAuthenticatedClientAsync();
            var response = await client.PutAsJsonAsync($"api/tasks/{id}/status", statusDto);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<TaskDto>() : null;
        }

        public async Task<bool> DeleteTaskAsync(int id)
        {
            var client = await GetAuthenticatedClientAsync();
            var response = await client.DeleteAsync($"api/tasks/{id}");
            return response.IsSuccessStatusCode;
        }

        // Dashboard
        public async Task<DashboardDto?> GetDashboardDataAsync()
        {
            var client = await GetAuthenticatedClientAsync();
            return await client.GetFromJsonAsync<DashboardDto>("api/dashboard");
        }
    }
}
