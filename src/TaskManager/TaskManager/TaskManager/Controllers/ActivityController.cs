using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Controllers;

[Authorize]
[Route("api/activity")]
[ApiController]
public class ActivityController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantService _tenant;

    public ActivityController(ApplicationDbContext context, ITenantService tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ActivityItemDto>>> GetFeed(
        [FromQuery] int take = 40,
        CancellationToken ct = default)
    {
        if (_tenant.OrganizationId is not int orgId)
            return BadRequest("Organization context required");

        take = Math.Clamp(take, 1, 100);

        var history = await _context.TaskHistories
            .AsNoTracking()
            .Include(h => h.ChangedBy)
            .Include(h => h.Task).ThenInclude(t => t.Project)
            .Where(h => h.Task.OrganizationId == orgId)
            .OrderByDescending(h => h.ChangedAt)
            .Take(take)
            .ToListAsync(ct);

        var items = history.Select(h => new ActivityItemDto
        {
            Kind = "task_change",
            Summary = FormatHistory(h.FieldName, h.OldValue, h.NewValue),
            ActorName = h.ChangedBy is null
                ? null
                : $"{h.ChangedBy.FirstName} {h.ChangedBy.LastName}".Trim(),
            TaskId = h.TaskId,
            TaskTitle = h.Task?.Title,
            ProjectId = h.Task?.ProjectId,
            ProjectName = h.Task?.Project?.Name,
            OccurredAt = h.ChangedAt
        }).ToList();

        return Ok(items);
    }

    private static string FormatHistory(string field, string? oldValue, string? newValue)
    {
        if (string.Equals(field, "Status", StringComparison.OrdinalIgnoreCase))
            return $"changed status from {oldValue ?? "—"} to {newValue ?? "—"}";
        if (string.Equals(field, "Created", StringComparison.OrdinalIgnoreCase))
            return "created the task";
        return $"updated {field}";
    }
}
