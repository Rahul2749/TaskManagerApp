using System.Security.Cryptography;
using System.Text.Json;
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

[Authorize(Roles = "OrganizationAdmin,Manager")]
[Route("api/integrations")]
[ApiController]
public class IntegrationsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantService _tenant;
    private readonly IEntitlementService _entitlements;

    public IntegrationsController(ApplicationDbContext db, ITenantService tenant, IEntitlementService entitlements)
    {
        _db = db;
        _tenant = tenant;
        _entitlements = entitlements;
    }

    // ── Outbound webhooks ──────────────────────────────────────────────────

    [HttpGet("webhooks")]
    public async Task<ActionResult<IEnumerable<OutboundWebhookDto>>> ListWebhooks(CancellationToken ct)
    {
        if (!await EnsureIntegrationsAsync(ct)) return UpgradeRequired();
        var items = await _db.OutboundWebhooks.OrderByDescending(w => w.CreatedAt).ToListAsync(ct);
        return Ok(items.Select(ToWebhookDto));
    }

    [HttpPost("webhooks")]
    public async Task<ActionResult<CreateOutboundWebhookResultDto>> CreateWebhook(
        [FromBody] UpsertOutboundWebhookDto dto, CancellationToken ct)
    {
        if (!await EnsureIntegrationsAsync(ct)) return UpgradeRequired();
        if (_tenant.OrganizationId is not int orgId)
            return BadRequest("Tenant context required");

        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var entity = new OutboundWebhook
        {
            OrganizationId = orgId,
            Name = dto.Name.Trim(),
            TargetUrl = dto.TargetUrl.Trim(),
            Secret = secret,
            Events = string.IsNullOrWhiteSpace(dto.Events) ? "*" : dto.Events.Trim(),
            IsEnabled = dto.IsEnabled,
            CreatedAt = DateTime.UtcNow
        };
        _db.OutboundWebhooks.Add(entity);
        await _db.SaveChangesAsync(ct);

        return Ok(new CreateOutboundWebhookResultDto
        {
            Webhook = ToWebhookDto(entity),
            Secret = secret
        });
    }

    [HttpPut("webhooks/{id:int}")]
    public async Task<ActionResult<OutboundWebhookDto>> UpdateWebhook(
        int id, [FromBody] UpsertOutboundWebhookDto dto, CancellationToken ct)
    {
        if (!await EnsureIntegrationsAsync(ct)) return UpgradeRequired();
        var entity = await _db.OutboundWebhooks.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (entity is null) return NotFound();

        entity.Name = dto.Name.Trim();
        entity.TargetUrl = dto.TargetUrl.Trim();
        entity.Events = string.IsNullOrWhiteSpace(dto.Events) ? "*" : dto.Events.Trim();
        entity.IsEnabled = dto.IsEnabled;
        await _db.SaveChangesAsync(ct);
        return Ok(ToWebhookDto(entity));
    }

    [HttpDelete("webhooks/{id:int}")]
    public async Task<IActionResult> DeleteWebhook(int id, CancellationToken ct)
    {
        if (!await EnsureIntegrationsAsync(ct)) return UpgradeRequired();
        var entity = await _db.OutboundWebhooks.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (entity is null) return NotFound();
        _db.OutboundWebhooks.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("webhooks/{id:int}/deliveries")]
    public async Task<ActionResult<IEnumerable<WebhookDeliveryDto>>> Deliveries(int id, CancellationToken ct)
    {
        if (!await EnsureIntegrationsAsync(ct)) return UpgradeRequired();
        var items = await _db.WebhookDeliveries
            .Where(d => d.OutboundWebhookId == id)
            .OrderByDescending(d => d.CreatedAt)
            .Take(40)
            .ToListAsync(ct);
        return Ok(items.Select(d => new WebhookDeliveryDto
        {
            Id = d.Id,
            EventType = d.EventType,
            AttemptCount = d.AttemptCount,
            LastStatusCode = d.LastStatusCode,
            Succeeded = d.Succeeded,
            CreatedAt = d.CreatedAt,
            LastError = d.LastError
        }));
    }

    // ── Slack / GitHub connections ─────────────────────────────────────────

    [HttpGet("connections")]
    public async Task<ActionResult<IEnumerable<IntegrationConnectionDto>>> ListConnections(CancellationToken ct)
    {
        if (!await EnsureIntegrationsAsync(ct)) return UpgradeRequired();
        var items = await _db.IntegrationConnections.OrderByDescending(c => c.CreatedAt).ToListAsync(ct);
        return Ok(items.Select(ToConnectionDto));
    }

    [HttpPost("connections")]
    public async Task<ActionResult<IntegrationConnectionDto>> CreateConnection(
        [FromBody] UpsertIntegrationConnectionDto dto, CancellationToken ct)
    {
        if (!await EnsureIntegrationsAsync(ct)) return UpgradeRequired();
        if (_tenant.OrganizationId is not int orgId)
            return BadRequest("Tenant context required");

        var provider = dto.Provider.Trim().ToLowerInvariant();
        if (provider is not ("slack" or "github" or "custom"))
            return BadRequest("Provider must be slack, github, or custom.");

        var entity = new IntegrationConnection
        {
            OrganizationId = orgId,
            Provider = provider,
            Name = dto.Name.Trim(),
            ConfigJson = JsonSerializer.Serialize(new { webhookUrl = dto.WebhookUrl.Trim() }),
            IsEnabled = dto.IsEnabled,
            CreatedAt = DateTime.UtcNow
        };
        _db.IntegrationConnections.Add(entity);
        await _db.SaveChangesAsync(ct);
        return Ok(ToConnectionDto(entity));
    }

    [HttpDelete("connections/{id:int}")]
    public async Task<IActionResult> DeleteConnection(int id, CancellationToken ct)
    {
        if (!await EnsureIntegrationsAsync(ct)) return UpgradeRequired();
        var entity = await _db.IntegrationConnections.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return NotFound();
        _db.IntegrationConnections.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<bool> EnsureIntegrationsAsync(CancellationToken ct) =>
        _tenant.IsSuperAdmin ||
        await _entitlements.HasFeatureAsync(_tenant.OrganizationId, FeatureKeys.Integrations, ct);

    private ObjectResult UpgradeRequired() =>
        StatusCode(StatusCodes.Status402PaymentRequired, new ProblemDetails
        {
            Title = "Upgrade required",
            Detail = "Integrations require Professional or higher.",
            Status = StatusCodes.Status402PaymentRequired
        });

    private static OutboundWebhookDto ToWebhookDto(OutboundWebhook w) => new()
    {
        Id = w.Id,
        Name = w.Name,
        TargetUrl = w.TargetUrl,
        Events = w.Events,
        IsEnabled = w.IsEnabled,
        CreatedAt = w.CreatedAt,
        LastDeliveredAt = w.LastDeliveredAt
    };

    private static IntegrationConnectionDto ToConnectionDto(IntegrationConnection c)
    {
        string? url = null;
        try
        {
            using var doc = JsonDocument.Parse(c.ConfigJson);
            if (doc.RootElement.TryGetProperty("webhookUrl", out var el))
                url = el.GetString();
        }
        catch { /* ignore */ }

        return new IntegrationConnectionDto
        {
            Id = c.Id,
            Provider = c.Provider,
            Name = c.Name,
            WebhookUrl = url,
            IsEnabled = c.IsEnabled,
            CreatedAt = c.CreatedAt
        };
    }
}
