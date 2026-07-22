using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Controllers;

[Authorize(Roles = "OrganizationAdmin,Manager")]
[Route("api/templates")]
[ApiController]
public class TemplatesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantService _tenant;

    public TemplatesController(ApplicationDbContext context, ITenantService tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    [HttpGet("tasks")]
    public async Task<ActionResult<IEnumerable<TaskTemplateDto>>> ListTaskTemplates(CancellationToken ct)
    {
        var items = await _context.TaskTemplates
            .OrderBy(t => t.Name)
            .Select(t => new TaskTemplateDto
            {
                Id = t.Id,
                Name = t.Name,
                Title = t.Title,
                Description = t.Description,
                Priority = t.Priority,
                SubtasksJson = t.SubtasksJson
            })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost("tasks")]
    public async Task<ActionResult<TaskTemplateDto>> CreateTaskTemplate(
        [FromBody] UpsertTaskTemplateDto dto, CancellationToken ct)
    {
        if (_tenant.OrganizationId is not int orgId)
            return BadRequest("Tenant context required");

        var entity = new TaskTemplate
        {
            OrganizationId = orgId,
            Name = dto.Name.Trim(),
            Title = dto.Title.Trim(),
            Description = dto.Description,
            Priority = dto.Priority,
            SubtasksJson = dto.SubtasksJson,
            CreatedAt = DateTime.UtcNow
        };
        _context.TaskTemplates.Add(entity);
        await _context.SaveChangesAsync(ct);
        return Ok(ToDto(entity));
    }

    [HttpDelete("tasks/{id:int}")]
    public async Task<IActionResult> DeleteTaskTemplate(int id, CancellationToken ct)
    {
        var entity = await _context.TaskTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is null) return NotFound();
        _context.TaskTemplates.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("tasks/{id:int}/apply")]
    public async Task<ActionResult<TaskDto>> ApplyTaskTemplate(
        int id, [FromBody] ApplyTaskTemplateDto dto, CancellationToken ct)
    {
        var template = await _context.TaskTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return NotFound();

        if (_tenant.OrganizationId is not int orgId || _tenant.UserId is not int userId)
            return BadRequest("Tenant context required");

        var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == dto.ProjectId, ct);
        if (project is null) return BadRequest("Project not found");

        var task = new TaskItem
        {
            OrganizationId = orgId,
            ProjectId = dto.ProjectId,
            Title = template.Title,
            Description = template.Description,
            Priority = template.Priority,
            Status = dto.AssignedToId.HasValue ? "Assigned" : "NotAssigned",
            AssignedToId = dto.AssignedToId,
            AssignedById = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Tasks.Add(task);
        await _context.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(template.SubtasksJson))
        {
            try
            {
                var titles = JsonSerializer.Deserialize<List<string>>(template.SubtasksJson) ?? [];
                foreach (var title in titles.Where(t => !string.IsNullOrWhiteSpace(t)))
                {
                    _context.Subtasks.Add(new Subtask
                    {
                        TaskId = task.Id,
                        Title = title.Trim(),
                        IsCompleted = false,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                await _context.SaveChangesAsync(ct);
            }
            catch (JsonException)
            {
                // ignore bad template JSON
            }
        }

        return Ok(new TaskDto
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            ProjectId = task.ProjectId,
            Status = task.Status,
            Priority = task.Priority,
            AssignedToId = task.AssignedToId
        });
    }

    [HttpGet("projects")]
    public async Task<ActionResult<IEnumerable<ProjectTemplateDto>>> ListProjectTemplates(CancellationToken ct)
    {
        var items = await _context.ProjectTemplates
            .OrderBy(t => t.Name)
            .Select(t => new ProjectTemplateDto
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                TasksJson = t.TasksJson
            })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost("projects")]
    public async Task<ActionResult<ProjectTemplateDto>> CreateProjectTemplate(
        [FromBody] UpsertProjectTemplateDto dto, CancellationToken ct)
    {
        if (_tenant.OrganizationId is not int orgId)
            return BadRequest("Tenant context required");

        var entity = new ProjectTemplate
        {
            OrganizationId = orgId,
            Name = dto.Name.Trim(),
            Description = dto.Description,
            TasksJson = dto.TasksJson,
            CreatedAt = DateTime.UtcNow
        };
        _context.ProjectTemplates.Add(entity);
        await _context.SaveChangesAsync(ct);
        return Ok(new ProjectTemplateDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            TasksJson = entity.TasksJson
        });
    }

    [HttpDelete("projects/{id:int}")]
    public async Task<IActionResult> DeleteProjectTemplate(int id, CancellationToken ct)
    {
        var entity = await _context.ProjectTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is null) return NotFound();
        _context.ProjectTemplates.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("projects/{id:int}/apply")]
    public async Task<ActionResult<ProjectDto>> ApplyProjectTemplate(
        int id, [FromBody] ApplyProjectTemplateDto dto, CancellationToken ct)
    {
        var template = await _context.ProjectTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return NotFound();

        if (_tenant.OrganizationId is not int orgId || _tenant.UserId is not int userId)
            return BadRequest("Tenant context required");

        var project = new Project
        {
            OrganizationId = orgId,
            Name = dto.ProjectName.Trim(),
            Description = dto.Description ?? template.Description,
            Status = "Active",
            ManagerId = userId,
            StartDate = DateTime.UtcNow.Date,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Projects.Add(project);
        await _context.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(template.TasksJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(template.TasksJson);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var title = el.TryGetProperty("title", out var t) ? t.GetString() : null;
                    if (string.IsNullOrWhiteSpace(title)) continue;
                    var priority = el.TryGetProperty("priority", out var p) ? p.GetString() ?? "Medium" : "Medium";
                    _context.Tasks.Add(new TaskItem
                    {
                        OrganizationId = orgId,
                        ProjectId = project.Id,
                        Title = title.Trim(),
                        Priority = priority,
                        Status = "NotAssigned",
                        AssignedById = userId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                await _context.SaveChangesAsync(ct);
            }
            catch (JsonException)
            {
                // ignore
            }
        }

        return Ok(new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            Status = project.Status,
            ManagerId = project.ManagerId,
            StartDate = project.StartDate
        });
    }

    private static TaskTemplateDto ToDto(TaskTemplate t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Title = t.Title,
        Description = t.Description,
        Priority = t.Priority,
        SubtasksJson = t.SubtasksJson
    };
}
