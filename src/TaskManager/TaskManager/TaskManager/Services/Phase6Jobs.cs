using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TaskManager.Billing;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.Services.Billing;

namespace TaskManager.Services;

/// <summary>Hangfire entry points for Phase 6 recurring tasks and automations.</summary>
public class Phase6Jobs
{
    private readonly ApplicationDbContext _context;
    private readonly IEntitlementService _entitlements;
    private readonly ILogger<Phase6Jobs> _logger;

    public Phase6Jobs(
        ApplicationDbContext context,
        IEntitlementService entitlements,
        ILogger<Phase6Jobs> logger)
    {
        _context = context;
        _entitlements = entitlements;
        _logger = logger;
    }

    /// <summary>Spawn next instances for due recurrence templates.</summary>
    public async Task ProcessRecurringTasksAsync()
    {
        var now = DateTime.UtcNow;
        var templates = await _context.Tasks
            .IgnoreQueryFilters()
            .Where(t =>
                t.RecurrenceFrequency != "none" &&
                t.RecurrenceParentTaskId == null &&
                t.NextOccurrenceAt != null &&
                t.NextOccurrenceAt <= now &&
                (t.RecurrenceEndDate == null || t.RecurrenceEndDate >= now.Date))
            .Take(100)
            .ToListAsync();

        foreach (var template in templates)
        {
            try
            {
                var child = new TaskItem
                {
                    Title = template.Title,
                    Description = template.Description,
                    ProjectId = template.ProjectId,
                    OrganizationId = template.OrganizationId,
                    AssignedToId = template.AssignedToId,
                    AssignedById = template.AssignedById,
                    Status = template.AssignedToId.HasValue ? "Assigned" : "NotAssigned",
                    Priority = template.Priority,
                    EstimatedHours = template.EstimatedHours,
                    StartDate = now.Date,
                    DueDate = template.DueDate.HasValue
                        ? now.Date.Add(template.DueDate.Value.Date - (template.StartDate?.Date ?? template.CreatedAt.Date))
                        : now.Date.AddDays(1),
                    RecurrenceFrequency = "none",
                    RecurrenceParentTaskId = template.Id,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _context.Tasks.Add(child);

                template.NextOccurrenceAt = ComputeNext(template.RecurrenceFrequency, template.RecurrenceInterval, now);
                if (template.RecurrenceEndDate.HasValue &&
                    template.NextOccurrenceAt > template.RecurrenceEndDate.Value.Date.AddDays(1))
                {
                    template.NextOccurrenceAt = null;
                }

                template.UpdatedAt = now;
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to spawn recurrence for task {TaskId}", template.Id);
            }
        }
    }

    /// <summary>Fire due_soon automation rules once per day window.</summary>
    public async Task ProcessDueSoonAutomationsAsync()
    {
        var orgs = await _context.AutomationRules
            .IgnoreQueryFilters()
            .Where(r => r.IsEnabled && r.TriggerType == "due_soon")
            .Select(r => r.OrganizationId)
            .Distinct()
            .ToListAsync();

        foreach (var orgId in orgs)
        {
            if (!await _entitlements.HasFeatureAsync(orgId, FeatureKeys.Automations))
                continue;

            var rules = await _context.AutomationRules
                .IgnoreQueryFilters()
                .Where(r => r.OrganizationId == orgId && r.IsEnabled && r.TriggerType == "due_soon")
                .ToListAsync();

            foreach (var rule in rules)
            {
                var days = ReadInt(rule.TriggerConfigJson, "daysBeforeDue", 1);
                var target = DateTime.UtcNow.Date.AddDays(days);
                var tasks = await _context.Tasks
                    .IgnoreQueryFilters()
                    .Where(t => t.OrganizationId == orgId && t.DueDate != null && t.DueDate.Value.Date == target)
                    .Where(t => t.Status != "Completed" && t.Status != "Closed")
                    .Take(50)
                    .ToListAsync();

                foreach (var task in tasks)
                    await ApplyActionAsync(rule, task, orgId);
            }
        }
    }

    public async Task HandleTaskCreatedAsync(int taskId)
    {
        var task = await _context.Tasks.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == taskId);
        if (task is null) return;
        await RunRulesAsync(task, "task_created", null, null);
    }

    public async Task HandleTaskStatusChangedAsync(int taskId, string oldStatus, string newStatus)
    {
        var task = await _context.Tasks.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == taskId);
        if (task is null) return;
        await RunRulesAsync(task, "task_status_changed", oldStatus, newStatus);
    }

    private async Task RunRulesAsync(TaskItem task, string trigger, string? oldStatus, string? newStatus)
    {
        if (!await _entitlements.HasFeatureAsync(task.OrganizationId, FeatureKeys.Automations))
            return;

        var rules = await _context.AutomationRules
            .IgnoreQueryFilters()
            .Where(r => r.OrganizationId == task.OrganizationId && r.IsEnabled && r.TriggerType == trigger)
            .ToListAsync();

        foreach (var rule in rules)
        {
            if (trigger == "task_status_changed")
            {
                var toStatus = ReadString(rule.TriggerConfigJson, "toStatus");
                if (!string.IsNullOrWhiteSpace(toStatus) &&
                    !string.Equals(toStatus, newStatus, StringComparison.OrdinalIgnoreCase))
                    continue;

                var fromStatus = ReadString(rule.TriggerConfigJson, "fromStatus");
                if (!string.IsNullOrWhiteSpace(fromStatus) &&
                    !string.Equals(fromStatus, oldStatus, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            if (!await TryConsumeRunAsync(task.OrganizationId))
            {
                _logger.LogWarning("Automation run limit reached for org {OrgId}", task.OrganizationId);
                return;
            }

            await ApplyActionAsync(rule, task, task.OrganizationId);
        }
    }

    private async Task ApplyActionAsync(AutomationRule rule, TaskItem task, int orgId)
    {
        try
        {
            switch (rule.ActionType)
            {
                case "set_status":
                {
                    var status = ReadString(rule.ActionConfigJson, "status");
                    if (!string.IsNullOrWhiteSpace(status) && task.Status != status)
                    {
                        task.Status = status;
                        task.UpdatedAt = DateTime.UtcNow;
                        if (status is "Completed" or "Closed" or "Tested")
                            task.CompletedDate = DateTime.UtcNow;
                    }
                    break;
                }
                case "assign_user":
                {
                    var userId = ReadInt(rule.ActionConfigJson, "userId", 0);
                    if (userId > 0)
                    {
                        task.AssignedToId = userId;
                        if (task.Status == "NotAssigned")
                            task.Status = "Assigned";
                        task.UpdatedAt = DateTime.UtcNow;
                    }
                    break;
                }
                case "add_comment":
                {
                    var body = ReadString(rule.ActionConfigJson, "comment")
                               ?? $"Automation: {rule.Name}";
                    _context.Comments.Add(new Comment
                    {
                        TaskId = task.Id,
                        AuthorId = task.AssignedById,
                        Body = body,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                    break;
                }
            }

            rule.LastRunAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Automation {RuleId} failed on task {TaskId}", rule.Id, task.Id);
        }
    }

    private async Task<bool> TryConsumeRunAsync(int orgId)
    {
        var limit = await _entitlements.GetLimitAsync(orgId, LimitKeys.AutomationRunsPerMonth);
        if (limit is null)
            return true;

        var period = DateTime.UtcNow.ToString("yyyy-MM");
        var counter = await _context.UsageCounters
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c =>
                c.OrganizationId == orgId &&
                c.Key == LimitKeys.AutomationRunsPerMonth &&
                c.Period == period);

        if (counter is null)
        {
            counter = new UsageCounter
            {
                OrganizationId = orgId,
                Key = LimitKeys.AutomationRunsPerMonth,
                Period = period,
                Value = 0,
                UpdatedAt = DateTime.UtcNow
            };
            _context.UsageCounters.Add(counter);
        }

        if (counter.Value >= limit.Value)
            return false;

        counter.Value++;
        counter.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public static DateTime ComputeNext(string frequency, int interval, DateTime from) =>
        frequency.ToLowerInvariant() switch
        {
            "daily" => from.AddDays(Math.Max(1, interval)),
            "weekly" => from.AddDays(7 * Math.Max(1, interval)),
            "monthly" => from.AddMonths(Math.Max(1, interval)),
            _ => from.AddDays(1)
        };

    private static string? ReadString(string json, string prop)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return doc.RootElement.TryGetProperty(prop, out var el) ? el.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static int ReadInt(string json, string prop, int fallback)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (doc.RootElement.TryGetProperty(prop, out var el) && el.TryGetInt32(out var v))
                return v;
        }
        catch
        {
            // ignore
        }

        return fallback;
    }
}
