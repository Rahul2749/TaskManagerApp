using System.Reflection;
using TaskManager.Client.Services;
using TaskManager.Shared.DTOs;

internal static class UiInitializationChecks
{
    public static async Task IntegrationsAsync(Action<bool, string> check)
    {
        var keys = new TaskCompletionSource<List<OrganizationApiKeyDto>?>();
        var hooks = new TaskCompletionSource<List<OutboundWebhookDto>?>();
        var connections = new TaskCompletionSource<List<IntegrationConnectionDto>?>();
        var requests = 0;
        var api = Fake<IApiService>.Create((method, _) =>
        {
            requests++;
            return method.Name switch
            {
                "GetApiKeysAsync" => keys.Task,
                "GetOutboundWebhooksAsync" => hooks.Task,
                "GetIntegrationConnectionsAsync" => connections.Task,
                _ => throw new InvalidOperationException(method.Name)
            };
        });
        var page = new TaskManager.Client.Pages.Admin.Integrations();
        Inject(page, api);
        var load = Invoke(page, "ReloadAsync");
        check(requests == 3, "independent integration reads ran sequentially");
        keys.SetResult([new() { Id = 1 }]);
        check(!load.IsCompleted, "integration load completed before all responses");
        hooks.SetResult([new() { Id = 2 }]);
        connections.SetResult([new() { Id = 3 }]);
        await load;
        check(Field<List<OrganizationApiKeyDto>>(page, "_keys").Single().Id == 1
            && Field<List<OutboundWebhookDto>>(page, "_hooks").Single().Id == 2
            && Field<List<IntegrationConnectionDto>>(page, "_connections").Single().Id == 3, "integration results changed");
    }

    public static async Task TaskDetailsAsync(Action<bool, string> check)
    {
        var entries = new TaskCompletionSource<List<TimeEntryDto>?>();
        var dependencies = new TaskCompletionSource<List<TaskDependencyDto>?>();
        var tasks = new TaskCompletionSource<List<TaskDto>?>();
        var requests = 0;
        var api = Fake<IApiService>.Create((method, args) =>
        {
            if (method.Name == "GetTaskAsync") return Task.FromResult<TaskDto?>(new() { Id = 1000, ProjectId = 100, Status = "Assigned" });
            requests++;
            return method.Name switch
            {
                "GetTaskTimeEntriesAsync" => entries.Task,
                "GetDependenciesAsync" => dependencies.Task,
                "GetTasksAsync" when (int?)args![0] == 100 => tasks.Task,
                _ => throw new InvalidOperationException(method.Name)
            };
        });
        var page = new TaskManager.Client.Pages.User.TaskDetails();
        Inject(page, api);
        page.GetType().GetProperty("TaskId")!.SetValue(page, 1000);
        var load = Invoke(page, "LoadTask");
        check(requests == 3, "independent task-detail reads ran sequentially");
        entries.SetResult([new() { Id = 1 }]);
        dependencies.SetResult([new() { Id = 2 }]);
        tasks.SetResult([new() { Id = 1000 }, new() { Id = null }]);
        await load;
        check(Field<List<TimeEntryDto>>(page, "_timeEntries").Single().Id == 1
            && Field<List<TaskDependencyDto>>(page, "_dependencies").Single().Id == 2
            && Field<List<TaskDto>>(page, "_projectTasks").Single().Id == 1000, "task-detail results changed");
        check(!Field<bool>(page, "isLoading"), "task-detail loading state did not finish");
    }

    private static void Inject(object page, IApiService api) => page.GetType().GetProperty("ApiService", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(page, api);
    private static Task Invoke(object page, string name) => (Task)page.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null)!;
    private static T Field<T>(object page, string name) => (T)page.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;
}
