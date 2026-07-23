using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Authorization;
using TaskManager.Billing;
using TaskManager.Data;
using TaskManager.Mapping;
using TaskManager.Models;
using TaskManager.Services;
using TaskManager.Services.Billing;
using TaskManager.Shared.DTOs;

namespace TaskManager.Controllers;

/// <summary>Public REST API authenticated with organization API keys (X-Api-Key or Bearer tm_…).</summary>
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
[Route("api/v1")]
[ApiController]
public class PublicApiV1Controller : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantService _tenant;
    private readonly IEntitlementService _entitlements;
    private readonly IOutboundEventPublisher _events;

    public PublicApiV1Controller(
        ApplicationDbContext db,
        ITenantService tenant,
        IEntitlementService entitlements,
        IOutboundEventPublisher events)
    {
        _db = db;
        _tenant = tenant;
        _entitlements = entitlements;
        _events = events;
    }

    [HttpGet("projects")]
    public async Task<ActionResult<IEnumerable<object>>> Projects(CancellationToken ct)
    {
        if (await GateAsync(ct) is { } err) return err;
        var items = await _db.Projects
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.Description, p.Status })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("tasks")]
    public async Task<ActionResult<IEnumerable<TaskDto>>> Tasks(
        [FromQuery] int? projectId = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        if (await GateAsync(ct) is { } err) return err;
        var query = _db.Tasks.Include(t => t.Project).Include(t => t.AssignedTo).AsQueryable();
        if (projectId.HasValue) query = query.Where(t => t.ProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(t => t.Status == status);
        var items = await query.OrderByDescending(t => t.UpdatedAt).Take(200).ToListAsync(ct);
        return Ok(items.Select(t => t.ToDto()));
    }

    [HttpGet("tasks/{id:int}")]
    public async Task<ActionResult<TaskDto>> TaskById(int id, CancellationToken ct)
    {
        if (await GateAsync(ct) is { } err) return err;
        var task = await _db.Tasks
            .Include(t => t.Project)
            .Include(t => t.AssignedTo)
            .Include(t => t.AssignedBy)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        return task is null ? NotFound() : Ok(task.ToDto());
    }

    [HttpPost("tasks")]
    public async Task<ActionResult<TaskDto>> CreateTask([FromBody] TaskDto dto, CancellationToken ct)
    {
        if (await GateAsync(ct) is { } err) return err;
        if (_tenant.OrganizationId is not int orgId)
            return BadRequest("Tenant context required");

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == dto.ProjectId, ct);
        if (project is null) return BadRequest("Project not found.");

        var admin = await _db.Users
            .IgnoreQueryFilters()
            .Where(u => u.OrganizationId == orgId && u.Role == Roles.OrganizationAdmin)
            .OrderBy(u => u.Id)
            .FirstOrDefaultAsync(ct);

        var task = new TaskItem
        {
            Title = dto.Title.Trim(),
            Description = dto.Description,
            ProjectId = dto.ProjectId,
            OrganizationId = orgId,
            AssignedToId = dto.AssignedToId,
            AssignedById = admin?.Id ?? 0,
            Status = dto.AssignedToId.HasValue ? "Assigned" : "NotAssigned",
            Priority = string.IsNullOrWhiteSpace(dto.Priority) ? "Medium" : dto.Priority,
            EstimatedHours = dto.EstimatedHours,
            StartDate = dto.StartDate,
            DueDate = dto.DueDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (task.AssignedById == 0)
            return BadRequest("Organization has no admin user to attribute task creation.");

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync(ct);

        await _events.PublishAsync(orgId, WebhookEvents.TaskCreated, new
        {
            id = task.Id,
            title = task.Title,
            projectId = task.ProjectId,
            status = task.Status,
            priority = task.Priority
        }, ct);

        await _db.Entry(task).Reference(t => t.Project).LoadAsync(ct);
        return CreatedAtAction(nameof(TaskById), new { id = task.Id }, task.ToDto());
    }

    [HttpPatch("tasks/{id:int}/status")]
    public async Task<ActionResult<TaskDto>> UpdateStatus(int id, [FromBody] UpdateTaskStatusDto dto, CancellationToken ct)
    {
        if (await GateAsync(ct) is { } err) return err;
        var task = await _db.Tasks.Include(t => t.Project).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null) return NotFound();

        var old = task.Status;
        task.Status = dto.Status;
        task.UpdatedAt = DateTime.UtcNow;
        if (dto.Status is "Completed" or "Closed" or "Tested")
            task.CompletedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _events.PublishAsync(task.OrganizationId, WebhookEvents.TaskStatusChanged, new
        {
            id = task.Id,
            title = task.Title,
            oldStatus = old,
            status = task.Status
        }, ct);

        if (task.Status is "Completed" or "Closed")
        {
            await _events.PublishAsync(task.OrganizationId, WebhookEvents.TaskCompleted, new
            {
                id = task.Id,
                title = task.Title,
                status = task.Status
            }, ct);
        }

        return Ok(task.ToDto());
    }

    private async Task<ActionResult?> GateAsync(CancellationToken ct)
    {
        if (!await _entitlements.HasFeatureAsync(_tenant.OrganizationId, FeatureKeys.PublicApi, ct))
        {
            return StatusCode(StatusCodes.Status402PaymentRequired, new ProblemDetails
            {
                Title = "Upgrade required",
                Detail = "Public API requires Professional or higher.",
                Status = StatusCodes.Status402PaymentRequired
            });
        }

        if (_tenant.OrganizationId is not int orgId)
            return BadRequest("Tenant context required");

        if (!await _entitlements.TryConsumeApiCallAsync(orgId, ct))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails
            {
                Title = "API limit reached",
                Detail = "Monthly API call limit reached for your plan.",
                Status = StatusCodes.Status429TooManyRequests
            });
        }

        return null;
    }
}
