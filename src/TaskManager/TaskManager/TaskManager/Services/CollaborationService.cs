using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Hubs;
using TaskManager.Models;
using TaskManager.Shared.DTOs;

namespace TaskManager.Services;

public interface ICollaborationService
{
    Task NotifyUsersAsync(
        int organizationId,
        IEnumerable<int> recipientUserIds,
        string type,
        string title,
        string message,
        string? linkUrl = null,
        int? taskId = null,
        int? actorUserId = null,
        CancellationToken ct = default);

    Task BroadcastOrgEventAsync(int organizationId, RealtimeEventDto payload, CancellationToken ct = default);

    Task HandleNewCommentAsync(TaskItem task, Comment comment, string authorName, CancellationToken ct = default);

    Task HandleTaskStatusChangedAsync(
        TaskItem task,
        string oldStatus,
        string newStatus,
        int actorUserId,
        string actorName,
        CancellationToken ct = default);

    IReadOnlyList<string> ExtractMentions(string body);
}

public sealed class CollaborationService : ICollaborationService
{
    private static readonly Regex MentionRegex = new(
        @"@([A-Za-z0-9._-]{2,50})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ApplicationDbContext _context;
    private readonly IHubContext<TaskHub> _hub;

    public CollaborationService(ApplicationDbContext context, IHubContext<TaskHub> hub)
    {
        _context = context;
        _hub = hub;
    }

    public IReadOnlyList<string> ExtractMentions(string body) =>
        MentionRegex.Matches(body ?? string.Empty)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public async Task NotifyUsersAsync(
        int organizationId,
        IEnumerable<int> recipientUserIds,
        string type,
        string title,
        string message,
        string? linkUrl = null,
        int? taskId = null,
        int? actorUserId = null,
        CancellationToken ct = default)
    {
        var recipients = recipientUserIds.Distinct().Where(id => id != actorUserId).ToList();
        if (recipients.Count == 0)
            return;

        var now = DateTime.UtcNow;
        foreach (var userId in recipients)
        {
            _context.AppNotifications.Add(new AppNotification
            {
                OrganizationId = organizationId,
                UserId = userId,
                Type = type,
                Title = title,
                Message = message,
                LinkUrl = linkUrl,
                TaskId = taskId,
                ActorUserId = actorUserId,
                CreatedAt = now
            });
        }

        await _context.SaveChangesAsync(ct);

        var payload = new AppNotificationDto
        {
            Type = type,
            Title = title,
            Message = message,
            LinkUrl = linkUrl,
            TaskId = taskId,
            IsRead = false,
            CreatedAt = now
        };

        foreach (var userId in recipients)
        {
            await _hub.Clients.Group(TaskHub.UserGroup(userId))
                .SendAsync("NotificationReceived", payload, ct);
        }
    }

    public Task BroadcastOrgEventAsync(int organizationId, RealtimeEventDto payload, CancellationToken ct = default) =>
        _hub.Clients.Group(TaskHub.OrgGroup(organizationId))
            .SendAsync("WorkspaceEvent", payload, ct);

    public async Task HandleNewCommentAsync(
        TaskItem task, Comment comment, string authorName, CancellationToken ct = default)
    {
        var link = $"/user/task/{task.Id}";
        var mentionNames = ExtractMentions(comment.Body);
        var lowered = mentionNames.Select(n => n.ToLowerInvariant()).ToList();
        var mentionedUsers = lowered.Count == 0
            ? []
            : await _context.Users
                .Where(u => u.OrganizationId == task.OrganizationId &&
                            lowered.Contains(u.Username.ToLower()))
                .Select(u => u.Id)
                .ToListAsync(ct);

        if (mentionedUsers.Count > 0)
        {
            await NotifyUsersAsync(
                task.OrganizationId,
                mentionedUsers,
                NotificationTypes.Mention,
                "You were mentioned",
                $"{authorName} mentioned you on “{task.Title}”",
                link,
                task.Id,
                comment.AuthorId,
                ct);
        }

        var watchers = await _context.TaskWatchers
            .Where(w => w.TaskId == task.Id)
            .Select(w => w.UserId)
            .ToListAsync(ct);

        var recipients = new HashSet<int>(watchers);
        if (task.AssignedToId is int assignee)
            recipients.Add(assignee);

        recipients.ExceptWith(mentionedUsers);

        await NotifyUsersAsync(
            task.OrganizationId,
            recipients,
            NotificationTypes.Comment,
            "New comment",
            $"{authorName} commented on “{task.Title}”",
            link,
            task.Id,
            comment.AuthorId,
            ct);

        await BroadcastOrgEventAsync(task.OrganizationId, new RealtimeEventDto
        {
            EventType = "comment.created",
            TaskId = task.Id,
            ProjectId = task.ProjectId,
            Message = $"{authorName} commented on “{task.Title}”"
        }, ct);
    }

    public async Task HandleTaskStatusChangedAsync(
        TaskItem task,
        string oldStatus,
        string newStatus,
        int actorUserId,
        string actorName,
        CancellationToken ct = default)
    {
        var link = $"/user/task/{task.Id}";
        var recipients = new List<int>();
        if (task.AssignedToId is int assignee)
            recipients.Add(assignee);

        var watchers = await _context.TaskWatchers
            .Where(w => w.TaskId == task.Id)
            .Select(w => w.UserId)
            .ToListAsync(ct);
        recipients.AddRange(watchers);

        await NotifyUsersAsync(
            task.OrganizationId,
            recipients,
            NotificationTypes.TaskStatus,
            "Task status updated",
            $"{actorName} moved “{task.Title}” from {oldStatus} to {newStatus}",
            link,
            task.Id,
            actorUserId,
            ct);

        await BroadcastOrgEventAsync(task.OrganizationId, new RealtimeEventDto
        {
            EventType = "task.status_changed",
            TaskId = task.Id,
            ProjectId = task.ProjectId,
            Message = $"“{task.Title}” → {newStatus}"
        }, ct);
    }
}
