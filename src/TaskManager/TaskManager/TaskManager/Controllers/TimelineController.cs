using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Billing;
using TaskManager.Data;
using TaskManager.Services;
using TaskManager.Services.Billing;
using TaskManager.Shared.DTOs;

namespace TaskManager.Controllers;

[Authorize]
[Route("api/timeline")]
[ApiController]
public class TimelineController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantService _tenant;
    private readonly IEntitlementService _entitlements;

    public TimelineController(
        ApplicationDbContext context,
        ITenantService tenant,
        IEntitlementService entitlements)
    {
        _context = context;
        _tenant = tenant;
        _entitlements = entitlements;
    }

    [HttpGet]
    public async Task<ActionResult<TimelineDto>> Get(
        [FromQuery] int? projectId = null,
        CancellationToken ct = default)
    {
        if (!_tenant.IsSuperAdmin &&
            !await _entitlements.HasFeatureAsync(_tenant.OrganizationId, FeatureKeys.TimelineGantt, ct))
        {
            return StatusCode(StatusCodes.Status402PaymentRequired, new ProblemDetails
            {
                Title = "Upgrade required",
                Detail = "Timeline & Gantt require Professional or higher.",
                Status = StatusCodes.Status402PaymentRequired
            });
        }

        var taskQuery = _context.Tasks
            .Include(t => t.Project)
            .Include(t => t.AssignedTo)
            .AsQueryable();

        if (projectId.HasValue)
            taskQuery = taskQuery.Where(t => t.ProjectId == projectId);

        var tasks = await taskQuery.OrderBy(t => t.StartDate ?? t.CreatedAt).ToListAsync(ct);
        var taskIds = tasks.Select(t => t.Id).ToHashSet();

        var deps = await _context.TaskDependencies
            .Include(d => d.PredecessorTask)
            .Include(d => d.SuccessorTask)
            .Where(d => taskIds.Contains(d.PredecessorTaskId) && taskIds.Contains(d.SuccessorTaskId))
            .ToListAsync(ct);

        return Ok(new TimelineDto
        {
            Tasks = tasks.Select(t =>
            {
                var start = t.StartDate?.Date ?? t.CreatedAt.Date;
                var end = t.DueDate?.Date ?? start.AddDays(1);
                if (end < start)
                    end = start.AddDays(1);

                return new TimelineTaskDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    ProjectId = t.ProjectId,
                    ProjectName = t.Project?.Name,
                    Status = t.Status,
                    Priority = t.Priority,
                    AssignedToId = t.AssignedToId,
                    AssignedToName = t.AssignedTo is null
                        ? null
                        : $"{t.AssignedTo.FirstName} {t.AssignedTo.LastName}".Trim(),
                    Start = start,
                    End = end
                };
            }).ToList(),
            Dependencies = deps.Select(d => new TaskDependencyDto
            {
                Id = d.Id,
                PredecessorTaskId = d.PredecessorTaskId,
                PredecessorTitle = d.PredecessorTask.Title,
                SuccessorTaskId = d.SuccessorTaskId,
                SuccessorTitle = d.SuccessorTask.Title,
                DependencyType = d.DependencyType,
                LagDays = d.LagDays
            }).ToList()
        });
    }
}
