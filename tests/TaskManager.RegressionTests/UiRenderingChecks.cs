using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Services;
using TaskManager.Client.Components.Layout;
using TaskManager.Client.Services;

internal static class UiRenderingChecks
{
    private static ServiceProvider Services(TestNotifications? notifications = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
        services.AddSingleton<NavigationManager>(new TestNavigation());
        services.AddSingleton(Fake<IJSRuntime>.Create((method, _) => Activator.CreateInstance(method.ReturnType)));
        if (notifications is not null) services.AddSingleton<INotificationRealtimeService>(notifications);
        return services.BuildServiceProvider();
    }

    public static async Task NotificationsAsync(Action<bool, string> check)
    {
        var notifications = new TestNotifications();
        await using var services = Services(notifications);
        var bodyRenders = 0;
        await using (var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>()))
        {
            await renderer.Dispatcher.InvokeAsync(async () =>
            {
                var root = await renderer.RenderComponentAsync<NotificationHost>(ParameterView.FromDictionary(new Dictionary<string, object?>
                { [nameof(NotificationHost.BodyRendered)] = (Action)(() => bodyRenders++) }));
                var initial = root.ToHtmlString();
                check(notifications.Connections == 1 && notifications.Subscribers == 1, "notification component did not connect/subscribe once");
                var initialRenders = bodyRenders;
                notifications.SetCount(42);
                var updated = root.ToHtmlString();
                check(updated != initial && updated.Contains("42"), "notification badge did not update");
                check(bodyRenders == initialRenders, "notification rerendered page content");
                notifications.SetCount(0);
                check(root.ToHtmlString() == initial, "logout/read reset left an old badge visible");
                check(bodyRenders == initialRenders, "reset rerendered page content");
            });
        }
        check(notifications.Subscribers == 0, "disposed notification component retained its subscription");
        notifications.SetCount(99); // Disposal must leave no callback to a dead renderer.
    }

    public static async Task TablePagingAsync(Action<bool, string> check)
    {
        await using var services = Services();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            MudTable<int>? table = null;
            var root = await renderer.RenderComponentAsync<PagedTableHost>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { [nameof(PagedTableHost.Capture)] = (Action<MudTable<int>>)(value => table = value) }));
            var html = root.ToHtmlString();
            check(html.Contains("row-0001") && html.Contains("row-0020") && !html.Contains("row-0021"), "table did not bound initial rows to 20");
            table!.NavigateTo(1);
            html = root.ToHtmlString();
            check(html.Contains("row-0021") && !html.Contains("row-0001"), "next page did not expose remaining records");
            table.NavigateTo(9);
            html = root.ToHtmlString();
            check(html.Contains("row-0200"), "last table records are inaccessible");
            await table.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(table.Items)] = new[] { 1, 2 } }));
            html = root.ToHtmlString();
            check(html.Contains("row-0001") && !html.Contains("row-0200"), "table refresh retained deleted rows");
        });
    }

    private sealed class NotificationHost : ComponentBase
    {
        [Parameter] public Action? BodyRendered { get; set; }
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<NotificationBell>(0);
            builder.CloseComponent();
            BodyRendered?.Invoke();
            builder.AddContent(1, "Page content");
        }
    }

    private sealed class PagedTableHost : ComponentBase
    {
        [Parameter] public Action<MudTable<int>>? Capture { get; set; }
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<MudTable<int>>(0);
            builder.AddAttribute(1, "Items", Enumerable.Range(1, 200));
            builder.AddAttribute(2, "RowsPerPage", 20);
            builder.AddAttribute(3, "RowTemplate", (RenderFragment<int>)(item => row =>
            {
                row.OpenComponent<MudTd>(0);
                row.AddAttribute(1, "ChildContent", (RenderFragment)(cell => cell.AddContent(0, $"row-{item:0000}")));
                row.CloseComponent();
            }));
            builder.AddAttribute(4, "PagerContent", (RenderFragment)(pager =>
            {
                pager.OpenComponent<MudTablePager>(0);
                pager.AddAttribute(1, "PageSizeOptions", new[] { 20, 50, 100 });
                pager.CloseComponent();
            }));
            builder.AddComponentReferenceCapture(5, instance => Capture?.Invoke((MudTable<int>)instance));
            builder.CloseComponent();
        }
    }

    private sealed class TestNotifications : INotificationRealtimeService
    {
        private Action? _changed;
        public int UnreadCount { get; private set; }
        public int Connections { get; private set; }
        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
        public event Action? Changed { add => _changed += value; remove => _changed -= value; }
        public void SetCount(int count) { UnreadCount = count; _changed?.Invoke(); }
        public Task EnsureConnectedAsync() { Connections++; return Task.CompletedTask; }
        public Task RefreshUnreadCountAsync() => Task.CompletedTask;
        public Task DisconnectAsync() { SetCount(0); return Task.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
