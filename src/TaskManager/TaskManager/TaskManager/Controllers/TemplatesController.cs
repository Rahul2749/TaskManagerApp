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
[Route("api/templates")]
[ApiController]
public class TemplatesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantService _tenant;
    private readonly IEntitlementService _entitlements;

    public TemplatesController(ApplicationDbContext context, ITenantService tenant, IEntitlementService entitlements)
    {
        _context = context;
        _tenant = tenant;
        _entitlements = entitlements;
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

        if (!ValidPriority(dto.Priority) || !TryReadSubtasks(dto.SubtasksJson, out _))
            return BadRequest("Use a valid priority and a JSON array of subtask titles (up to 300 characters each).");

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

        if (!await _context.Projects.VisibleTo(_tenant).AnyAsync(p => p.Id == project.Id, ct))
            return Forbid();
        if (!ValidPriority(template.Priority) || !TryReadSubtasks(template.SubtasksJson, out var titles))
            return BadRequest("Template contains invalid priority or subtask data.");
        if (dto.AssignedToId is int assignee)
        {
            if (!await TaskAssignments.IsValidAssigneeAsync(_context, orgId, assignee, ct))
                return BadRequest("Assignee must be an active user in this organization.");
            await TaskAssignments.EnsureProjectMemberAsync(_context, project.Id, assignee, ct);
        }

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
        for (var i = 0; i < titles.Count; i++)
        {
            task.Subtasks.Add(new Subtask
            {
                Title = titles[i].Trim(),
                SortOrder = i + 1,
                CreatedAt = DateTime.UtcNow
            });
        }
        _context.Tasks.Add(task);
        await _context.SaveChangesAsync(ct);

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

        if (!TryReadTasks(dto.TasksJson, out _))
            return BadRequest("TasksJson must be a JSON array of tasks with valid titles and priorities.");

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

        if (!TryReadTasks(template.TasksJson, out var tasks))
            return BadRequest("Template contains invalid task data.");
        var projectCount = await _context.Projects.CountAsync(p => p.OrganizationId == orgId, ct);
        if (!await _entitlements.IsWithinLimitAsync(orgId, LimitKeys.MaxProjects, projectCount, ct: ct))
            return Problem(detail: "Project limit reached for your plan. Upgrade to create more projects.",
                statusCode: StatusCodes.Status402PaymentRequired);

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
        foreach (var input in tasks)
        {
            project.Tasks.Add(new TaskItem
            {
                OrganizationId = orgId,
                Title = input.Title.Trim(),
                Priority = input.Priority,
                Status = "NotAssigned",
                AssignedById = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        _context.Projects.Add(project);
        await _context.SaveChangesAsync(ct);

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

    private static bool ValidPriority(string? priority) => priority is "Low" or "Medium" or "High" or "Critical";

    private static bool TryReadSubtasks(string? json, out List<string> titles)
    {
        titles = [];
        if (string.IsNullOrWhiteSpace(json)) return true;
        try
        {
            titles = JsonSerializer.Deserialize<List<string>>(json) ?? [];
            return titles.All(t => !string.IsNullOrWhiteSpace(t) && t.Length <= 300);
        }
        catch (JsonException) { return false; }
    }

    private static bool TryReadTasks(string? json, out List<TemplateTask> tasks)
    {
        tasks = [];
        if (string.IsNullOrWhiteSpace(json)) return true;
        try
        {
            tasks = JsonSerializer.Deserialize<List<TemplateTask>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            return tasks.All(t => t is not null && !string.IsNullOrWhiteSpace(t.Title) &&
                t.Title.Length <= 300 && ValidPriority(t.Priority));
        }
        catch (JsonException) { return false; }
    }

    private sealed class TemplateTask
    {
        public string Title { get; set; } = string.Empty;
        public string Priority { get; set; } = "Medium";
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
