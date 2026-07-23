using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Authorization;
using TaskManager.Billing;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.Services;
using TaskManager.Services.Billing;
using TaskManager.Shared.DTOs;

namespace TaskManager.Controllers;

[Authorize(Roles = "OrganizationAdmin,Manager")]
[Route("api/api-keys")]
[ApiController]
public class ApiKeysController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantService _tenant;
    private readonly IEntitlementService _entitlements;
    private readonly IAuditService _audit;

    public ApiKeysController(
        ApplicationDbContext db,
        ITenantService tenant,
        IEntitlementService entitlements,
        IAuditService audit)
    {
        _db = db;
        _tenant = tenant;
        _entitlements = entitlements;
        _audit = audit;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrganizationApiKeyDto>>> List(CancellationToken ct)
    {
        if (!await EnsurePublicApiAsync(ct)) return UpgradeRequired();
        var items = await _db.OrganizationApiKeys.OrderByDescending(k => k.CreatedAt).ToListAsync(ct);
        return Ok(items.Select(ToDto));
    }

    [HttpPost]
    public async Task<ActionResult<CreateApiKeyResultDto>> Create([FromBody] CreateApiKeyDto dto, CancellationToken ct)
    {
        if (!await EnsurePublicApiAsync(ct)) return UpgradeRequired();
        if (_tenant.OrganizationId is not int orgId || _tenant.UserId is not int userId)
            return BadRequest("Tenant context required");

        var (plaintext, prefix, hash) = ApiKeyAuthenticationHandler.GenerateKey();
        var entity = new OrganizationApiKey
        {
            OrganizationId = orgId,
            Name = dto.Name.Trim(),
            KeyPrefix = prefix,
            KeyHash = hash,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };
        _db.OrganizationApiKeys.Add(entity);
        await _db.SaveChangesAsync(ct);

        try
        {
            await _audit.LogAsync("api_key.created", "OrganizationApiKey", entity.Id.ToString(), new { entity.Name }, orgId, ct);
        }
        catch { /* non-fatal */ }

        return Ok(new CreateApiKeyResultDto
        {
            Key = ToDto(entity),
            PlaintextKey = plaintext
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Revoke(int id, CancellationToken ct)
    {
        if (!await EnsurePublicApiAsync(ct)) return UpgradeRequired();
        var key = await _db.OrganizationApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (key is null) return NotFound();
        key.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        try
        {
            await _audit.LogAsync("api_key.revoked", "OrganizationApiKey", key.Id.ToString(), new { key.Name }, key.OrganizationId, ct);
        }
        catch { /* non-fatal */ }

        return NoContent();
    }

    private async Task<bool> EnsurePublicApiAsync(CancellationToken ct) =>
        _tenant.IsSuperAdmin ||
        await _entitlements.HasFeatureAsync(_tenant.OrganizationId, FeatureKeys.PublicApi, ct);

    private ObjectResult UpgradeRequired() =>
        StatusCode(StatusCodes.Status402PaymentRequired, new ProblemDetails
        {
            Title = "Upgrade required",
            Detail = "Public API keys require Professional or higher.",
            Status = StatusCodes.Status402PaymentRequired
        });

    private static OrganizationApiKeyDto ToDto(OrganizationApiKey k) => new()
    {
        Id = k.Id,
        Name = k.Name,
        KeyPrefix = k.KeyPrefix + "…",
        CreatedAt = k.CreatedAt,
        LastUsedAt = k.LastUsedAt,
        IsRevoked = k.RevokedAt.HasValue
    };
}
