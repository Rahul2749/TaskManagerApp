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
[Route("api/dependencies")]
[ApiController]
public class TaskDependenciesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantService _tenant;
    private readonly IEntitlementService _entitlements;

    public TaskDependenciesController(
        ApplicationDbContext context,
        ITenantService tenant,
        IEntitlementService entitlements)
    {
        _context = context;
        _tenant = tenant;
        _entitlements = entitlements;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TaskDependencyDto>>> List(
        [FromQuery] int? projectId = null,
        [FromQuery] int? taskId = null,
        CancellationToken ct = default)
    {
        if (!await EnsureGanttAsync(ct))
            return UpgradeRequired();

        var query = _context.TaskDependencies
            .Include(d => d.PredecessorTask)
            .Include(d => d.SuccessorTask)
            .AsQueryable();

        if (taskId.HasValue)
            query = query.Where(d => d.PredecessorTaskId == taskId || d.SuccessorTaskId == taskId);

        if (projectId.HasValue)
            query = query.Where(d =>
                d.PredecessorTask.ProjectId == projectId || d.SuccessorTask.ProjectId == projectId);

        var items = await query.OrderBy(d => d.Id).ToListAsync(ct);
        return Ok(items.Select(ToDto));
    }

    [HttpPost]
    public async Task<ActionResult<TaskDependencyDto>> Create(
        [FromBody] CreateTaskDependencyDto dto,
        CancellationToken ct = default)
    {
        if (!await EnsureGanttAsync(ct))
            return UpgradeRequired();

        if (_tenant.OrganizationId is not int orgId)
            return BadRequest("Tenant context required");

        if (dto.PredecessorTaskId == dto.SuccessorTaskId)
            return BadRequest("A task cannot depend on itself.");

        var pred = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == dto.PredecessorTaskId, ct);
        var succ = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == dto.SuccessorTaskId, ct);
        if (pred is null || succ is null)
            return NotFound("One or both tasks were not found.");

        if (await WouldCreateCycleAsync(dto.PredecessorTaskId, dto.SuccessorTaskId, ct))
            return BadRequest("Adding this dependency would create a cycle.");

        var type = string.IsNullOrWhiteSpace(dto.DependencyType) ? "FS" : dto.DependencyType.Trim().ToUpperInvariant();
        if (type is not ("FS" or "SS" or "FF" or "SF"))
            return BadRequest("DependencyType must be FS, SS, FF, or SF.");

        var exists = await _context.TaskDependencies.AnyAsync(d =>
            d.PredecessorTaskId == dto.PredecessorTaskId && d.SuccessorTaskId == dto.SuccessorTaskId, ct);
        if (exists)
            return Conflict("Dependency already exists.");

        var edge = new TaskDependency
        {
            OrganizationId = orgId,
            PredecessorTaskId = dto.PredecessorTaskId,
            SuccessorTaskId = dto.SuccessorTaskId,
            DependencyType = type,
            LagDays = dto.LagDays,
            CreatedAt = DateTime.UtcNow
        };

        _context.TaskDependencies.Add(edge);
        await _context.SaveChangesAsync(ct);

        edge.PredecessorTask = pred;
        edge.SuccessorTask = succ;
        return Ok(ToDto(edge));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        if (!await EnsureGanttAsync(ct))
            return UpgradeRequired();

        var edge = await _context.TaskDependencies.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (edge is null)
            return NotFound();

        _context.TaskDependencies.Remove(edge);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<bool> WouldCreateCycleAsync(int predecessorId, int successorId, CancellationToken ct)
    {
        // If successor can already reach predecessor, adding pred→succ creates a cycle.
        var visited = new HashSet<int>();
        var stack = new Stack<int>();
        stack.Push(successorId);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == predecessorId)
                return true;
            if (!visited.Add(current))
                continue;

            var next = await _context.TaskDependencies
                .Where(d => d.PredecessorTaskId == current)
                .Select(d => d.SuccessorTaskId)
                .ToListAsync(ct);
            foreach (var n in next)
                stack.Push(n);
        }

        return false;
    }

    private async Task<bool> EnsureGanttAsync(CancellationToken ct) =>
        _tenant.IsSuperAdmin ||
        await _entitlements.HasFeatureAsync(_tenant.OrganizationId, FeatureKeys.TimelineGantt, ct);

    private ObjectResult UpgradeRequired() =>
        StatusCode(StatusCodes.Status402PaymentRequired, new ProblemDetails
        {
            Title = "Upgrade required",
            Detail = "Timeline and dependencies require Professional or higher.",
            Status = StatusCodes.Status402PaymentRequired
        });

    private static TaskDependencyDto ToDto(TaskDependency d) => new()
    {
        Id = d.Id,
        PredecessorTaskId = d.PredecessorTaskId,
        PredecessorTitle = d.PredecessorTask?.Title ?? string.Empty,
        SuccessorTaskId = d.SuccessorTaskId,
        SuccessorTitle = d.SuccessorTask?.Title ?? string.Empty,
        DependencyType = d.DependencyType,
        LagDays = d.LagDays
    };
}
