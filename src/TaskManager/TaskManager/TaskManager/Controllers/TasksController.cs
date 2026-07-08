using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Mapping;
using TaskManager.Models;
using TaskManager.Pagination;
using TaskManager.Services;
using TaskManager.Shared.DTOs;
using TaskManager.Shared.Pagination;

namespace TaskManager.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TasksController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ITenantService _tenant;

        public TasksController(ApplicationDbContext context, ITenantService tenant)
        {
            _context = context;
            _tenant = tenant;
        }

        [HttpGet]
        public async Task<ActionResult> GetTasks(
            [FromQuery] int? projectId = null,
            [FromQuery] string? status = null,
            [FromQuery] int? assignedToId = null,
            [FromQuery] int? pageNumber = null,
            [FromQuery] int? pageSize = null)
        {
            var currentUserId = _tenant.UserId!.Value;
            var currentUserRole = _tenant.Role;

            IQueryable<TaskItem> query = _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssignedTo)
                .Include(t => t.AssignedBy);

            // Filter based on role (tenant filter already applied by EF query filter)
            if (currentUserRole == Roles.User)
            {
                query = query.Where(t => t.AssignedToId == currentUserId);
            }
            else if (currentUserRole == Roles.Manager)
            {
                query = query.Where(t => t.Project.ManagerId == currentUserId);
            }
            // SuperAdmin / OrganizationAdmin see all tasks within their tenant

            // Apply filters
            if (projectId.HasValue)
                query = query.Where(t => t.ProjectId == projectId.Value);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(t => t.Status == status);

            if (assignedToId.HasValue)
                query = query.Where(t => t.AssignedToId == assignedToId.Value);

            query = query.OrderByDescending(t => t.CreatedAt);

            // Backward-compatible: when no paging is requested, return the plain list (the
            // existing Mobile/Client code expects an array). When paging is requested,
            // return a PagedResult<TaskDto> so the UI can render controls.
            if (pageNumber.HasValue || pageSize.HasValue)
            {
                var page = await query.ToPagedResultAsync(
                    pageNumber ?? 1,
                    pageSize ?? 20);

                var paged = new PagedResult<TaskDto>
                {
                    Items = page.Items.Select(t => t.ToDto()).ToList(),
                    PageNumber = page.PageNumber,
                    PageSize = page.PageSize,
                    TotalCount = page.TotalCount
                };
                return Ok(paged);
            }

            var tasks = await query.ToListAsync();
            return Ok(tasks.Select(t => t.ToDto()).ToList());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<TaskDto>> GetTask(int id)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssignedTo)
                .Include(t => t.AssignedBy)
                .Include(t => t.History)
                    .ThenInclude(h => h.ChangedBy)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
                return NotFound();

            // Check access rights
            var currentUserId = _tenant.UserId!.Value;
            var currentUserRole = _tenant.Role;

            if (currentUserRole == Roles.User && task.AssignedToId != currentUserId)
                return Forbid();

            if (currentUserRole == Roles.Manager && task.Project.ManagerId != currentUserId)
                return Forbid();

            var taskDto = task.ToDto();
            taskDto.History = task.History.Select(h => h.ToDto()).ToList();

            return Ok(taskDto);
        }

        [Authorize(Roles = "SuperAdmin,OrganizationAdmin,Manager,Admin")]
        [HttpPost]
        public async Task<ActionResult<TaskDto>> CreateTask([FromBody] TaskDto taskDto)
        {
            var currentUserId = _tenant.UserId!.Value;
            var currentUserRole = _tenant.Role;

            // Verify project exists and user has access
            var project = await _context.Projects.FindAsync(taskDto.ProjectId);
            if (project == null)
                return NotFound("Project not found");

            if (currentUserRole == Roles.Manager && project.ManagerId != currentUserId)
                return Forbid("You can only create tasks in your projects");

            // Verify assigned user if specified
            if (taskDto.AssignedToId.HasValue)
            {
                var assignedUser = await _context.Users.FindAsync(taskDto.AssignedToId.Value);
                if (assignedUser == null)
                    return NotFound("Assigned user not found");

                // Verify user is assigned to the project
                var isUserInProject = await _context.ProjectUsers
                    .AnyAsync(pu => pu.ProjectId == taskDto.ProjectId && pu.UserId == taskDto.AssignedToId.Value);

                if (!isUserInProject)
                    return BadRequest("User is not assigned to this project");
            }

            var task = new TaskItem
            {
                Title = taskDto.Title,
                Description = taskDto.Description,
                ProjectId = taskDto.ProjectId,
                // Denormalized tenant key from the owning project
                OrganizationId = project.OrganizationId,
                AssignedToId = taskDto.AssignedToId,
                AssignedById = currentUserId,
                Status = taskDto.AssignedToId.HasValue ? "Assigned" : "NotAssigned",
                Priority = taskDto.Priority,
                EstimatedHours = taskDto.EstimatedHours,
                ActualHours = 0,
                StartDate = taskDto.StartDate,
                DueDate = taskDto.DueDate,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            // Add history entry
            await AddTaskHistory(task.Id, currentUserId, "Created", null, "Task created");

            // Reload with navigation properties
            await _context.Entry(task).Reference(t => t.Project).LoadAsync();
            await _context.Entry(task).Reference(t => t.AssignedTo).LoadAsync();
            await _context.Entry(task).Reference(t => t.AssignedBy).LoadAsync();

            return CreatedAtAction(nameof(GetTask), new { id = task.Id }, task.ToDto());
        }

        [Authorize(Roles = "SuperAdmin,OrganizationAdmin,Manager,Admin")]
        [HttpPut("{id}")]
        public async Task<ActionResult<TaskDto>> UpdateTask(int id, [FromBody] TaskDto taskDto)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
                return NotFound();

            var currentUserId = _tenant.UserId!.Value;
            var currentUserRole = _tenant.Role;

            if (currentUserRole == Roles.Manager && task.Project.ManagerId != currentUserId)
                return Forbid();

            // Track changes for history
            var changes = new List<(string field, string oldValue, string newValue)>();

            if (task.Title != taskDto.Title)
                changes.Add(("Title", task.Title, taskDto.Title));

            if (task.Status != taskDto.Status)
                changes.Add(("Status", task.Status, taskDto.Status));

            if (task.Priority != taskDto.Priority)
                changes.Add(("Priority", task.Priority, taskDto.Priority));

            if (task.AssignedToId != taskDto.AssignedToId)
            {
                var oldUser = task.AssignedToId.HasValue ?
                    (await _context.Users.FindAsync(task.AssignedToId.Value))?.Username : "Unassigned";
                var newUser = taskDto.AssignedToId.HasValue ?
                    (await _context.Users.FindAsync(taskDto.AssignedToId.Value))?.Username : "Unassigned";
                changes.Add(("AssignedTo", oldUser ?? "Unassigned", newUser ?? "Unassigned"));
            }

            // Update task
            task.Title = taskDto.Title;
            task.Description = taskDto.Description;
            task.AssignedToId = taskDto.AssignedToId;
            task.Status = taskDto.Status;
            task.Priority = taskDto.Priority;
            task.EstimatedHours = taskDto.EstimatedHours;
            task.StartDate = taskDto.StartDate;
            task.DueDate = taskDto.DueDate;
            task.UpdatedAt = DateTime.UtcNow;

            if (TaskItemExtensions.IsCompletedStatus(taskDto.Status))
            {
                task.CompletedDate = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Add history entries
            foreach (var change in changes)
            {
                await AddTaskHistory(task.Id, currentUserId, change.field, change.oldValue, change.newValue);
            }

            // Reload navigation properties
            await _context.Entry(task).Reference(t => t.AssignedTo).LoadAsync();
            await _context.Entry(task).Reference(t => t.AssignedBy).LoadAsync();

            return Ok(task.ToDto());
        }

        [HttpPut("{id}/status")]
        public async Task<ActionResult<TaskDto>> UpdateTaskStatus(int id, [FromBody] UpdateTaskStatusDto statusDto)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssignedTo)
                .Include(t => t.AssignedBy)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
                return NotFound();

            var currentUserId = _tenant.UserId!.Value;
            var currentUserRole = _tenant.Role;

            // Users can only update their own tasks
            if (currentUserRole == Roles.User && task.AssignedToId != currentUserId)
                return Forbid();

            // Managers can update tasks in their projects
            if (currentUserRole == Roles.Manager && task.Project.ManagerId != currentUserId)
                return Forbid();

            var oldStatus = task.Status;
            task.Status = statusDto.Status;
            task.UpdatedAt = DateTime.UtcNow;

            if (statusDto.ActualHours.HasValue)
            {
                task.ActualHours = statusDto.ActualHours.Value;
            }

            if (TaskItemExtensions.IsCompletedStatus(statusDto.Status))
            {
                task.CompletedDate = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Add history entry
            await AddTaskHistory(task.Id, currentUserId, "Status", oldStatus, statusDto.Status, statusDto.Comment);

            return Ok(task.ToDto());
        }

        [Authorize(Roles = "SuperAdmin,OrganizationAdmin,Manager,Admin")]
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteTask(int id)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
                return NotFound();

            var currentUserId = _tenant.UserId!.Value;
            var currentUserRole = _tenant.Role;

            if (currentUserRole == Roles.Manager && task.Project.ManagerId != currentUserId)
                return Forbid();

            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task AddTaskHistory(int taskId, int changedById, string fieldName, string? oldValue, string? newValue, string? comment = null)
        {
            var history = new TaskHistory
            {
                TaskId = taskId,
                ChangedById = changedById,
                FieldName = fieldName,
                OldValue = oldValue,
                NewValue = newValue,
                Comment = comment,
                ChangedAt = DateTime.UtcNow
            };

            _context.TaskHistories.Add(history);
            await _context.SaveChangesAsync();
        }
    }
}

