using System.Net.Http.Json;
using TaskManager.Shared.DTOs;
using TaskManager.Shared.DTOs.Billing;
using TaskManager.Shared.Http;

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
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<TaskDto>();
        throw new InvalidOperationException(await HttpErrorReader.ReadDetailAsync(response, "Failed to save task."));
    }

    public async Task<TaskDto?> UpdateTaskAsync(int id, TaskDto task)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/tasks/{id}", task);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<TaskDto>();
        throw new InvalidOperationException(await HttpErrorReader.ReadDetailAsync(response, "Failed to save task."));
    }

    public async Task<bool> DeleteTaskAsync(int id)
    {
        var response = await _httpClient.DeleteAsync($"api/tasks/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<ProjectDto?> CreateProjectAsync(ProjectDto project)
    {
        var response = await _httpClient.PostAsJsonAsync("api/projects", project);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<ProjectDto>();
        throw new InvalidOperationException(await HttpErrorReader.ReadDetailAsync(response, "Failed to save project."));
    }

    public async Task<ProjectDto?> UpdateProjectAsync(int id, ProjectDto project)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/projects/{id}", project);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<ProjectDto>();
        throw new InvalidOperationException(await HttpErrorReader.ReadDetailAsync(response, "Failed to save project."));
    }

    public async Task<bool> DeleteProjectAsync(int id)
    {
        var response = await _httpClient.DeleteAsync($"api/projects/{id}");
        return response.IsSuccessStatusCode;
    }

    public Task<DashboardDto?> GetDashboardDataAsync() =>
        _httpClient.GetFromJsonAsync<DashboardDto>("api/dashboard");

    public Task<WorkspaceAnalyticsDto?> GetWorkspaceAnalyticsAsync() =>
        _httpClient.GetFromJsonAsync<WorkspaceAnalyticsDto>("api/analytics/summary");

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

    public Task<List<CommentDto>?> GetCommentsAsync(int taskId) =>
        _httpClient.GetFromJsonAsync<List<CommentDto>>($"api/tasks/{taskId}/comments");

    public async Task<CommentDto?> CreateCommentAsync(int taskId, string body, int? parentCommentId = null)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/tasks/{taskId}/comments", new CommentDto
        {
            TaskId = taskId,
            Body = body,
            ParentCommentId = parentCommentId
        });
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CommentDto>()
            : null;
    }

    public Task<List<SubtaskDto>?> GetSubtasksAsync(int taskId) =>
        _httpClient.GetFromJsonAsync<List<SubtaskDto>>($"api/tasks/{taskId}/subtasks");

    public async Task<SubtaskDto?> CreateSubtaskAsync(int taskId, string title)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/tasks/{taskId}/subtasks", new SubtaskDto
        {
            TaskId = taskId,
            Title = title
        });
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<SubtaskDto>()
            : null;
    }

    public async Task<SubtaskDto?> UpdateSubtaskAsync(int taskId, SubtaskDto subtask)
    {
        if (subtask.Id is not int id)
            return null;

        var response = await _httpClient.PutAsJsonAsync($"api/tasks/{taskId}/subtasks/{id}", subtask);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<SubtaskDto>()
            : null;
    }

    public async Task<bool> DeleteSubtaskAsync(int taskId, int subtaskId)
    {
        var response = await _httpClient.DeleteAsync($"api/tasks/{taskId}/subtasks/{subtaskId}");
        return response.IsSuccessStatusCode;
    }

    public Task<SubscriptionDto?> GetSubscriptionAsync() =>
        _httpClient.GetFromJsonAsync<SubscriptionDto>("api/billing/subscription");

    public Task<List<InvoiceDto>?> GetInvoicesAsync() =>
        _httpClient.GetFromJsonAsync<List<InvoiceDto>>("api/billing/invoices");

    public Task<List<TaskTemplateDto>?> GetTaskTemplatesAsync() =>
        _httpClient.GetFromJsonAsync<List<TaskTemplateDto>>("api/templates/tasks");

    public async Task<TaskDto?> ApplyTaskTemplateAsync(int templateId, ApplyTaskTemplateDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/templates/tasks/{templateId}/apply", dto);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TaskDto>()
            : null;
    }

    public async Task<List<AppNotificationDto>?> GetNotificationsAsync(bool unreadOnly = false, int take = 50) =>
        await _httpClient.GetFromJsonAsync<List<AppNotificationDto>>(
            $"api/notifications?unreadOnly={unreadOnly}&take={take}");

    public async Task<int> GetUnreadNotificationCountAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<UnreadCountResponse>("api/notifications/unread-count");
        return result?.Count ?? 0;
    }

    public async Task<bool> MarkNotificationReadAsync(int id) =>
        (await _httpClient.PostAsync($"api/notifications/{id}/read", null)).IsSuccessStatusCode;

    public async Task<bool> MarkAllNotificationsReadAsync() =>
        (await _httpClient.PostAsync("api/notifications/read-all", null)).IsSuccessStatusCode;

    public Task<List<ActivityItemDto>?> GetActivityFeedAsync(int take = 40) =>
        _httpClient.GetFromJsonAsync<List<ActivityItemDto>>($"api/activity?take={take}");

    public Task<TimelineDto?> GetTimelineAsync(int? projectId = null)
    {
        var url = projectId.HasValue ? $"api/timeline?projectId={projectId}" : "api/timeline";
        return _httpClient.GetFromJsonAsync<TimelineDto>(url);
    }

    public Task<TimesheetSummaryDto?> GetTimesheetAsync(DateTime? weekStart = null)
    {
        var url = weekStart.HasValue ? $"api/time-entries?weekStart={weekStart:O}" : "api/time-entries";
        return _httpClient.GetFromJsonAsync<TimesheetSummaryDto>(url);
    }

    public async Task<(TimeEntryDto? Entry, string? Error)> CreateTimeEntryAsync(CreateTimeEntryDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync("api/time-entries", dto);
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<TimeEntryDto>(), null);
        return (null, await response.Content.ReadAsStringAsync());
    }

    public Task<List<TimeEntryDto>?> GetTaskTimeEntriesAsync(int taskId) =>
        _httpClient.GetFromJsonAsync<List<TimeEntryDto>>($"api/time-entries/task/{taskId}");

    public async Task<bool> DeleteTimeEntryAsync(int id) =>
        (await _httpClient.DeleteAsync($"api/time-entries/{id}")).IsSuccessStatusCode;

    public Task<List<TaskDependencyDto>?> GetDependenciesAsync(int? projectId = null, int? taskId = null)
    {
        var qs = new List<string>();
        if (projectId.HasValue) qs.Add($"projectId={projectId}");
        if (taskId.HasValue) qs.Add($"taskId={taskId}");
        var url = qs.Count == 0 ? "api/dependencies" : $"api/dependencies?{string.Join("&", qs)}";
        return _httpClient.GetFromJsonAsync<List<TaskDependencyDto>>(url);
    }

    public async Task<(TaskDependencyDto? Dependency, string? Error)> CreateDependencyAsync(CreateTaskDependencyDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync("api/dependencies", dto);
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<TaskDependencyDto>(), null);
        var err = await response.Content.ReadAsStringAsync();
        return (null, string.IsNullOrWhiteSpace(err) ? "Could not create dependency." : err);
    }

    public async Task<bool> DeleteDependencyAsync(int id) =>
        (await _httpClient.DeleteAsync($"api/dependencies/{id}")).IsSuccessStatusCode;

    public async Task<TaskDto?> SetTaskRecurrenceAsync(int taskId, SetRecurrenceDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/tasks/{taskId}/recurrence", dto);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TaskDto>()
            : null;
    }

    public Task<List<AutomationRuleDto>?> GetAutomationRulesAsync() =>
        _httpClient.GetFromJsonAsync<List<AutomationRuleDto>>("api/automations");

    private sealed class UnreadCountResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("count")]
        public int Count { get; set; }
    }
}
