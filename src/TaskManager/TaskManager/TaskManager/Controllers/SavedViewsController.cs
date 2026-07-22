using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Controllers;

[Authorize]
[Route("api/saved-views")]
[ApiController]
public class SavedViewsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantService _tenant;

    public SavedViewsController(ApplicationDbContext context, ITenantService tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SavedViewDto>>> List([FromQuery] string entityType = "task", CancellationToken ct = default)
    {
        if (_tenant.UserId is not int userId || _tenant.OrganizationId is not int orgId)
            return BadRequest("Tenant context required");

        var views = await _context.SavedViews
            .Where(v => v.UserId == userId && v.OrganizationId == orgId && v.EntityType == entityType)
            .OrderBy(v => v.Name)
            .Select(v => new SavedViewDto
            {
                Id = v.Id,
                Name = v.Name,
                EntityType = v.EntityType,
                FiltersJson = v.FiltersJson,
                CreatedAt = v.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(views);
    }

    [HttpPost]
    public async Task<ActionResult<SavedViewDto>> Create([FromBody] CreateSavedViewDto dto, CancellationToken ct = default)
    {
        if (_tenant.UserId is not int userId || _tenant.OrganizationId is not int orgId)
            return BadRequest("Tenant context required");

        var view = new SavedView
        {
            OrganizationId = orgId,
            UserId = userId,
            Name = dto.Name.Trim(),
            EntityType = string.IsNullOrWhiteSpace(dto.EntityType) ? "task" : dto.EntityType.Trim().ToLowerInvariant(),
            FiltersJson = string.IsNullOrWhiteSpace(dto.FiltersJson) ? "{}" : dto.FiltersJson,
            CreatedAt = DateTime.UtcNow
        };

        _context.SavedViews.Add(view);
        await _context.SaveChangesAsync(ct);

        return Ok(new SavedViewDto
        {
            Id = view.Id,
            Name = view.Name,
            EntityType = view.EntityType,
            FiltersJson = view.FiltersJson,
            CreatedAt = view.CreatedAt
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        if (_tenant.UserId is not int userId)
            return BadRequest("Tenant context required");

        var view = await _context.SavedViews.FirstOrDefaultAsync(v => v.Id == id && v.UserId == userId, ct);
        if (view is null)
            return NotFound();

        _context.SavedViews.Remove(view);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }
}
