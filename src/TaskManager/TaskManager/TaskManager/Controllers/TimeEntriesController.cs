using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Billing;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.Services;
using TaskManager.Services.Billing;
using TaskManager.Shared.DTOs;

namespace TaskManager.Controllers;

[Authorize]
[Route("api/time-entries")]
[ApiController]
public class TimeEntriesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantService _tenant;
    private readonly IEntitlementService _entitlements;

    public TimeEntriesController(
        ApplicationDbContext context,
        ITenantService tenant,
        IEntitlementService entitlements)
    {
        _context = context;
        _tenant = tenant;
        _entitlements = entitlements;
    }

    [HttpGet]
    public async Task<ActionResult<TimesheetSummaryDto>> List(
        [FromQuery] DateTime? weekStart = null,
        [FromQuery] int? userId = null,
        [FromQuery] int? taskId = null,
        CancellationToken ct = default)
    {
        if (!await EnsureFeatureAsync(ct))
            return UpgradeRequired();

        var start = (weekStart ?? DateTime.UtcNow.Date).Date;
        // Normalize to Monday
        var diff = ((int)start.DayOfWeek + 6) % 7;
        start = start.AddDays(-diff);
        var end = start.AddDays(7);

        var query = _context.TimeEntries
            .Include(e => e.Task)
            .Include(e => e.User)
            .Where(e => e.WorkDate >= start && e.WorkDate < end);

        var role = _tenant.Role;
        var currentUserId = _tenant.UserId;
        if (role == Roles.User)
            query = query.Where(e => e.UserId == currentUserId);
        else if (userId.HasValue)
            query = query.Where(e => e.UserId == userId);

        if (taskId.HasValue)
            query = query.Where(e => e.TaskId == taskId);

        var entries = await query.OrderByDescending(e => e.WorkDate).ThenByDescending(e => e.Id).ToListAsync(ct);
        return Ok(new TimesheetSummaryDto
        {
            WeekStart = start,
            TotalHours = entries.Sum(e => e.Hours),
            Entries = entries.Select(ToDto).ToList()
        });
    }

    [HttpGet("task/{taskId:int}")]
    public async Task<ActionResult<IEnumerable<TimeEntryDto>>> ForTask(int taskId, CancellationToken ct = default)
    {
        if (!await EnsureFeatureAsync(ct))
            return UpgradeRequired();

        var entries = await _context.TimeEntries
            .Include(e => e.User)
            .Include(e => e.Task)
            .Where(e => e.TaskId == taskId)
            .OrderByDescending(e => e.WorkDate)
            .ToListAsync(ct);

        return Ok(entries.Select(ToDto));
    }

    [HttpPost]
    public async Task<ActionResult<TimeEntryDto>> Create(
        [FromBody] CreateTimeEntryDto dto,
        CancellationToken ct = default)
    {
        if (!await EnsureFeatureAsync(ct))
            return UpgradeRequired();

        if (_tenant.OrganizationId is not int orgId || _tenant.UserId is not int userId)
            return BadRequest("Tenant context required");

        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == dto.TaskId, ct);
        if (task is null)
            return NotFound("Task not found.");

        if (_tenant.Role == Roles.User && task.AssignedToId != userId)
            return Forbid();

        var entry = new TimeEntry
        {
            OrganizationId = orgId,
            TaskId = dto.TaskId,
            UserId = userId,
            WorkDate = dto.WorkDate.Date,
            Hours = Math.Round(dto.Hours, 2),
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.TimeEntries.Add(entry);

        var total = await _context.TimeEntries
            .Where(e => e.TaskId == dto.TaskId)
            .SumAsync(e => (decimal?)e.Hours, ct) ?? 0;
        task.ActualHours = total + entry.Hours;
        task.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        await _context.Entry(entry).Reference(e => e.User).LoadAsync(ct);
        await _context.Entry(entry).Reference(e => e.Task).LoadAsync(ct);
        return Ok(ToDto(entry));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        if (!await EnsureFeatureAsync(ct))
            return UpgradeRequired();

        var entry = await _context.TimeEntries.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entry is null)
            return NotFound();

        if (_tenant.Role == Roles.User && entry.UserId != _tenant.UserId)
            return Forbid();

        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == entry.TaskId, ct);
        _context.TimeEntries.Remove(entry);
        if (task is not null)
        {
            task.ActualHours = Math.Max(0, task.ActualHours - entry.Hours);
            task.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<bool> EnsureFeatureAsync(CancellationToken ct) =>
        _tenant.IsSuperAdmin ||
        await _entitlements.HasFeatureAsync(_tenant.OrganizationId, FeatureKeys.TimeTracking, ct);

    private ObjectResult UpgradeRequired() =>
        StatusCode(StatusCodes.Status402PaymentRequired, new ProblemDetails
        {
            Title = "Upgrade required",
            Detail = "Time tracking requires Professional or higher.",
            Status = StatusCodes.Status402PaymentRequired
        });

    private static TimeEntryDto ToDto(TimeEntry e) => new()
    {
        Id = e.Id,
        TaskId = e.TaskId,
        TaskTitle = e.Task?.Title,
        UserId = e.UserId,
        UserName = e.User is null ? null : $"{e.User.FirstName} {e.User.LastName}".Trim(),
        WorkDate = e.WorkDate,
        Hours = e.Hours,
        Notes = e.Notes,
        CreatedAt = e.CreatedAt
    };
}
