using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.Services;

namespace TaskManager.Services;

public interface IAuditService
{
    Task LogAsync(
        string action,
        string entityType,
        string? entityId = null,
        object? details = null,
        int? organizationId = null,
        CancellationToken ct = default,
        int? actorUserId = null,
        string? actorEmail = null);
}

public sealed class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantService _tenant;
    private readonly IHttpContextAccessor _http;

    public AuditService(ApplicationDbContext db, ITenantService tenant, IHttpContextAccessor http)
    {
        _db = db;
        _tenant = tenant;
        _http = http;
    }

    public async Task LogAsync(
        string action,
        string entityType,
        string? entityId = null,
        object? details = null,
        int? organizationId = null,
        CancellationToken ct = default,
        int? actorUserId = null,
        string? actorEmail = null)
    {
        var orgId = organizationId ?? _tenant.OrganizationId;
        var uid = actorUserId ?? _tenant.UserId;
        var email = actorEmail;
        if (email is null && uid is int resolvedUid)
        {
            email = await _db.Users.IgnoreQueryFilters()
                .Where(u => u.Id == resolvedUid)
                .Select(u => u.Email)
                .FirstOrDefaultAsync(ct);
        }

        var ip = _http.HttpContext?.Connection.RemoteIpAddress?.ToString();

        _db.AuditLogEntries.Add(new AuditLogEntry
        {
            OrganizationId = orgId,
            ActorUserId = uid,
            ActorEmail = email,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            DetailsJson = details is null ? null : JsonSerializer.Serialize(details),
            IpAddress = ip,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }
}
