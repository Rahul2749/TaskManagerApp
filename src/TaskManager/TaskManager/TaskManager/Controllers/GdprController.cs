using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Controllers;

[Authorize(Roles = "OrganizationAdmin")]
[Route("api/gdpr")]
[ApiController]
public class GdprController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantService _tenant;
    private readonly IAuditService _audit;

    public GdprController(ApplicationDbContext db, ITenantService tenant, IAuditService audit)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
    }

    /// <summary>Download a JSON export of the current organization's portable data.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        if (_tenant.OrganizationId is not int orgId)
            return BadRequest("Tenant context required");

        var org = await _db.Organizations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == orgId, ct);
        if (org is null) return NotFound();

        var users = await _db.Users.IgnoreQueryFilters()
            .Where(u => u.OrganizationId == orgId)
            .Select(u => new
            {
                u.Id, u.Username, u.Email, u.FirstName, u.LastName, u.Role, u.IsActive, u.CreatedAt
            })
            .ToListAsync(ct);

        var projects = await _db.Projects
            .Select(p => new { p.Id, p.Name, p.Description, p.Status, p.CreatedAt })
            .ToListAsync(ct);

        var tasks = await _db.Tasks
            .Select(t => new
            {
                t.Id, t.Title, t.Description, t.Status, t.Priority, t.ProjectId,
                t.AssignedToId, t.DueDate, t.CreatedAt, t.UpdatedAt
            })
            .ToListAsync(ct);

        var comments = await _db.Comments
            .Select(c => new { c.Id, c.TaskId, c.AuthorId, c.Body, c.CreatedAt })
            .ToListAsync(ct);

        var timeEntries = await _db.TimeEntries
            .Select(e => new { e.Id, e.TaskId, e.UserId, e.WorkDate, e.Hours, e.Notes, e.CreatedAt })
            .ToListAsync(ct);

        var payload = new
        {
            exportedAt = DateTime.UtcNow,
            organization = new { org.Id, org.Name, org.Slug, org.Description, org.CreatedAt },
            users,
            projects,
            tasks,
            comments,
            timeEntries
        };

        await _audit.LogAsync("gdpr.export", "Organization", orgId.ToString(), new { userCount = users.Count, taskCount = tasks.Count }, orgId, ct);

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);
        return File(bytes, "application/json", $"taskmanager-export-{org.Slug}-{DateTime.UtcNow:yyyyMMdd}.json");
    }
}
