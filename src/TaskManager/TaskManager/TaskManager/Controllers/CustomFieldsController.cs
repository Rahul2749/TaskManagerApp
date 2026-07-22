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
[Route("api/custom-fields")]
[ApiController]
public class CustomFieldsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantService _tenant;
    private readonly IEntitlementService _entitlements;

    public CustomFieldsController(
        ApplicationDbContext context,
        ITenantService tenant,
        IEntitlementService entitlements)
    {
        _context = context;
        _tenant = tenant;
        _entitlements = entitlements;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CustomFieldDefinitionDto>>> List(
        [FromQuery] int? projectId = null,
        CancellationToken ct = default)
    {
        if (!await EnsureFeatureAsync(ct))
            return UpgradeRequired();

        var query = _context.CustomFieldDefinitions.AsQueryable();
        if (projectId.HasValue)
            query = query.Where(d => d.ProjectId == null || d.ProjectId == projectId);
        else
            query = query.Where(d => d.ProjectId == null);

        var items = await query
            .OrderBy(d => d.SortOrder).ThenBy(d => d.Name)
            .ToListAsync(ct);

        return Ok(items.Select(ToDto));
    }

    [HttpPost]
    public async Task<ActionResult<CustomFieldDefinitionDto>> Create(
        [FromBody] UpsertCustomFieldDefinitionDto dto,
        CancellationToken ct = default)
    {
        if (!await EnsureFeatureAsync(ct))
            return UpgradeRequired();

        if (_tenant.OrganizationId is not int orgId)
            return BadRequest("Tenant context required");

        if (!IsValidType(dto.FieldType))
            return BadRequest("FieldType must be text, number, date, or select.");

        var def = new CustomFieldDefinition
        {
            OrganizationId = orgId,
            ProjectId = dto.ProjectId,
            Name = dto.Name.Trim(),
            FieldType = dto.FieldType.Trim().ToLowerInvariant(),
            OptionsJson = dto.OptionsJson,
            SortOrder = dto.SortOrder,
            IsRequired = dto.IsRequired,
            CreatedAt = DateTime.UtcNow
        };

        _context.CustomFieldDefinitions.Add(def);
        await _context.SaveChangesAsync(ct);
        return Ok(ToDto(def));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CustomFieldDefinitionDto>> Update(
        int id,
        [FromBody] UpsertCustomFieldDefinitionDto dto,
        CancellationToken ct = default)
    {
        if (!await EnsureFeatureAsync(ct))
            return UpgradeRequired();

        var def = await _context.CustomFieldDefinitions.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (def is null)
            return NotFound();

        if (!IsValidType(dto.FieldType))
            return BadRequest("FieldType must be text, number, date, or select.");

        def.Name = dto.Name.Trim();
        def.FieldType = dto.FieldType.Trim().ToLowerInvariant();
        def.ProjectId = dto.ProjectId;
        def.OptionsJson = dto.OptionsJson;
        def.SortOrder = dto.SortOrder;
        def.IsRequired = dto.IsRequired;
        await _context.SaveChangesAsync(ct);
        return Ok(ToDto(def));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        if (!await EnsureFeatureAsync(ct))
            return UpgradeRequired();

        var def = await _context.CustomFieldDefinitions.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (def is null)
            return NotFound();

        _context.CustomFieldDefinitions.Remove(def);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [Authorize(Roles = "OrganizationAdmin,Manager,User")]
    [HttpGet("tasks/{taskId:int}")]
    public async Task<ActionResult<IEnumerable<CustomFieldValueDto>>> GetTaskValues(int taskId, CancellationToken ct = default)
    {
        if (!await EnsureFeatureAsync(ct))
            return UpgradeRequired();

        var task = await _context.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task is null)
            return NotFound();

        var definitions = await _context.CustomFieldDefinitions
            .Where(d => d.ProjectId == null || d.ProjectId == task.ProjectId)
            .OrderBy(d => d.SortOrder)
            .ToListAsync(ct);

        var values = await _context.CustomFieldValues
            .Where(v => v.TaskId == taskId)
            .ToDictionaryAsync(v => v.DefinitionId, v => v.Value, ct);

        return Ok(definitions.Select(d => new CustomFieldValueDto
        {
            DefinitionId = d.Id,
            Name = d.Name,
            FieldType = d.FieldType,
            OptionsJson = d.OptionsJson,
            IsRequired = d.IsRequired,
            Value = values.GetValueOrDefault(d.Id)
        }));
    }

    [Authorize(Roles = "OrganizationAdmin,Manager,User")]
    [HttpPut("tasks/{taskId:int}")]
    public async Task<IActionResult> SetTaskValues(
        int taskId,
        [FromBody] SetCustomFieldValuesDto dto,
        CancellationToken ct = default)
    {
        if (!await EnsureFeatureAsync(ct))
            return UpgradeRequired();

        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task is null)
            return NotFound();

        if (_tenant.Role == Roles.User && task.AssignedToId != _tenant.UserId)
            return Forbid();

        var allowedIds = await _context.CustomFieldDefinitions
            .Where(d => d.ProjectId == null || d.ProjectId == task.ProjectId)
            .Select(d => d.Id)
            .ToListAsync(ct);

        var existing = await _context.CustomFieldValues
            .Where(v => v.TaskId == taskId)
            .ToListAsync(ct);

        foreach (var input in dto.Values.Where(v => allowedIds.Contains(v.DefinitionId)))
        {
            var row = existing.FirstOrDefault(e => e.DefinitionId == input.DefinitionId);
            if (row is null)
            {
                _context.CustomFieldValues.Add(new CustomFieldValue
                {
                    TaskId = taskId,
                    DefinitionId = input.DefinitionId,
                    Value = input.Value
                });
            }
            else
            {
                row.Value = input.Value;
            }
        }

        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<bool> EnsureFeatureAsync(CancellationToken ct) =>
        _tenant.IsSuperAdmin ||
        await _entitlements.HasFeatureAsync(_tenant.OrganizationId, FeatureKeys.CustomFields, ct);

    private ObjectResult UpgradeRequired() =>
        StatusCode(StatusCodes.Status402PaymentRequired, new ProblemDetails
        {
            Title = "Upgrade required",
            Detail = "Custom fields require Professional or higher.",
            Status = StatusCodes.Status402PaymentRequired
        });

    private static bool IsValidType(string type) =>
        type is "text" or "number" or "date" or "select";

    private static CustomFieldDefinitionDto ToDto(CustomFieldDefinition d) => new()
    {
        Id = d.Id,
        ProjectId = d.ProjectId,
        Name = d.Name,
        FieldType = d.FieldType,
        OptionsJson = d.OptionsJson,
        SortOrder = d.SortOrder,
        IsRequired = d.IsRequired
    };
}
