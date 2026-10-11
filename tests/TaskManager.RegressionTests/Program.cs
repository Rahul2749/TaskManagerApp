using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TaskManager.Billing;
using TaskManager.Client.Services;
using TaskManager.Controllers;
using TaskManager.Data;
using TaskManager.Helpers;
using TaskManager.Mapping;
using TaskManager.Models;
using TaskManager.Services;
using TaskManager.Services.Billing;
using TaskManager.Shared.DTOs;
using TaskManager.Shared.DTOs.Billing;
using TaskManager.Validation;
using MobileApi = TaskManager.Mobile.Services.IApiService;
using MobileAuth = TaskManager.Mobile.Services.IAuthService;

var checks = 0;
var groups = 0;
var failures = new List<string>();
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new InvalidOperationException(message);
}
async Task Run(string name, Func<Task> test)
{
    groups++;
    try { await test(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures.Add(name); Console.WriteLine($"FAIL {name}: {ex}"); }
}
T Payload<T>(ActionResult<T> result) => (T)((OkObjectResult)result.Result!).Value!;
var collaboration = Fake<ICollaborationService>.Create((m, _) => m.ReturnType == typeof(Task)
    ? Task.CompletedTask : Array.Empty<string>());
var events = Fake<IOutboundEventPublisher>.Create((_, _) => Task.CompletedTask);

await Run("ordinary users cannot access other people's task features", async () =>
{
    var tenant = new TestTenant(1, Roles.User, 20);
    await using var db = await Seed(tenant);
    var plans = new TestEntitlements();
    Check((await new CommentsController(db, tenant, collaboration).GetComments(1001)).Result is NotFoundResult, "comments leaked");
    Check((await new CommentsController(db, tenant, collaboration).CreateComment(1001, new() { Body = "wrong task" })).Result is NotFoundResult, "unauthorized comment created");
    Check((await new SubtasksController(db, tenant).GetSubtasks(1001)).Result is NotFoundResult, "subtasks leaked");
    Check((await new TagsController(db, tenant).ApplyTag(1, 1001)) is NotFoundObjectResult, "tag applied outside assignment");
    Check((await new TagsController(db, tenant).RemoveTag(1, 1001)) is NotFoundObjectResult, "tag removed outside assignment");
    Check(await db.TaskTags.AnyAsync(t => t.TaskId == 1001), "existing tag was changed");
    Check((await new CustomFieldsController(db, tenant, plans).GetTaskValues(1001)).Result is NotFoundResult, "custom fields leaked");
    Check(await new CustomFieldsController(db, tenant, plans).SetTaskValues(1001, new()) is NotFoundResult, "custom fields changed outside assignment");
    var timeline = Payload(await new TimelineController(db, tenant, plans).Get());
    Check(timeline.Tasks.Select(t => t.Id).SequenceEqual([1000]), "timeline leaked task titles");
    var activity = Payload(await new ActivityController(db, tenant).GetFeed());
    Check(activity.All(a => a.TaskId == 1000), "activity leaked task history");
    var times = new TimeEntriesController(db, tenant, plans);
    Check((await times.ForTask(1001)).Result is NotFoundObjectResult, "time entries leaked");
    Check(Payload(await times.ForTask(1000)).All(e => e.UserId == 20), "another user's timesheet leaked");
    Check(Payload(await new TaskDependenciesController(db, tenant, plans).List()).Count() == 0, "dependency titles leaked");
    Check((await new CustomFieldsController(db, tenant, plans).GetTaskValues(1000)).Result is OkObjectResult, "own fields inaccessible");
});

await Run("manager permissions apply to recurrence, dependencies, members and time entries", async () =>
{
    var tenant = new TestTenant(1, Roles.Manager, 10);
    await using var db = await Seed(tenant);
    var plans = new TestEntitlements();
    Check((await new TasksController(db, tenant, collaboration).SetRecurrence(1001, new() { Frequency = "daily" })).Result is NotFoundResult, "recurrence changed in another project");
    Check((await new ProjectsController(db, tenant, plans).GetProjectUsers(101)).Result is NotFoundResult, "another project's members leaked");
    var deps = new TaskDependenciesController(db, tenant, plans);
    Check(Payload(await deps.List()).Select(d => d.Id).SequenceEqual([1]), "manager saw outside dependency");
    Check((await deps.Create(new() { PredecessorTaskId = 1000, SuccessorTaskId = 1001 })).Result is NotFoundObjectResult, "outside dependency created");
    Check(await deps.Delete(2) is NotFoundResult, "outside dependency deleted");
    var times = new TimeEntriesController(db, tenant, plans);
    Check(Payload(await times.List()).Entries.All(e => e.TaskId != 1001), "manager saw another project's hours");
    Check((await times.Create(new() { TaskId = 1001, WorkDate = DateTime.UtcNow, Hours = 1 })).Result is NotFoundObjectResult, "manager logged time on outside task");
    Check(await times.Delete(2) is NotFoundResult, "manager deleted outside time entry");
    Check((await new CustomFieldsController(db, tenant, plans).Create(new() { Name = "other", ProjectId = 101 })).Result is NotFoundObjectResult, "outside project field created");
});

await Run("missing tenant context fails closed", async () =>
{
    var tenant = new TestTenant(null, Roles.Manager, 10);
    await using var db = await Seed(tenant);
    Check(await db.Tasks.VisibleTo(tenant).CountAsync() == 0, "missing tenant exposed all tasks");
    Check(await db.Projects.VisibleTo(tenant).CountAsync() == 0, "missing tenant exposed all projects");
    var super = new TestTenant(null, Roles.SuperAdmin, 99);
    Check(await db.Tasks.VisibleTo(super).CountAsync() == 4, "super admin cannot inspect tasks");
});

await Run("custom-field authorization allows users only on task-value endpoints", async () =>
{
    var services = new ServiceCollection().AddLogging().AddAuthorization().BuildServiceProvider();
    var policyProvider = services.GetRequiredService<IAuthorizationPolicyProvider>();
    var authorization = services.GetRequiredService<IAuthorizationService>();
    var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, Roles.User)], "test"));
    foreach (var methodName in new[] { "GetTaskValues", "SetTaskValues", "Create", "Update", "Delete", "List" })
    {
        var type = typeof(CustomFieldsController);
        var metadata = type.GetCustomAttributes<AuthorizeAttribute>().Cast<IAuthorizeData>()
            .Concat(type.GetMethod(methodName)!.GetCustomAttributes<AuthorizeAttribute>());
        var policy = await AuthorizationPolicy.CombineAsync(policyProvider, metadata);
        var result = await authorization.AuthorizeAsync(user, null, policy!);
        Check(result.Succeeded == (methodName is "GetTaskValues" or "SetTaskValues"), $"wrong authorization for {methodName}");
    }
});

await Run("task moves persist and reject foreign projects and assignees", async () =>
{
    var tenant = new TestTenant(1, Roles.Manager, 10);
    await using var db = await Seed(tenant);
    var controller = new TasksController(db, tenant, collaboration);
    var dto = new TaskDto { Title = "Moved", ProjectId = 102, AssignedToId = 20, Status = "InProgress" };
    Check((await controller.UpdateTask(1000, dto)).Result is OkObjectResult, "move failed");
    Check((await db.Tasks.FindAsync(1000))!.ProjectId == 102, "project change silently ignored");
    Check(await db.ProjectUsers.AnyAsync(p => p.ProjectId == 102 && p.UserId == 20), "assignee not mapped in new project");
    dto.ProjectId = 101;
    Check((await controller.UpdateTask(1000, dto)).Result is NotFoundObjectResult, "move to other manager's project allowed");
    dto.ProjectId = 102; dto.AssignedToId = 30;
    Check((await controller.UpdateTask(1000, dto)).Result is BadRequestObjectResult, "foreign assignee accepted");
    dto.AssignedToId = 22;
    Check((await controller.UpdateTask(1000, dto)).Result is BadRequestObjectResult, "inactive assignee accepted");
    dto.AssignedToId = 11;
    Check((await controller.UpdateTask(1000, dto)).Result is BadRequestObjectResult, "manager assigned as team user");
});

await Run("completion dates and status validation", async () =>
{
    var task = new TaskItem { Status = "InProgress" };
    task.SetStatus("Completed"); var date = task.CompletedDate;
    Check(date.HasValue, "completion date missing");
    task.SetStatus("Tested"); Check(task.CompletedDate == date, "testing overwrote completion date");
    task.SetStatus("InProgress"); Check(task.CompletedDate is null, "reopened task retains completion date");
    var validator = new UpdateTaskStatusDtoValidator();
    Check(!validator.Validate(new UpdateTaskStatusDto { Status = "Invalid", ActualHours = 1 }).IsValid, "invalid status accepted");
    Check(!validator.Validate(new UpdateTaskStatusDto { Status = "InProgress", ActualHours = -1 }).IsValid, "negative hours accepted");
    Check(validator.Validate(new UpdateTaskStatusDto { Status = "Closed", ActualHours = 0 }).IsValid, "valid status rejected");
    var tenant = new TestTenant(1, Roles.Manager, 10);
    await using var db = await Seed(tenant);
    var existing = (await db.Tasks.FindAsync(1000))!;
    existing.SetStatus("Completed"); await db.SaveChangesAsync();
    var dto = new TaskDto { Title = "Reopened", ProjectId = 100, AssignedToId = 20, Status = "InProgress" };
    await new TasksController(db, tenant, collaboration).UpdateTask(1000, dto);
    Check(existing.CompletedDate is null, "edit endpoint failed to clear completion date");
});

await Run("password is optional for editing but mandatory for creation", async () =>
{
    var dto = new RegisterDto { FirstName = "Test", LastName = "User", Username = "test.user", Email = "test@example.com" };
    Check(Validator.TryValidateObject(dto, new ValidationContext(dto), [], true), "edit form requires password");
    Check(new RegisterDtoValidator().Validate(dto).IsValid, "server validator requires password for edit");
    var tenant = new TestTenant(1, Roles.Manager, 10);
    await using var db = await Seed(tenant);
    var controller = new UsersController(db, tenant, new TestEntitlements());
    Check((await controller.CreateUser(dto)).Result is BadRequestObjectResult, "blank password accepted for creation");
    dto.Username = "user20"; dto.Email = "user20@example.com";
    Check((await controller.UpdateUser(20, dto)).Result is OkObjectResult, "edit with blank password rejected");
    Check((await db.Users.FindAsync(20))!.PasswordHash == "existing-hash", "edit changed existing password");
    dto.Password = "weak";
    Check(!new RegisterDtoValidator().Validate(dto).IsValid, "weak replacement password accepted");
});

await Run("project membership is deduplicated and validated before changing rows", async () =>
{
    var tenant = new TestTenant(1, Roles.Manager, 10);
    await using var db = await Seed(tenant);
    var controller = new ProjectsController(db, tenant, new TestEntitlements());
    Check(await controller.AssignUsersToProject(100, new() { UserIds = [20, 20, 21] }) is OkObjectResult, "duplicates rejected instead of deduplicated");
    Check(await db.ProjectUsers.CountAsync(p => p.ProjectId == 100 && p.UserId == 20) == 1, "duplicate member saved");
    Check(await controller.AssignUsersToProject(100, new() { UserIds = [30] }) is BadRequestObjectResult, "foreign member accepted");
    Check(await db.ProjectUsers.CountAsync(p => p.ProjectId == 100) == 2, "invalid request removed current members");
});

await Run("templates validate before saving, enforce limits and preserve checklist order", async () =>
{
    var tenant = new TestTenant(1, Roles.Manager, 10);
    await using var db = await Seed(tenant);
    var plans = new TestEntitlements { AllowProjects = false };
    var controller = new TemplatesController(db, tenant, plans)
    { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
    Check((await controller.ApplyProjectTemplate(1, new() { ProjectName = "limited" }, default)).Result is ObjectResult { StatusCode: 402 }, "template bypassed project cap");
    plans.AllowProjects = true;
    Check((await controller.ApplyProjectTemplate(2, new() { ProjectName = "bad" }, default)).Result is BadRequestObjectResult, "malformed legacy template accepted");
    Check(await db.Projects.CountAsync() == 3, "invalid template left a partial project");
    Check((await controller.CreateProjectTemplate(new() { Name = "bad", TasksJson = "{}" }, default)).Result is BadRequestObjectResult, "wrong JSON shape saved");
    Check((await controller.ApplyTaskTemplate(1, new() { ProjectId = 101 }, default)).Result is ForbidResult, "template created a task in another manager's project");
    var applied = await controller.ApplyTaskTemplate(1, new() { ProjectId = 100, AssignedToId = 20 }, default);
    var created = Payload(applied);
    var subtasks = await db.Subtasks.Where(s => s.TaskId == created.Id).OrderBy(s => s.SortOrder).ToListAsync();
    Check(subtasks.Select(s => s.Title).SequenceEqual(["one", "two"]), "checklist order incorrect");
    Check(subtasks.Select(s => s.SortOrder).SequenceEqual([1, 2]), "sort order not assigned");
    Check((await controller.ApplyProjectTemplate(1, new() { ProjectName = "valid" }, default)).Result is OkObjectResult, "valid project template failed");
    Check(await db.Tasks.AnyAsync(t => t.Title == "from template"), "project template failed to create task graph");
});

await Run("public API normalizes dates and rejects foreign users", async () =>
{
    var tenant = new TestTenant(1, Roles.OrganizationAdmin, 1);
    await using var db = await Seed(tenant);
    var controller = new PublicApiV1Controller(db, tenant, new TestEntitlements(), events);
    var dto = new TaskDto { Title = "API created", ProjectId = 100, AssignedToId = 30,
        StartDate = new DateTime(2026, 10, 12), DueDate = new DateTime(2026, 10, 13) };
    Check((await controller.CreateTask(dto, default)).Result is BadRequestObjectResult, "foreign API assignee accepted");
    dto.AssignedToId = 20;
    Check((await controller.CreateTask(dto, default)).Result is CreatedAtActionResult, "valid API task failed");
    var task = await db.Tasks.SingleAsync(t => t.Title == dto.Title);
    Check(task.StartDate!.Value.Kind == DateTimeKind.Utc && task.DueDate!.Value.Kind == DateTimeKind.Utc, "API persisted non-UTC dates");
    task.SetStatus("Completed");
    await controller.UpdateStatus(task.Id, new() { Status = "InProgress", ActualHours = 2 }, default);
    Check(task.CompletedDate is null && task.ActualHours == 2, "API reopening or hours update failed");
});

await Run("due-soon automation skips done tasks, runs once daily and meters each action", async () =>
{
    var tenant = new TestTenant(null, Roles.SuperAdmin, 99);
    await using var db = await Seed(tenant);
    var plans = new TestEntitlements { AutomationLimit = 1 };
    var date = DateTime.UtcNow.Date.AddDays(1);
    foreach (var task in await db.Tasks.ToListAsync()) task.DueDate = date;
    (await db.Tasks.FindAsync(1002))!.Status = "Tested";
    db.AutomationRules.Add(new() { OrganizationId = 1, Name = "reminder", TriggerType = "due_soon",
        ActionType = "add_comment", TriggerConfigJson = "{}", ActionConfigJson = "{}", IsEnabled = true });
    await db.SaveChangesAsync();
    var initial = await db.Comments.CountAsync();
    var jobs = new Phase6Jobs(db, plans, NullLogger<Phase6Jobs>.Instance);
    await jobs.ProcessDueSoonAutomationsAsync();
    Check(await db.Comments.CountAsync() == initial + 1, "run quota bypassed");
    Check(await db.UsageCounters.SingleAsync() is { Value: 1 }, "run not metered");
    await jobs.ProcessDueSoonAutomationsAsync();
    Check(await db.Comments.CountAsync() == initial + 1, "daily rule repeated");
    Check(!await db.Comments.AnyAsync(c => c.TaskId == 1002 && c.Body.StartsWith("Automation:")), "tested task received due-soon action");
});

await Run("comment threads delete together and tag collisions return a conflict", async () =>
{
    var tenant = new TestTenant(1, Roles.User, 20);
    await using var db = await Seed(tenant);
    Check(await new CommentsController(db, tenant, collaboration).DeleteComment(1000, 1) is NoContentResult, "thread delete failed");
    Check(!await db.Comments.AnyAsync(c => c.Id <= 3), "thread replies remained");
    var result = await new TagsController(db, tenant).UpdateTag(1, new() { Name = "second" });
    Check(result.Result is ConflictObjectResult, "duplicate tag name did not return conflict");
});

await Run("custom-field duplicate inputs do not create duplicate rows", async () =>
{
    var tenant = new TestTenant(1, Roles.User, 20);
    await using var db = await Seed(tenant);
    var controller = new CustomFieldsController(db, tenant, new TestEntitlements());
    Check(await controller.SetTaskValues(1000, new() { Values = [new() { DefinitionId = 1, Value = "first" }, new() { DefinitionId = 1, Value = "last" }] }) is NoContentResult, "duplicate values failed");
    var values = await db.CustomFieldValues.Where(v => v.TaskId == 1000).ToListAsync();
    Check(values.Count == 1 && values[0].Value == "last", "duplicate field rows saved");
});

await Run("subscription failures retry and cleared sessions ignore stale responses", async () =>
{
    var attempts = 0;
    var billing = Fake<IBillingService>.Create((_, _) => ++attempts == 1
        ? Task.FromException<SubscriptionDto?>(new HttpRequestException("temporary"))
        : Task.FromResult<SubscriptionDto?>(new() { PlanCode = "professional" }));
    var state = new EntitlementStateService(billing);
    await state.EnsureLoadedAsync(); Check(!state.IsLoaded, "failure cached as loaded");
    await state.EnsureLoadedAsync(); Check(state.IsLoaded && attempts == 2, "failed subscription not retried");
    var pending = new TaskCompletionSource<SubscriptionDto?>();
    state = new EntitlementStateService(Fake<IBillingService>.Create((_, _) => pending.Task));
    var loading = state.RefreshAsync(); state.Clear();
    pending.SetResult(new() { PlanCode = "old organization" }); await loading;
    Check(state.Subscription is null && !state.IsLoaded, "old session response repopulated cache");
});

await Run("web task form keeps projects after optional failures and ignores stale assignee responses", async () =>
{
    var page = new TaskManager.Client.Pages.Manager.ManageTasks();
    var type = page.GetType();
    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    T FieldValue<T>(string name) => (T)type.GetField(name, flags)!.GetValue(page)!;
    Task Invoke(string name, params object[] args) => (Task)type.GetMethod(name, flags)!.Invoke(page, args)!;
    var older = new TaskCompletionSource<List<UserDto>?>();
    var newer = new TaskCompletionSource<List<UserDto>?>();
    var api = Fake<TaskManager.Client.Services.IApiService>.Create((method, args) => method.Name switch
    {
        "GetProjectsAsync" => Task.FromResult<List<ProjectDto>?>([new() { Id = 100, Name = "created" }]),
        "GetUsersAsync" => Task.FromResult<List<UserDto>?>([]),
        "GetTasksAsync" => Task.FromResult<List<TaskDto>?>([]),
        "GetSavedViewsAsync" => Task.FromException<List<SavedViewDto>?>(new HttpRequestException("optional saved views failed")),
        "GetProjectUsersAsync" => (int)args![0]! == 100 ? older.Task : newer.Task,
        _ => throw new InvalidOperationException(method.Name)
    });
    type.GetProperty("ApiService", flags)!.SetValue(page, api);
    type.GetProperty("Snackbar", flags)!.SetValue(page, Fake<MudBlazor.ISnackbar>.Create((_, _) => null));
    await Invoke("LoadData");
    Check(FieldValue<List<ProjectDto>>("projects").Count == 1, "saved view failure hid projects");
    Check(!FieldValue<bool>("isLoading"), "page remained loading");
    await Invoke("OpenCreateDialog");
    Check(FieldValue<bool>("showDialog"), "create dialog did not open");
    var first = Invoke("OnProjectChange", 100);
    var second = Invoke("OnProjectChange", 102);
    Check(FieldValue<bool>("isLoadingProjectUsers"), "form allowed save while members were loading");
    newer.SetResult([new() { Id = 21 }]); await second;
    older.SetResult([new() { Id = 20 }]); await first;
    Check(FieldValue<List<UserDto>>("availableUsers").Select(u => u.Id).SequenceEqual([21]), "old request overwrote current members");
    Check(!FieldValue<bool>("isLoadingProjectUsers"), "form remained loading after selected request finished");
});

await Run("web task form refreshes newly created users and restores assignees on reopen", async () =>
{
    var page = new TaskManager.Client.Pages.Manager.ManageTasks();
    var type = page.GetType();
    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    T FieldValue<T>(string name) => (T)type.GetField(name, flags)!.GetValue(page)!;
    Task Invoke(string name, params object[] args) => (Task)type.GetMethod(name, flags)!.Invoke(page, args)!;
    void Close() => type.GetMethod("CloseDialog", flags)!.Invoke(page, null);
    var workspaceUsers = new List<UserDto> { new() { Id = 20, FirstName = "Existing", LastName = "User" } };
    var failUsers = false;
    var api = Fake<TaskManager.Client.Services.IApiService>.Create((method, _) => method.Name switch
    {
        "GetProjectsAsync" => Task.FromResult<List<ProjectDto>?>([new() { Id = 100, Name = "created" }]),
        "GetUsersAsync" => failUsers
            ? Task.FromException<List<UserDto>?>(new HttpRequestException("temporary user load failure"))
            : Task.FromResult<List<UserDto>?>(workspaceUsers.ToList()),
        "GetTasksAsync" => Task.FromResult<List<TaskDto>?>([]),
        "GetSavedViewsAsync" => Task.FromResult<List<SavedViewDto>?>([]),
        "GetProjectUsersAsync" => Task.FromResult<List<UserDto>?>([]),
        _ => throw new InvalidOperationException(method.Name)
    });
    type.GetProperty("ApiService", flags)!.SetValue(page, api);
    type.GetProperty("Snackbar", flags)!.SetValue(page, Fake<MudBlazor.ISnackbar>.Create((_, _) => null));
    await Invoke("LoadData");
    workspaceUsers.Add(new() { Id = 21, FirstName = "New", LastName = "User" });
    await Invoke("OpenCreateDialog");
    Check(FieldValue<List<UserDto>>("availableUsers").Any(u => u.Id == 21), "newly created user missing before project selection");
    Check(FieldValue<TaskDto>("taskModel").ProjectId == 0, "test unexpectedly selected a project");
    Close();
    await Invoke("OpenCreateDialog");
    Check(FieldValue<List<UserDto>>("availableUsers").Count == 2, "reopening dialog lost workspace users");
    await Invoke("OnProjectChange", 100);
    Check(FieldValue<List<UserDto>>("availableUsers").Count == 2, "project without mappings hid workspace users");
    await Invoke("OnProjectChange", 0);
    Check(FieldValue<List<UserDto>>("availableUsers").Count == 2, "clearing project emptied assignees");
    Close();
    workspaceUsers.Add(new() { Id = 23, FirstName = "Another", LastName = "User" });
    await Invoke("OpenEditDialog", new TaskDto { Id = 1000, ProjectId = 100, AssignedToId = 23, Title = "existing task" });
    Check(FieldValue<List<UserDto>>("availableUsers").Any(u => u.Id == 23), "edit used stale workspace users");
    Check(FieldValue<TaskDto>("taskModel").AssignedToId == 23, "edit lost selected assignee");
    Close();
    failUsers = true;
    await Invoke("OpenCreateDialog");
    Check(FieldValue<List<UserDto>>("availableUsers").Count == 3, "temporary refresh failure discarded cached users");
    Check(!FieldValue<bool>("isLoading"), "failed refresh left page loading");
    Close();
    failUsers = false;
    workspaceUsers.Add(new() { Id = 24, FirstName = "Recovered", LastName = "User" });
    await Invoke("OpenCreateDialog");
    Check(FieldValue<List<UserDto>>("availableUsers").Any(u => u.Id == 24), "next dialog open did not retry failed user refresh");
});

await Run("Razorpay checkout signatures use the API secret and provider reads preserve UTC dates", async () =>
{
    const string apiSecret = "regression-api-secret";
    const string webhookSecret = "regression-webhook-secret";
    string Sign(string value, string secret) => Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(secret), System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    var requests = 0;
    using var http = new HttpClient(new TestHttpHandler(request =>
    {
        requests++;
        Check(request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath == "/v1/subscriptions/sub_owned", "incorrect provider read");
        Check(request.Headers.Authorization?.Scheme == "Basic", "missing server-side provider authentication");
        return new(System.Net.HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"sub_owned\",\"status\":\"authenticated\",\"current_start\":null,\"current_end\":null,\"start_at\":1792886400}") };
    }));
    var provider = new RazorpayBillingProvider(http, Microsoft.Extensions.Options.Options.Create(new RazorpayOptions
    { KeyId = "rzp_test_regression", KeySecret = apiSecret, WebhookSecret = webhookSecret }), NullLogger<RazorpayBillingProvider>.Instance);
    var signature = Sign("pay_valid|sub_owned", apiSecret);
    Check(provider.VerifyCheckoutSignature("pay_valid", "sub_owned", signature), "valid checkout signature rejected");
    Check(!provider.VerifyCheckoutSignature("pay_other", "sub_owned", signature), "signature accepted for a different payment");
    Check(!provider.VerifyCheckoutSignature("pay_valid", "sub_foreign", signature), "signature accepted for a different subscription");
    Check(!provider.VerifyCheckoutSignature("pay_valid", "sub_owned", Sign("pay_valid|sub_owned", webhookSecret)), "checkout incorrectly uses webhook secret");
    Check(!provider.VerifyCheckoutSignature("pay_valid", "sub_owned", "invalid"), "malformed signature accepted");
    Check(provider.VerifyWebhookSignature("payload", Sign("payload", webhookSecret)), "webhook validation regressed");
    var current = await provider.GetSubscriptionAsync("sub_owned");
    Check(current.Status == "authenticated" && current.StartsAt?.Kind == DateTimeKind.Utc, "provider authorization/date parsing failed");
    Check(current.CurrentPeriodStart is null && current.CurrentPeriodEnd is null && requests == 1, "unstarted subscription gained a billing period");
});

await Run("checkout replaces the internal signup provider before verification and sync", async () =>
{
    var tenant = new TestTenant(1, Roles.OrganizationAdmin, 1);
    await using var db = await Seed(tenant);
    db.Plans.AddRange(new Plan { Id = 1, Code = PlanCodes.Free, Name = "Free" },
        new Plan { Id = 2, Code = PlanCodes.Professional, Name = "Professional", ProviderMonthlyPlanId = "plan_professional" });
    var sub = new Subscription { OrganizationId = 1, PlanId = 1, Provider = "internal", Status = SubscriptionStatus.Active };
    db.Subscriptions.Add(sub); await db.SaveChangesAsync();
    using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
    var app = Microsoft.Extensions.Options.Options.Create(new AppOptions());
    var entitlements = new EntitlementService(db, cache, NullLogger<EntitlementService>.Instance, app);
    var provider = Fake<IBillingProvider>.Create((method, _) => method.Name switch
    {
        "get_Name" => "razorpay", "get_IsConfigured" => true,
        "EnsureCustomerAsync" => Task.FromResult("cust_owned"),
        "CreateSubscriptionAsync" => Task.FromResult(new ProviderSubscription("sub_owned", "created", null)),
        "GetSubscriptionAsync" => Task.FromResult(new ProviderSubscription("sub_owned", "authenticated", null, StartsAt: DateTime.UtcNow.AddDays(14))),
        "VerifyCheckoutSignature" => true,
        _ => throw new InvalidOperationException(method.Name)
    });
    var controller = new BillingController(db, tenant, entitlements, provider,
        Microsoft.Extensions.Options.Options.Create(new RazorpayOptions { KeyId = "rzp_test_regression" }), app,
        Fake<Hangfire.IBackgroundJobClient>.Create((_, _) => "job"), NullLogger<BillingController>.Instance);
    var checkout = Payload(await controller.Checkout(new() { PlanCode = PlanCodes.Professional, Seats = 1 }, default));
    Check(checkout.ProviderSubscriptionId == "sub_owned", "checkout failed to create provider subscription");
    Check(sub.Provider == "razorpay", "checkout left signup subscription labeled internal");
    Check(sub.Status == SubscriptionStatus.Incomplete && sub.PlanId == 2, "checkout prematurely granted access or kept Free plan");
    var verified = Payload(await controller.VerifyCheckout(new() { PaymentId = "pay_valid", SubscriptionId = "sub_owned", Signature = new string('a', 64) }, default));
    Check(verified is { PlanCode: PlanCodes.Professional, Status: SubscriptionStatus.Trialing, IsActive: true }, "upgraded signup subscription could not verify");
    Check(Payload(await controller.SyncSubscription(default)).IsActive, "upgraded signup subscription could not sync");
    Check(await db.Subscriptions.CountAsync() == 1, "checkout duplicated signup subscription");
});

await Run("legacy internal subscriptions recover only after provider verification", async () =>
{
    var tenant = new TestTenant(1, Roles.OrganizationAdmin, 1);
    await using var db = await Seed(tenant);
    db.Plans.Add(new() { Id = 1, Code = PlanCodes.Professional, Name = "Professional" });
    var sub = new Subscription { OrganizationId = 1, PlanId = 1, Provider = "internal", ProviderSubscriptionId = "sub_owned", Status = SubscriptionStatus.Incomplete, TrialEndsAt = DateTime.UtcNow.AddDays(14) };
    db.Subscriptions.Add(sub); await db.SaveChangesAsync();
    using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
    var app = Microsoft.Extensions.Options.Options.Create(new AppOptions());
    var entitlements = new EntitlementService(db, cache, NullLogger<EntitlementService>.Instance, app);
    var reads = 0;
    var fail = true;
    var validSignature = false;
    var provider = Fake<IBillingProvider>.Create((method, _) => method.Name switch
    {
        "get_Name" => "razorpay", "get_IsConfigured" => true,
        "VerifyCheckoutSignature" => validSignature,
        "GetSubscriptionAsync" => Read(),
        _ => throw new InvalidOperationException(method.Name)
    });
    Task<ProviderSubscription> Read()
    {
        reads++;
        return fail ? Task.FromException<ProviderSubscription>(new HttpRequestException("provider unavailable"))
            : Task.FromResult(new ProviderSubscription("sub_owned", "authenticated", null, StartsAt: DateTime.UtcNow.AddDays(14)));
    }
    var controller = new BillingController(db, tenant, entitlements, provider,
        Microsoft.Extensions.Options.Options.Create(new RazorpayOptions()), app,
        Fake<Hangfire.IBackgroundJobClient>.Create((_, _) => "job"), NullLogger<BillingController>.Instance);
    var request = new VerifyCheckoutRequestDto { PaymentId = "pay_valid", SubscriptionId = "sub_owned", Signature = new string('a', 64) };
    Check((await controller.VerifyCheckout(request, default)).Result is BadRequestObjectResult && reads == 0, "legacy provider bypassed checkout signature validation");
    Check((await controller.SyncSubscription(default)).Result is ObjectResult { StatusCode: 503 }, "provider failure did not fail safely");
    Check(sub.Provider == "internal" && sub.Status == SubscriptionStatus.Incomplete, "failed provider lookup relabeled/unlocked legacy record");
    fail = false;
    var recovered = Payload(await controller.SyncSubscription(default));
    Check(recovered is { PlanCode: PlanCodes.Professional, Status: SubscriptionStatus.Trialing, IsActive: true } && sub.Provider == "razorpay", "legacy internal checkout did not recover through sync");
    sub.Provider = "internal"; await db.SaveChangesAsync();
    validSignature = true;
    Check(Payload(await controller.VerifyCheckout(request, default)).IsActive && sub.Provider == "razorpay", "legacy callback did not recover provider label");
    sub.Provider = "other-provider"; await db.SaveChangesAsync();
    var previousReads = reads;
    Check((await controller.SyncSubscription(default)).Result is ObjectResult { StatusCode: 503 } && reads == previousReads, "arbitrary other-provider records were reconciled");
    sub.Provider = "internal"; sub.ProviderSubscriptionId = "unrecognized-id"; await db.SaveChangesAsync();
    Check((await controller.SyncSubscription(default)).Result is ObjectResult { StatusCode: 503 } && reads == previousReads, "malformed internal provider ID was accepted for recovery");
});

await Run("verified checkout grants trial access, rejects foreign callbacks and refreshes cached entitlements", async () =>
{
    var tenant = new TestTenant(1, Roles.OrganizationAdmin, 1);
    await using var db = await Seed(tenant);
    db.Plans.Add(new() { Id = 1, Code = PlanCodes.Professional, Name = "Professional" });
    var trialEnd = DateTime.UtcNow.AddDays(14);
    var sub = new Subscription { OrganizationId = 1, PlanId = 1, ProviderSubscriptionId = "sub_owned", Status = SubscriptionStatus.Incomplete, TrialEndsAt = trialEnd };
    db.Subscriptions.AddRange(sub, new() { OrganizationId = 2, PlanId = 1, ProviderSubscriptionId = "sub_foreign", Status = SubscriptionStatus.Incomplete });
    await db.SaveChangesAsync();
    using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
    var app = Microsoft.Extensions.Options.Options.Create(new AppOptions());
    var entitlements = new EntitlementService(db, cache, NullLogger<EntitlementService>.Instance, app);
    Check((await entitlements.GetPlanAsync(1)).Code == PlanCodes.Free, "pending checkout prematurely unlocked plan");
    var providerReads = 0;
    var status = "authenticated";
    var signatureValid = false;
    var provider = Fake<IBillingProvider>.Create((method, args) => method.Name switch
    {
        "get_Name" => "razorpay",
        "get_IsConfigured" => true,
        "VerifyCheckoutSignature" => signatureValid && (string)args![1]! == "sub_owned",
        "GetSubscriptionAsync" => Read(),
        _ => throw new InvalidOperationException(method.Name)
    });
    Task<ProviderSubscription> Read() { providerReads++; return Task.FromResult(new ProviderSubscription("sub_owned", status, null,
        status == "active" ? DateTime.UtcNow : null, status == "active" ? DateTime.UtcNow.AddMonths(1) : null, trialEnd)); }
    var controller = new BillingController(db, tenant, entitlements, provider,
        Microsoft.Extensions.Options.Options.Create(new RazorpayOptions()), app,
        Fake<Hangfire.IBackgroundJobClient>.Create((_, _) => "job"), NullLogger<BillingController>.Instance);
    var request = new VerifyCheckoutRequestDto { SubscriptionId = "sub_owned", PaymentId = "pay_valid", Signature = new string('a', 64) };
    Check((await controller.VerifyCheckout(request, default)).Result is BadRequestObjectResult, "unverified checkout activated");
    signatureValid = true;
    Check((await controller.VerifyCheckout(new() { SubscriptionId = "sub_foreign", PaymentId = "pay_valid", Signature = request.Signature }, default)).Result is BadRequestObjectResult, "foreign subscription callback accepted");
    Check(providerReads == 0 && sub.Status == SubscriptionStatus.Incomplete, "rejected callbacks changed state/contacted provider");
    var trial = Payload(await controller.VerifyCheckout(request, default));
    Check(trial is { PlanCode: PlanCodes.Professional, Status: SubscriptionStatus.Trialing, IsActive: true }, "authorized trial stayed on Free");
    Check((await entitlements.GetPlanAsync(1)).Code == PlanCodes.Professional, "old Free entitlement cache survived verification");
    Check((await controller.VerifyCheckout(request, default)).Result is OkObjectResult && await db.Subscriptions.IgnoreQueryFilters().CountAsync() == 2, "duplicate callback created another subscription");
    Check((await db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.OrganizationId == 2)).Status == SubscriptionStatus.Incomplete, "another workspace was changed");
    status = "active";
    var active = Payload(await controller.SyncSubscription(default));
    Check(active is { IsActive: true, Status: SubscriptionStatus.Active } && sub.CurrentPeriodEnd?.Kind == DateTimeKind.Utc, "provider activation/period was not reconciled");
    status = "cancelled";
    var canceled = Payload(await controller.SyncSubscription(default));
    Check(canceled is { IsActive: false, PlanCode: PlanCodes.Free }, "canceled provider subscription retained paid access");
});

await Run("billing sync recovers missing callbacks without payment and fails safely on provider errors", async () =>
{
    var tenant = new TestTenant(1, Roles.OrganizationAdmin, 1);
    await using var db = await Seed(tenant);
    db.Plans.Add(new() { Id = 1, Code = PlanCodes.Professional, Name = "Professional" });
    var trialEnd = DateTime.UtcNow.AddDays(14);
    var sub = new Subscription { OrganizationId = 1, PlanId = 1, ProviderSubscriptionId = "sub_owned", Status = SubscriptionStatus.Incomplete, TrialEndsAt = trialEnd };
    db.Subscriptions.Add(sub); await db.SaveChangesAsync();
    using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
    var app = Microsoft.Extensions.Options.Options.Create(new AppOptions());
    var entitlements = new EntitlementService(db, cache, NullLogger<EntitlementService>.Instance, app);
    var status = "created";
    var fail = false;
    var returnedId = "sub_owned";
    var replaceDuringRead = false;
    var provider = Fake<IBillingProvider>.Create((method, args) => method.Name switch
    {
        "get_Name" => "razorpay", "get_IsConfigured" => true,
        "GetSubscriptionAsync" => Read(),
        _ => throw new InvalidOperationException($"Unexpected billing operation: {method.Name}")
    });
    async Task<ProviderSubscription> Read()
    {
        if (fail) throw new HttpRequestException("unavailable");
        if (replaceDuringRead)
        {
            sub.ProviderSubscriptionId = "sub_replacement";
            sub.Status = SubscriptionStatus.Incomplete;
            sub.UpdatedAt = DateTime.UtcNow.AddSeconds(1);
            await db.SaveChangesAsync();
        }
        return new ProviderSubscription(returnedId, status, null, StartsAt: trialEnd);
    }
    BillingController Controller(ITenantService currentTenant) => new(db, currentTenant, entitlements, provider,
        Microsoft.Extensions.Options.Options.Create(new RazorpayOptions()), app,
        Fake<Hangfire.IBackgroundJobClient>.Create((_, _) => "job"), NullLogger<BillingController>.Instance);
    var controller = Controller(tenant);
    Check(Payload(await controller.SyncSubscription(default)) is { IsActive: false, PlanCode: PlanCodes.Free }, "created subscription granted access");
    status = "authenticated";
    Check(Payload(await controller.SyncSubscription(default)) is { IsActive: true, Status: SubscriptionStatus.Trialing }, "lost callback was not recovered by provider read");
    fail = true;
    Check((await controller.SyncSubscription(default)).Result is ObjectResult { StatusCode: 503 } && sub.Status == SubscriptionStatus.Trialing, "provider failure erased existing trial");
    fail = false; returnedId = "sub_foreign";
    Check((await controller.SyncSubscription(default)).Result is ObjectResult { StatusCode: 503 } && sub.ProviderSubscriptionId == "sub_owned", "mismatched provider entity accepted");
    returnedId = "sub_owned"; trialEnd = DateTime.UtcNow.AddDays(-1);
    Check(Payload(await controller.SyncSubscription(default)) is { IsActive: false }, "expired authorization without charge granted access");
    Check((await Controller(new TestTenant(null, Roles.OrganizationAdmin, 1)).SyncSubscription(default)).Result is ForbidResult, "missing workspace context allowed sync");
    replaceDuringRead = true; status = "active";
    Check((await controller.SyncSubscription(default)).Result is ConflictObjectResult, "stale provider read overwrote a replacement checkout");
    Check(sub.ProviderSubscriptionId == "sub_replacement" && sub.Status == SubscriptionStatus.Incomplete, "replacement checkout gained old subscription's access");
    foreach (var action in new[] { nameof(BillingController.SyncSubscription), nameof(BillingController.VerifyCheckout) })
        Check(typeof(BillingController).GetMethod(action)!.GetCustomAttributes<AuthorizeAttribute>().Single().Roles == Roles.OrganizationAdmin, "billing sync/verification role restriction missing");
});

await Run("billing page offers retry after load failure and recovers existing subscription", async () =>
{
    var fail = true;
    var syncs = 0;
    var billing = Fake<IBillingService>.Create((method, _) => method.Name switch
    {
        "SyncSubscriptionAsync" => Sync(),
        "GetSubscriptionAsync" => fail ? Task.FromException<SubscriptionDto?>(new HttpRequestException("subscription load failed"))
            : Task.FromResult<SubscriptionDto?>(new() { PlanCode = PlanCodes.Professional, Status = "trialing", IsActive = true }),
        "GetInvoicesAsync" => fail ? Task.FromException<List<InvoiceDto>>(new HttpRequestException("invoice load failed")) : Task.FromResult(new List<InvoiceDto>()),
        _ => throw new InvalidOperationException(method.Name)
    });
    Task<SubscriptionDto?> Sync() { syncs++; return fail ? Task.FromException<SubscriptionDto?>(new HttpRequestException("sync failed")) : Task.FromResult<SubscriptionDto?>(new()); }
    var page = new TaskManager.Client.Pages.Billing.ManageBilling();
    var type = page.GetType();
    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    object? Field(string name) => type.GetField(name, flags)!.GetValue(page);
    Task Refresh() => (Task)type.GetMethod("RefreshBillingAsync", flags)!.Invoke(page, null)!;
    type.GetProperty("BillingService", flags)!.SetValue(page, billing);
    type.GetProperty("Entitlements", flags)!.SetValue(page, new EntitlementStateService(billing));
    await Refresh();
    Check(Field("_loading") is false, "billing remained on loading skeletons after failure");
    Check(Field("_subscription") is null && Field("_syncWarning") is string { Length: > 0 }, "failed subscription was silently shown as Free");
    Check(Field("_invoiceLoadFailed") is true, "invoice failure shown as no invoices");
    fail = false;
    await Refresh();
    Check(Field("_subscription") is SubscriptionDto { PlanCode: PlanCodes.Professional, IsActive: true }, "billing retry failed to display recovered plan");
    Check(Field("_syncWarning") is null && Field("_invoiceLoadFailed") is false && syncs == 2, "retry failed to clear error state");
});

await Run("pricing sends checkout details for verification and routes failures to billing recovery", async () =>
{
    var verifies = 0;
    var fail = false;
    var subscription = new SubscriptionDto { PlanCode = PlanCodes.Professional, Status = "trialing", IsActive = true };
    var billing = Fake<IBillingService>.Create((method, args) => method.Name switch
    {
        "StartCheckoutAsync" => Task.FromResult<CheckoutSessionDto?>(new() { ProviderKeyId = "rzp_test_regression", ProviderSubscriptionId = "sub_owned" }),
        "VerifyCheckoutAsync" => Verify((VerifyCheckoutRequestDto)args![0]!),
        "GetSubscriptionAsync" => Task.FromResult<SubscriptionDto?>(subscription),
        _ => throw new InvalidOperationException(method.Name)
    });
    Task<SubscriptionDto?> Verify(VerifyCheckoutRequestDto request)
    {
        verifies++;
        Check(request.PaymentId == "pay_valid" && request.SubscriptionId == "sub_owned" && request.Signature == new string('a', 64), "pricing discarded checkout result fields");
        return fail ? Task.FromException<SubscriptionDto?>(new HttpRequestException("verification unavailable")) : Task.FromResult<SubscriptionDto?>(subscription);
    }
    var page = new TaskManager.Client.Pages.Pricing();
    var type = page.GetType();
    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    var navigation = new TestNavigation();
    var js = Fake<Microsoft.JSInterop.IJSRuntime>.Create((method, _) =>
    {
        var resultType = method.ReturnType.GetGenericArguments()[0];
        var checkout = Activator.CreateInstance(resultType, nonPublic: true)!;
        resultType.GetProperty("PaymentId")!.SetValue(checkout, "pay_valid");
        resultType.GetProperty("SubscriptionId")!.SetValue(checkout, "sub_owned");
        resultType.GetProperty("Signature")!.SetValue(checkout, new string('a', 64));
        return Activator.CreateInstance(method.ReturnType, checkout);
    });
    type.GetField("_isAuthenticated", flags)!.SetValue(page, true);
    type.GetProperty("BillingService", flags)!.SetValue(page, billing);
    type.GetProperty("Entitlements", flags)!.SetValue(page, new EntitlementStateService(billing));
    type.GetProperty("JS", flags)!.SetValue(page, js);
    type.GetProperty("Navigation", flags)!.SetValue(page, navigation);
    type.GetProperty("Snackbar", flags)!.SetValue(page, Fake<MudBlazor.ISnackbar>.Create((_, _) => null));
    Task Checkout() => (Task)type.GetMethod("StartCheckout", flags)!.Invoke(page, [new PlanDto { Code = PlanCodes.Professional, Name = "Professional" }])!;
    await Checkout();
    Check(verifies == 1 && navigation.LastUri == "/billing", "successful callback was not verified before billing navigation");
    Check(type.GetField("_checkoutBusy", flags)!.GetValue(page) is false, "checkout stayed busy after verification");
    fail = true;
    await Checkout();
    Check(verifies == 2 && navigation.LastUri == "/billing", "verification failure did not reach billing recovery");
    Check(type.GetField("_checkoutBusy", flags)!.GetValue(page) is false, "checkout stayed busy after verification failure");
});

await Run("dashboard and analytics preserve empty projects, percentages and ownership scope", async () =>
{
    foreach (var role in new[] { Roles.OrganizationAdmin, Roles.Manager })
    {
        var tenant = new TestTenant(1, role, role == Roles.Manager ? 10 : 1);
        await using var db = await Seed(tenant);
        (await db.Tasks.FirstAsync(t => t.Id == 1000)).Status = "Completed";
        (await db.Tasks.FirstAsync(t => t.Id == 1002)).Status = "Tested";
        db.Tasks.Add(new() { Id = 1003, OrganizationId = 1, ProjectId = 100, Title = "remaining", Status = "InProgress", AssignedById = 10, AssignedToId = 20 });
        await db.SaveChangesAsync();
        var dashboard = Payload(await new DashboardController(db, tenant).GetDashboardData());
        var populated = dashboard.ProjectSummaries.Single(p => p.ProjectId == 100);
        var empty = dashboard.ProjectSummaries.Single(p => p.ProjectId == 102);
        Check(populated.TotalTasks == 3 && populated.CompletedTasks == 2 && populated.InProgressTasks == 1 && populated.CompletionPercentage == 66.67, "dashboard counts or rounding changed");
        Check(empty.TotalTasks == 0 && empty.CompletionPercentage == 0, "empty dashboard project did not return zero completion");
        Check(dashboard.ProjectSummaries.All(p => p.ProjectId != 200), "dashboard leaked another workspace");
        Check(role != Roles.Manager || dashboard.ProjectSummaries.All(p => p.ProjectId != 101), "dashboard leaked another manager's project");
        var analytics = Payload(await new AnalyticsController(db, tenant).Summary(default));
        Check(analytics.ProjectSummaries.Single(p => p.ProjectId == 100).CompletionPercentage == 66.7, "analytics rounding changed");
        Check(analytics.ProjectSummaries.Single(p => p.ProjectId == 102).CompletionPercentage == 0, "empty analytics project did not return zero completion");
        Check(analytics.ProjectSummaries.All(p => p.ProjectId != 200 && (role != Roles.Manager || p.ProjectId != 101)), "analytics scope changed");
    }
    var emptyTenant = new TestTenant(1, Roles.OrganizationAdmin, 1);
    await using var emptyDb = await Seed(emptyTenant);
    emptyDb.Tasks.RemoveRange(await emptyDb.Tasks.ToListAsync());
    await emptyDb.SaveChangesAsync();
    var emptyDashboard = Payload(await new DashboardController(emptyDb, emptyTenant).GetDashboardData());
    Check(emptyDashboard.TotalTasks == 0 && emptyDashboard.ProjectSummaries.All(p => p.CompletionPercentage == 0), "workspace without tasks failed");
});

await Run("project list and details preserve collections and manager access", async () =>
{
    var tenant = new TestTenant(1, Roles.Manager, 10);
    await using var db = await Seed(tenant);
    var controller = new ProjectsController(db, tenant, new TestEntitlements());
    var projects = Payload(await controller.GetProjects()).ToList();
    Check(projects.Select(p => p.Id).Order().SequenceEqual(new int?[] { 100, 102 }), "project list scope changed");
    var project = projects.Single(p => p.Id == 100);
    Check(project.TaskCount == 2 && project.AssignedUsers.Select(u => u.Id).SequenceEqual([20]), "project collections or counts changed");
    Check(Payload(await controller.GetProject(100)).TaskCount == 2, "project details lost tasks");
    Check((await controller.GetProject(101)).Result is ForbidResult, "project details leaked another manager's project");
});

await Run("project summary SQL contains counts and filters without division", () =>
{
    var tenant = new TestTenant(1, Roles.Manager, 10);
    using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql("Host=localhost;Database=regression;Username=test;Password=test").Options, tenant);
    var queries = new[] { db.Projects.SelectTaskCounts(), db.Projects.Where(p => p.ManagerId == 10).SelectTaskCounts(),
        db.Projects.SelectTaskCounts().OrderByDescending(p => p.TotalTasks).Take(20) };
    foreach (var query in queries)
    {
        var sql = query.ToQueryString();
        Check(!sql.Contains('/') && !sql.Contains("round(", StringComparison.OrdinalIgnoreCase), "percentage division returned to database SQL");
        Check(sql.Contains("count(*)") && sql.Contains("OrganizationId"), "SQL counts or tenant filters missing");
    }
    Check(queries[1].ToQueryString().Contains("ManagerId"), "manager filter missing");
    Check(queries[2].ToQueryString().Contains("LIMIT"), "analytics limit moved out of SQL");
    return Task.CompletedTask;
});

await Run("dashboard load failures show an error and retry recovers", async () =>
{
    foreach (var page in new object[] { new TaskManager.Client.Pages.Admin.Dashboard(), new TaskManager.Client.Pages.User.Dashboard() })
    {
        var attempt = 0;
        var api = Fake<TaskManager.Client.Services.IApiService>.Create((method, _) => method.Name == "GetDashboardDataAsync"
            ? ++attempt == 1 ? Task.FromException<DashboardDto?>(new HttpRequestException("server error")) : Task.FromResult<DashboardDto?>(new() { TotalProjects = 2 })
            : throw new InvalidOperationException(method.Name));
        var type = page.GetType();
        type.GetProperty("ApiService", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(page, api);
        var load = type.GetMethod("LoadDashboardData", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)load.Invoke(page, null)!;
        Check(type.GetField("_loadError", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page) is string { Length: > 0 }, "dashboard failure was hidden");
        await (Task)load.Invoke(page, null)!;
        Check(type.GetField("_loadError", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page) is null &&
            type.GetField("dashboardData", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page) is DashboardDto { TotalProjects: 2 }, "dashboard retry did not recover");
    }
});

await Run("task scope queries translate with the production PostgreSQL provider", () =>
{
    var tenant = new TestTenant(1, Roles.Manager, 10);
    using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql("Host=localhost;Database=regression;Username=test;Password=test").Options, tenant);
    var ids = db.Tasks.VisibleTo(tenant).Select(t => t.Id);
    Check(db.TaskDependencies.Where(d => ids.Contains(d.PredecessorTaskId) && ids.Contains(d.SuccessorTaskId))
        .ToQueryString().Contains("ManagerId"), "dependency scope failed SQL translation");
    Check(db.TimeEntries.VisibleTo(tenant).ToQueryString().Contains("ManagerId"), "time entry scope failed SQL translation");
    var monday = DateTimeUtc.Normalize(new DateTime(2026, 10, 12));
    Check(db.TimeEntries.Where(e => e.WorkDate >= monday && e.WorkDate < monday.AddDays(7)).ToQueryString().Contains("WorkDate"), "week date cannot be bound as timestamp");
    return Task.CompletedTask;
});

await Run("mobile edit preserves omitted fields and does not race assignee requests", async () =>
{
    TaskDto? saved = null;
    var requests = 0;
    var project = new ProjectDto { Id = 100, Name = "own" };
    var user = new UserDto { Id = 20, Username = "user20" };
    var api = Fake<MobileApi>.Create((method, args) => method.Name switch
    {
        "GetProjectsAsync" => Task.FromResult<List<ProjectDto>?>([project]),
        "GetTaskAsync" => Task.FromResult<TaskDto?>(new() { Id = 1000, Title = "mobile", ProjectId = 100,
            AssignedToId = 20, Status = "Completed", EstimatedHours = 7, StartDate = DateTime.UtcNow.Date, DueDate = null }),
        "GetProjectUsersAsync" => GetMembers(),
        "GetUsersAsync" => Task.FromResult<List<UserDto>?>([user]),
        "UpdateTaskAsync" => Save((TaskDto)args![1]!),
        _ => throw new InvalidOperationException(method.Name)
    });
    Task<List<UserDto>?> GetMembers() { requests++; return Task.FromResult<List<UserDto>?>([user]); }
    Task<TaskDto?> Save(TaskDto dto) { saved = dto; return Task.FromResult<TaskDto?>(dto); }
    var auth = Fake<MobileAuth>.Create((_, _) => Task.FromResult<UserDto?>(new() { Role = Roles.Manager }));
    var vm = new TaskManager.Mobile.ViewModels.TaskEditorViewModel(api, auth) { TaskId = 1000 };
    await vm.LoadCommand.ExecuteAsync(null);
    Check(requests == 1, "mobile edit issued duplicate assignee loads");
    Check(vm.PriorityOptions.Contains("Critical"), "critical priority missing");
    await vm.SaveCommand.ExecuteAsync(null);
    Check(saved is { EstimatedHours: 7, Status: "Completed", DueDate: null } && saved.StartDate.HasValue, "mobile edit overwrote existing fields");
    var older = new TaskCompletionSource<List<UserDto>?>();
    var newer = new TaskCompletionSource<List<UserDto>?>();
    var raceApi = Fake<MobileApi>.Create((method, args) => method.Name switch
    {
        "GetProjectUsersAsync" => (int)args![0]! == 100 ? older.Task : newer.Task,
        "GetUsersAsync" => Task.FromResult<List<UserDto>?>([]),
        _ => throw new InvalidOperationException(method.Name)
    });
    vm = new(raceApi, auth);
    vm.SelectedProject = new() { Id = 100 };
    vm.SelectedProject = new() { Id = 102 };
    newer.SetResult([new() { Id = 21, Username = "new project" }]);
    await Task.Yield(); await Task.Yield();
    older.SetResult([new() { Id = 20, Username = "old project" }]);
    await Task.Yield(); await Task.Yield();
    Check(vm.Users.Select(u => u.Id).SequenceEqual([21]), "older assignee response overwrote selected project");
});

await Run("late notification count responses do not restore a logged-out session", async () =>
{
    var pending = new TaskCompletionSource<int>();
    var api = Fake<TaskManager.Client.Services.IApiService>.Create((method, _) => method.Name switch
    {
        "GetUnreadNotificationCountAsync" => pending.Task,
        _ => throw new InvalidOperationException(method.Name)
    });
    var auth = new TestAuthStateProvider();
    var js = Fake<Microsoft.JSInterop.IJSRuntime>.Create((_, _) => throw new InvalidOperationException("unexpected storage read"));
    await using var notifications = new TaskManager.Client.Services.NotificationRealtimeService(new TestNavigation(),
        new TaskManager.Client.Services.LocalStorageService(js), api, auth);
    var refresh = notifications.RefreshUnreadCountAsync();
    auth.NotifyLoggedOut();
    pending.SetResult(42);
    await refresh;
    Check(notifications.UnreadCount == 0, "old account's notification count returned after logout");
});

await Run("lightweight browser JWT parsing preserves roles and rejects expired or malformed tokens", async () =>
{
    var removals = 0;
    string Token(object payload) => "eyJhbGciOiJIUzI1NiJ9." + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(payload)))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".signature";
    var token = Token(new { sub = "20", unique_name = "member", role = new[] { "User", "Manager" }, OrganizationId = "1", exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() });
    var js = Fake<Microsoft.JSInterop.IJSRuntime>.Create((method, args) =>
    {
        if ((string)args![0]! == "localStorage.getItem")
            return new ValueTask<string>(System.Text.Json.JsonSerializer.Serialize(token));
        removals++;
        return new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>();
    });
    using var http = new HttpClient();
    var provider = new TaskManager.Client.Services.CustomAuthStateProvider(http, new TaskManager.Client.Services.LocalStorageService(js));
    var state = await provider.GetAuthenticationStateAsync();
    Check(state.User.Identity?.Name == "member" && state.User.IsInRole("User") && state.User.IsInRole("Manager"), "browser claims/array roles changed");
    Check(state.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value == "20" && state.User.FindFirst("OrganizationId")?.Value == "1", "browser workspace claims lost");
    token = Token(new { sub = "20", exp = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds() });
    state = await provider.GetAuthenticationStateAsync();
    Check(state.User.Identity?.IsAuthenticated == false && http.DefaultRequestHeaders.Authorization is null && removals == 3, "expired token retained session access");
    token = "not-a-valid-jwt";
    state = await provider.GetAuthenticationStateAsync();
    Check(state.User.Identity?.IsAuthenticated == false && http.DefaultRequestHeaders.Authorization is null, "malformed token did not clear browser session");
});

await Run("concurrent browser storage reads coalesce without retaining stale tokens", async () =>
{
    var reads = 0;
    var pending = new TaskCompletionSource<string>();
    var js = Fake<Microsoft.JSInterop.IJSRuntime>.Create((method, args) =>
    {
        if ((string)args![0]! == "localStorage.getItem")
        {
            reads++;
            return new ValueTask<string>(pending.Task);
        }
        return new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>();
    });
    var storage = new TaskManager.Client.Services.LocalStorageService(js);
    var first = storage.GetItemAsync<string>("accessToken");
    var second = storage.GetItemAsync<string>("accessToken");
    Check(reads == 1, "simultaneous session reads performed duplicate JS interop");
    pending.SetResult("\"first-token\"");
    Check(await first == "first-token" && await second == "first-token", "coalesced storage read changed payload");
    pending = new TaskCompletionSource<string>();
    var fresh = storage.GetItemAsync<string>("accessToken");
    pending.SetResult("\"updated-token\"");
    Check(reads == 2 && await fresh == "updated-token", "storage cache retained another tab's previous token");
});

await Run("login prevents duplicate submissions and navigates without reloading WASM", async () =>
{
    var pending = new TaskCompletionSource<LoginResponseDto?>();
    var submissions = 0;
    var auth = Fake<TaskManager.Client.Services.IAuthServiceClient>.Create((method, _) => method.Name switch
    {
        "LoginAsync" => Submit(),
        "GetCurrentUserAsync" => Task.FromResult<UserDto?>(new() { Role = Roles.Manager }),
        _ => throw new InvalidOperationException(method.Name)
    });
    Task<LoginResponseDto?> Submit() { submissions++; return pending.Task; }
    var page = new TaskManager.Client.Pages.Login();
    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
    var navigation = new TestNavigation();
    page.GetType().GetProperty("AuthService", flags)!.SetValue(page, auth);
    page.GetType().GetProperty("Navigation", flags)!.SetValue(page, navigation);
    var method = page.GetType().GetMethod("HandleLogin", flags)!;
    var first = (Task)method.Invoke(page, null)!;
    await (Task)method.Invoke(page, null)!;
    Check(submissions == 1, "login issued duplicate concurrent requests");
    pending.SetResult(new() { Tokens = new() });
    await first;
    Check(navigation.LastUri == "/manager/dashboard" && !navigation.LastForceLoad, "successful login restarted WASM");
});

await Run("successful login rejects return URLs that lead back to authentication pages", async () =>
{
    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
    foreach (var returnUrl in new[] { "/login", "/login?returnUrl=%2Flogin", "/LOGIN/", "/%6cogin", "/register", "//outside.example" })
    {
        var auth = Fake<TaskManager.Client.Services.IAuthServiceClient>.Create((method, _) => method.Name switch
        {
            "LoginAsync" => Task.FromResult<LoginResponseDto?>(new() { Tokens = new() { User = new() { Role = Roles.Manager } } }),
            "GetCurrentUserAsync" => Task.FromResult<UserDto?>(new() { Role = Roles.Manager }),
            _ => throw new InvalidOperationException(method.Name)
        });
        var page = new TaskManager.Client.Pages.Login { ReturnUrl = returnUrl };
        var navigation = new TestNavigation();
        page.GetType().GetProperty("AuthService", flags)!.SetValue(page, auth);
        page.GetType().GetProperty("Navigation", flags)!.SetValue(page, navigation);
        await (Task)page.GetType().GetMethod("HandleLogin", flags)!.Invoke(page, null)!;
        Check(navigation.LastUri == "/manager/dashboard" && !navigation.LastForceLoad, "successful login stayed on authentication return URL: " + returnUrl);
    }
});

await Run("login routes each role and displays an error instead of silently stopping", async () =>
{
    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
    foreach (var (role, onboarding, expected) in new[] { (Roles.SuperAdmin, false, "/superadmin/dashboard"),
        (Roles.OrganizationAdmin, false, "/admin/dashboard"), (Roles.OrganizationAdmin, true, "/onboarding"),
        (Roles.Manager, false, "/manager/dashboard"), (Roles.User, false, "/user/dashboard") })
    {
        var auth = Fake<TaskManager.Client.Services.IAuthServiceClient>.Create((method, _) => method.Name switch
        {
            "LoginAsync" => Task.FromResult<LoginResponseDto?>(new() { Tokens = new() { User = new() { Role = role, NeedsOnboarding = onboarding } } }),
            "GetCurrentUserAsync" => Task.FromResult<UserDto?>(null),
            _ => throw new InvalidOperationException(method.Name)
        });
        var page = new TaskManager.Client.Pages.Login();
        var navigation = new TestNavigation();
        page.GetType().GetProperty("AuthService", flags)!.SetValue(page, auth);
        page.GetType().GetProperty("Navigation", flags)!.SetValue(page, navigation);
        await (Task)page.GetType().GetMethod("HandleLogin", flags)!.Invoke(page, null)!;
        Check(navigation.LastUri == expected && !navigation.LastForceLoad, "response user did not route role " + role);
        page.ReturnUrl = "/tasks/1000";
        await (Task)page.GetType().GetMethod("HandleLogin", flags)!.Invoke(page, null)!;
        Check(navigation.LastUri == "/tasks/1000", "valid protected return URL was discarded");
    }
    var missingUser = new TaskManager.Client.Pages.Login();
    var missingAuth = Fake<TaskManager.Client.Services.IAuthServiceClient>.Create((_, _) => Task.FromResult<UserDto?>(null));
    missingUser.GetType().GetProperty("AuthService", flags)!.SetValue(missingUser, missingAuth);
    missingUser.GetType().GetProperty("Navigation", flags)!.SetValue(missingUser, new TestNavigation());
    await (Task)missingUser.GetType().GetMethod("NavigateAfterLoginAsync", flags)!.Invoke(missingUser, [null])!;
    Check(!string.IsNullOrWhiteSpace((string?)missingUser.GetType().GetField("errorMessage", flags)!.GetValue(missingUser)), "missing session silently left login without an error");
});

await Run("real login publishes authenticated state before navigation and supersedes stale startup reads", async () =>
{
    var tokenService = new TokenService(new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["JwtSettings:SecretKey"] = "regression-only-token-signing-key-at-least-32-bytes",
        ["JwtSettings:Issuer"] = "regression", ["JwtSettings:Audience"] = "regression", ["JwtSettings:ExpiryMinutes"] = "30"
    }).Build());
    var token = tokenService.GenerateAccessToken(new User { Id = 20, Username = "member", Email = "member@example.com", FirstName = "Test", LastName = "Member", Role = Roles.Manager, OrganizationId = 1 });
    var tokens = new TokenDto { AccessToken = token, RefreshToken = "test-refresh", User = new() { Id = 20, Role = Roles.Manager } };
    var values = new Dictionary<string, string>();
    var pendingReads = new List<(TaskCompletionSource<string>, string?)>();
    var js = Fake<Microsoft.JSInterop.IJSRuntime>.Create((_, args) =>
    {
        var identifier = (string)args![0]!;
        var parameters = (object?[])args[^1]!;
        var key = (string)parameters[0]!;
        if (identifier == "localStorage.getItem")
        {
            var pending = new TaskCompletionSource<string>();
            pendingReads.Add((pending, values.GetValueOrDefault(key)));
            return new ValueTask<string>(pending.Task);
        }
        if (identifier == "localStorage.setItem") values[key] = (string)parameters[1]!;
        else if (identifier == "localStorage.removeItem") values.Remove(key);
        return new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>();
    });
    using var http = new HttpClient(new TestHttpHandler(_ => new(System.Net.HttpStatusCode.OK)
    { Content = System.Net.Http.Json.JsonContent.Create(new LoginResponseDto { Tokens = tokens }) })) { BaseAddress = new("https://regression.example/") };
    var storage = new TaskManager.Client.Services.LocalStorageService(js);
    var provider = new TaskManager.Client.Services.CustomAuthStateProvider(http, storage);
    var startup = provider.GetAuthenticationStateAsync();
    Task<Microsoft.AspNetCore.Components.Authorization.AuthenticationState>? announcement = null;
    provider.AuthenticationStateChanged += task => announcement = task;
    var entitlements = Fake<TaskManager.Client.Services.IEntitlementState>.Create((method, _) => method.Name == "Clear" ? null : throw new InvalidOperationException(method.Name));
    var auth = new TaskManager.Client.Services.ClientAuthService(http, storage, provider, entitlements);
    var result = await auth.LoginAsync(new() { Username = "member", Password = "test" });
    Check(result?.Tokens is not null && announcement?.IsCompletedSuccessfully == true, "login returned before authentication state was ready");
    Check((await announcement!).User.IsInRole(Roles.Manager), "real server JWT role was not published");
    foreach (var (pending, value) in pendingReads) pending.TrySetResult(value!);
    Check((await startup).User.IsInRole(Roles.Manager), "stale anonymous startup read replaced the signed-in state");
    Check((await auth.GetCurrentUserAsync())?.Id == 20, "real login lost current user");
});

await Run("notification badge updates do not render page content and dispose cleanly", () => UiRenderingChecks.NotificationsAsync(Check));
await Run("bounded tables preserve access to every page and refreshed records", () => UiRenderingChecks.TablePagingAsync(Check));
await Run("integration initialization overlaps independent reads and preserves results", () => UiInitializationChecks.IntegrationsAsync(Check));
await Run("task-detail initialization overlaps independent reads and preserves results", () => UiInitializationChecks.TaskDetailsAsync(Check));
await Run("old user reads cannot replace the successfully logged-in user", () => LoginSessionRaceChecks.OldUserReadAsync(Check));
await Run("expired startup cleanup cannot delete a new login session", () => LoginSessionRaceChecks.ExpiredCleanupAsync(Check));

Console.WriteLine($"{groups - failures.Count}/{groups} groups passed; {checks} assertions; {failures.Count} failures.");
return failures.Count == 0 ? 0 : 1;

static async Task<ApplicationDbContext> Seed(ITenantService tenant)
{
    var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenant);
    db.Organizations.AddRange(new Organization { Id = 1, Name = "one", Slug = "one" }, new Organization { Id = 2, Name = "two", Slug = "two" });
    foreach (var (id, role, org) in new[] { (1, Roles.OrganizationAdmin, 1), (10, Roles.Manager, 1), (11, Roles.Manager, 1),
        (20, Roles.User, 1), (21, Roles.User, 1), (22, Roles.User, 1), (30, Roles.User, 2), (31, Roles.Manager, 2) })
        db.Users.Add(new() { Id = id, Role = role, OrganizationId = org, IsActive = id != 22,
            Username = $"user{id}", Email = $"user{id}@example.com", FirstName = "Test", LastName = "User", PasswordHash = "existing-hash" });
    foreach (var (id, manager, org) in new[] { (100, 10, 1), (101, 11, 1), (102, 10, 1), (200, 31, 2) })
        db.Projects.Add(new() { Id = id, Name = $"project{id}", ManagerId = manager, OrganizationId = org });
    foreach (var (id, project, user, org) in new[] { (1000, 100, 20, 1), (1001, 101, 21, 1), (1002, 100, 21, 1), (2000, 200, 30, 2) })
        db.Tasks.Add(new() { Id = id, Title = $"task{id}", ProjectId = project, AssignedToId = user, AssignedById = org == 1 ? 10 : 31,
            OrganizationId = org, Status = "InProgress" });
    db.Tags.AddRange(new Tag { Id = 1, Name = "first", OrganizationId = 1 }, new Tag { Id = 2, Name = "second", OrganizationId = 1 });
    db.TaskTags.Add(new() { TaskId = 1001, TagId = 1 });
    db.ProjectUsers.Add(new() { ProjectId = 100, UserId = 20 });
    db.Comments.AddRange(new Comment { Id = 1, TaskId = 1000, AuthorId = 20, Body = "root" },
        new Comment { Id = 2, TaskId = 1000, AuthorId = 21, Body = "reply", ParentCommentId = 1 },
        new Comment { Id = 3, TaskId = 1000, AuthorId = 21, Body = "nested", ParentCommentId = 2 });
    db.TaskDependencies.AddRange(new TaskDependency { Id = 1, OrganizationId = 1, PredecessorTaskId = 1000, SuccessorTaskId = 1002 },
        new TaskDependency { Id = 2, OrganizationId = 1, PredecessorTaskId = 1000, SuccessorTaskId = 1001 });
    for (var i = 0; i < 3; i++)
    {
        db.TaskHistories.Add(new() { TaskId = 1000 + i, ChangedById = i == 0 ? 20 : 21, FieldName = "Created" });
        db.TimeEntries.Add(new() { Id = i + 1, OrganizationId = 1, TaskId = 1000 + i, UserId = i == 0 ? 20 : 21, WorkDate = DateTime.UtcNow.Date, Hours = 1 });
    }
    db.TimeEntries.Add(new() { Id = 4, OrganizationId = 1, TaskId = 1000, UserId = 21, WorkDate = DateTime.UtcNow.Date, Hours = 1 });
    db.CustomFieldDefinitions.Add(new() { Id = 1, OrganizationId = 1, Name = "field", FieldType = "text" });
    db.TaskTemplates.Add(new() { Id = 1, OrganizationId = 1, Name = "task template", Title = "from template", SubtasksJson = "[\"one\",\"two\"]" });
    db.ProjectTemplates.AddRange(new ProjectTemplate { Id = 1, OrganizationId = 1, Name = "project template", TasksJson = "[{\"title\":\"from template\"}]" },
        new ProjectTemplate { Id = 2, OrganizationId = 1, Name = "invalid", TasksJson = "{}" });
    await db.SaveChangesAsync();
    return db;
}

internal record TestTenant(int? OrganizationId, string? Role, int? UserId) : ITenantService
{
    public bool IsSuperAdmin => Role == Roles.SuperAdmin;
}

internal sealed class TestEntitlements : IEntitlementService
{
    public bool AllowProjects { get; set; } = true;
    public long? AutomationLimit { get; set; }
    public Task<PlanDefinition> GetPlanAsync(int? organizationId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> HasFeatureAsync(int? organizationId, string featureKey, CancellationToken ct = default) => Task.FromResult(true);
    public Task<long?> GetLimitAsync(int? organizationId, string limitKey, CancellationToken ct = default) => Task.FromResult(AutomationLimit);
    public Task<bool> IsWithinLimitAsync(int? organizationId, string limitKey, long currentUsage, long additional = 1, CancellationToken ct = default) => Task.FromResult(AllowProjects);
    public Task<bool> TryConsumeApiCallAsync(int organizationId, CancellationToken ct = default) => Task.FromResult(true);
    public void Invalidate(int organizationId) { }
}

internal sealed class TestAuthStateProvider : Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider
{
    public override Task<Microsoft.AspNetCore.Components.Authorization.AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(new Microsoft.AspNetCore.Components.Authorization.AuthenticationState(new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity())));
    public void NotifyLoggedOut() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}

internal sealed class TestNavigation : Microsoft.AspNetCore.Components.NavigationManager
{
    public string? LastUri { get; private set; }
    public bool LastForceLoad { get; private set; }
    public TestNavigation() => Initialize("https://regression.example/", "https://regression.example/pricing");
    protected override void NavigateToCore(string uri, bool forceLoad) { LastUri = uri; LastForceLoad = forceLoad; }
    protected override void NavigateToCore(string uri, Microsoft.AspNetCore.Components.NavigationOptions options)
    { LastUri = uri; LastForceLoad = options.ForceLoad; }
}

internal sealed class TestHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(response(request));
    }
}

public class Fake<T> : DispatchProxy where T : class
{
    private Func<MethodInfo, object?[]?, object?> _invoke = null!;
    public static T Create(Func<MethodInfo, object?[]?, object?> invoke)
    {
        var proxy = Create<T, Fake<T>>();
        ((Fake<T>)(object)proxy)._invoke = invoke;
        return proxy;
    }
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => _invoke(targetMethod!, args);
}
