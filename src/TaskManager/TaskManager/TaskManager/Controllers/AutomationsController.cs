using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Billing;
using TaskManager.Data;
using TaskManager.Helpers;
using TaskManager.Models;
using TaskManager.Services;
using TaskManager.Services.Billing;
using TaskManager.Shared.DTOs;

namespace TaskManager.Controllers;

[Authorize(Roles = "OrganizationAdmin,Manager")]
[Route("api/automations")]
[ApiController]
public class AutomationsController : ControllerBase
{
    private static readonly HashSet<string> Triggers =
        ["task_created", "task_status_changed", "due_soon"];

    private static readonly HashSet<string> Actions =
        ["set_status", "add_comment", "assign_user"];

    private readonly ApplicationDbContext _context;
    private readonly ITenantService _tenant;
    private readonly IEntitlementService _entitlements;

    public AutomationsController(
        ApplicationDbContext context,
        ITenantService tenant,
        IEntitlementService entitlements)
    {
        _context = context;
        _tenant = tenant;
        _entitlements = entitlements;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AutomationRuleDto>>> List(CancellationToken ct = default)
    {
        if (!await EnsureFeatureAsync(ct))
            return UpgradeRequired();

        var items = await _context.AutomationRules
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
        return Ok(items.Select(ToDto));
    }

    [HttpPost]
    public async Task<ActionResult<AutomationRuleDto>> Create(
        [FromBody] UpsertAutomationRuleDto dto,
        CancellationToken ct = default)
    {
        if (!await EnsureFeatureAsync(ct))
            return UpgradeRequired();

        if (_tenant.OrganizationId is not int orgId)
            return BadRequest("Tenant context required");

        if (!TryValidate(dto, out var error))
            return BadRequest(error);

        if (!await IsValidAssigneeAsync(dto, ct))
            return BadRequest("Assignee must be an active user in this organization.");

        var rule = new AutomationRule
        {
            OrganizationId = orgId,
            Name = dto.Name.Trim(),
            TriggerType = dto.TriggerType.Trim().ToLowerInvariant(),
            TriggerConfigJson = NormalizeJson(dto.TriggerConfigJson),
            ActionType = dto.ActionType.Trim().ToLowerInvariant(),
            ActionConfigJson = NormalizeJson(dto.ActionConfigJson),
            IsEnabled = dto.IsEnabled,
            CreatedAt = DateTime.UtcNow
        };

        _context.AutomationRules.Add(rule);
        await _context.SaveChangesAsync(ct);
        return Ok(ToDto(rule));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AutomationRuleDto>> Update(
        int id,
        [FromBody] UpsertAutomationRuleDto dto,
        CancellationToken ct = default)
    {
        if (!await EnsureFeatureAsync(ct))
            return UpgradeRequired();

        var rule = await _context.AutomationRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null)
            return NotFound();

        if (!TryValidate(dto, out var error))
            return BadRequest(error);

        if (!await IsValidAssigneeAsync(dto, ct))
            return BadRequest("Assignee must be an active user in this organization.");

        rule.Name = dto.Name.Trim();
        rule.TriggerType = dto.TriggerType.Trim().ToLowerInvariant();
        rule.TriggerConfigJson = NormalizeJson(dto.TriggerConfigJson);
        rule.ActionType = dto.ActionType.Trim().ToLowerInvariant();
        rule.ActionConfigJson = NormalizeJson(dto.ActionConfigJson);
        rule.IsEnabled = dto.IsEnabled;

        await _context.SaveChangesAsync(ct);
        return Ok(ToDto(rule));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        if (!await EnsureFeatureAsync(ct))
            return UpgradeRequired();

        var rule = await _context.AutomationRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null)
            return NotFound();

        _context.AutomationRules.Remove(rule);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<bool> EnsureFeatureAsync(CancellationToken ct) =>
        _tenant.IsSuperAdmin ||
        await _entitlements.HasFeatureAsync(_tenant.OrganizationId, FeatureKeys.Automations, ct);

    private ObjectResult UpgradeRequired() =>
        StatusCode(StatusCodes.Status402PaymentRequired, new ProblemDetails
        {
            Title = "Upgrade required",
            Detail = "Automations require Professional or higher.",
            Status = StatusCodes.Status402PaymentRequired
        });

    private static bool TryValidate(UpsertAutomationRuleDto dto, out string? error)
    {
        error = null;
        var trigger = dto.TriggerType.Trim().ToLowerInvariant();
        var action = dto.ActionType.Trim().ToLowerInvariant();
        if (!Triggers.Contains(trigger))
        {
            error = "TriggerType must be task_created, task_status_changed, or due_soon.";
            return false;
        }

        if (!Actions.Contains(action))
        {
            error = "ActionType must be set_status, add_comment, or assign_user.";
            return false;
        }

        try
        {
            using var triggerDoc = JsonDocument.Parse(NormalizeJson(dto.TriggerConfigJson));
            using var actionDoc = JsonDocument.Parse(NormalizeJson(dto.ActionConfigJson));
            if (triggerDoc.RootElement.ValueKind != JsonValueKind.Object || actionDoc.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "TriggerConfigJson and ActionConfigJson must be JSON objects.";
                return false;
            }
            if (action == "set_status" && (!actionDoc.RootElement.TryGetProperty("status", out var status) ||
                status.ValueKind != JsonValueKind.String || status.GetString() is not
                    ("NotAssigned" or "Assigned" or "InProgress" or "Completed" or "Tested" or "Closed")))
            {
                error = "The set_status action requires a valid task status.";
                return false;
            }
            if (action == "assign_user" && (!actionDoc.RootElement.TryGetProperty("userId", out var userId) ||
                !userId.TryGetInt32(out var id) || id <= 0))
            {
                error = "The assign_user action requires a positive userId.";
                return false;
            }
        }
        catch
        {
            error = "TriggerConfigJson and ActionConfigJson must be valid JSON.";
            return false;
        }

        return true;
    }

    private static string NormalizeJson(string? json) =>
        string.IsNullOrWhiteSpace(json) ? "{}" : json.Trim();

    private async Task<bool> IsValidAssigneeAsync(UpsertAutomationRuleDto dto, CancellationToken ct)
    {
        if (!string.Equals(dto.ActionType.Trim(), "assign_user", StringComparison.OrdinalIgnoreCase)) return true;
        using var doc = JsonDocument.Parse(dto.ActionConfigJson);
        var userId = doc.RootElement.GetProperty("userId").GetInt32();
        return _tenant.OrganizationId is int orgId &&
            await TaskAssignments.IsValidAssigneeAsync(_context, orgId, userId, ct);
    }

    private static AutomationRuleDto ToDto(AutomationRule r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        TriggerType = r.TriggerType,
        TriggerConfigJson = r.TriggerConfigJson,
        ActionType = r.ActionType,
        ActionConfigJson = r.ActionConfigJson,
        IsEnabled = r.IsEnabled,
        CreatedAt = r.CreatedAt,
        LastRunAt = r.LastRunAt
    };
}
