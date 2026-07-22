using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TaskManager.Hubs;

[Authorize]
public class TaskHub : Hub
{
    public const string HubPath = "/hubs/tasks";

    public override async Task OnConnectedAsync()
    {
        var orgId = Context.User?.FindFirst("organizationId")?.Value;
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? Context.User?.FindFirst("sub")?.Value;

        if (!string.IsNullOrWhiteSpace(orgId))
            await Groups.AddToGroupAsync(Context.ConnectionId, OrgGroup(orgId));

        if (!string.IsNullOrWhiteSpace(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));

        await base.OnConnectedAsync();
    }

    public static string OrgGroup(int organizationId) => $"org:{organizationId}";
    public static string OrgGroup(string organizationId) => $"org:{organizationId}";
    public static string UserGroup(int userId) => $"user:{userId}";
    public static string UserGroup(string userId) => $"user:{userId}";
}
