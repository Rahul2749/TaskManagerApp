using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.Shared.DTOs;

namespace TaskManager.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TasksController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TasksController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TaskDto>>> GetTasks(
            [FromQuery] int? projectId = null,
            [FromQuery] string? status = null,
            [FromQuery] int? assignedToId = null)
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

            IQueryable<TaskItem> query = _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssignedTo)
                .Include(t => t.AssignedBy);

            // Filter based on role
            if (currentUserRole == "User")
            {
                // Users only see tasks assigned to them
                query = query.Where(t => t.AssignedToId == currentUserId);
            }
            else if (currentUserRole == "Manager")
            {
                // Managers see tasks in their projects
                query = query.Where(t => t.Project.ManagerId == currentUserId);
            }
            // Admins see all tasks

            // Apply filters
            if (projectId.HasValue)
                query = query.Where(t => t.ProjectId == projectId.Value);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(t => t.Status == status);

            if (assignedToId.HasValue)
                query = query.Where(t => t.AssignedToId == assignedToId.Value);

            var tasks = await query
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var taskDtos = tasks.Select(MapToTaskDto).ToList();

            return Ok(taskDtos);
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
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (currentUserRole == "User" && task.AssignedToId != currentUserId)
                return Forbid();

            if (currentUserRole == "Manager")
            {
                var project = await _context.Projects.FindAsync(task.ProjectId);
                if (project?.ManagerId != currentUserId)
                    return Forbid();
            }

            var taskDto = MapToTaskDto(task);
            taskDto.History = task.History.Select(h => new TaskHistoryDto
            {
                Id = h.Id,
                FieldName = h.FieldName,
                OldValue = h.OldValue,
                NewValue = h.NewValue,
                Comment = h.Comment,
                ChangedAt = h.ChangedAt,
                ChangedBy = MapToUserDto(h.ChangedBy)
            }).ToList();

            return Ok(taskDto);
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpPost]
        public async Task<ActionResult<TaskDto>> CreateTask([FromBody] TaskDto taskDto)
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

            // Verify project exists and user has access
            var project = await _context.Projects.FindAsync(taskDto.ProjectId);
            if (project == null)
                return NotFound("Project not found");

            if (currentUserRole == "Manager" && project.ManagerId != currentUserId)
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
            await _context.Entry(task)
                .Reference(t => t.Project)
                .LoadAsync();
            await _context.Entry(task)
                .Reference(t => t.AssignedTo)
                .LoadAsync();
            await _context.Entry(task)
                .Reference(t => t.AssignedBy)
                .LoadAsync();

            return CreatedAtAction(nameof(GetTask), new { id = task.Id }, MapToTaskDto(task));
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpPut("{id}")]
        public async Task<ActionResult<TaskDto>> UpdateTask(int id, [FromBody] TaskDto taskDto)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
                return NotFound();

            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (currentUserRole == "Manager" && task.Project.ManagerId != currentUserId)
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

            if (taskDto.Status == "Completed" || taskDto.Status == "Tested" || taskDto.Status == "Closed")
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

            return Ok(MapToTaskDto(task));
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

            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

            // Users can only update their own tasks
            if (currentUserRole == "User" && task.AssignedToId != currentUserId)
                return Forbid();

            // Managers can update tasks in their projects
            if (currentUserRole == "Manager" && task.Project.ManagerId != currentUserId)
                return Forbid();

            var oldStatus = task.Status;
            task.Status = statusDto.Status;
            task.UpdatedAt = DateTime.UtcNow;

            if (statusDto.ActualHours.HasValue)
            {
                task.ActualHours = statusDto.ActualHours.Value;
            }

            if (statusDto.Status == "Completed" || statusDto.Status == "Tested" || statusDto.Status == "Closed")
            {
                task.CompletedDate = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Add history entry
            await AddTaskHistory(task.Id, currentUserId, "Status", oldStatus, statusDto.Status, statusDto.Comment);

            return Ok(MapToTaskDto(task));
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteTask(int id)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
                return NotFound();

            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (currentUserRole == "Manager" && task.Project.ManagerId != currentUserId)
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

        private TaskDto MapToTaskDto(TaskItem task)
        {
            return new TaskDto
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                ProjectId = task.ProjectId,
                ProjectName = task.Project?.Name,
                AssignedToId = task.AssignedToId,
                AssignedTo = task.AssignedTo != null ? MapToUserDto(task.AssignedTo) : null,
                AssignedById = task.AssignedById,
                AssignedBy = task.AssignedBy != null ? MapToUserDto(task.AssignedBy) : null,
                Status = task.Status,
                Priority = task.Priority,
                EstimatedHours = task.EstimatedHours,
                ActualHours = task.ActualHours,
                StartDate = task.StartDate,
                DueDate = task.DueDate,
                CompletedDate = task.CompletedDate,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt
            };
        }

        private static UserDto MapToUserDto(User user)
        {
            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            };
        }
    }
}
