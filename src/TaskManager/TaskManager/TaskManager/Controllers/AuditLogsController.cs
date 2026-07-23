using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Billing;
using TaskManager.Data;
using TaskManager.Services;
using TaskManager.Services.Billing;
using TaskManager.Shared.DTOs;

namespace TaskManager.Controllers;

[Authorize(Roles = "OrganizationAdmin,Manager")]
[Route("api/audit-logs")]
[ApiController]
public class AuditLogsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantService _tenant;
    private readonly IEntitlementService _entitlements;

    public AuditLogsController(ApplicationDbContext db, ITenantService tenant, IEntitlementService entitlements)
    {
        _db = db;
        _tenant = tenant;
        _entitlements = entitlements;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AuditLogEntryDto>>> List(
        [FromQuery] int take = 100,
        [FromQuery] string? action = null,
        CancellationToken ct = default)
    {
        if (!_tenant.IsSuperAdmin &&
            !await _entitlements.HasFeatureAsync(_tenant.OrganizationId, FeatureKeys.AuditLog, ct))
        {
            return StatusCode(StatusCodes.Status402PaymentRequired, new ProblemDetails
            {
                Title = "Upgrade required",
                Detail = "Audit log requires Business or higher.",
                Status = StatusCodes.Status402PaymentRequired
            });
        }

        take = Math.Clamp(take, 1, 500);
        var query = _db.AuditLogEntries.AsQueryable();
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action == action);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

        return Ok(items.Select(a => new AuditLogEntryDto
        {
            Id = a.Id,
            Action = a.Action,
            EntityType = a.EntityType,
            EntityId = a.EntityId,
            ActorEmail = a.ActorEmail,
            ActorUserId = a.ActorUserId,
            DetailsJson = a.DetailsJson,
            IpAddress = a.IpAddress,
            CreatedAt = a.CreatedAt
        }));
    }
}
