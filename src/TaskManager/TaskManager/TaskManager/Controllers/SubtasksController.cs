using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Mapping;
using TaskManager.Models;
using TaskManager.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Controllers
{
    [Authorize]
    [Route("api/tasks/{taskId}/subtasks")]
    [ApiController]
    public class SubtasksController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ITenantService _tenant;

        public SubtasksController(ApplicationDbContext context, ITenantService tenant)
        {
            _context = context;
            _tenant = tenant;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SubtaskDto>>> GetSubtasks(int taskId)
        {
            if (!await TaskAccessible(taskId))
                return NotFound();

            var subtasks = await _context.Subtasks
                .Include(s => s.AssignedTo)
                .Where(s => s.TaskId == taskId)
                .OrderBy(s => s.SortOrder)
                .ToListAsync();

            return Ok(subtasks.Select(s => s.ToDto()).ToList());
        }

        [Authorize(Roles = "SuperAdmin,OrganizationAdmin,Manager,Admin")]
        [HttpPost]
        public async Task<ActionResult<SubtaskDto>> CreateSubtask(int taskId, [FromBody] SubtaskDto dto)
        {
            var task = await _context.Tasks.Include(t => t.Project).FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null)
                return NotFound();

            if (!await CanModify(task))
                return Forbid();

            // Append to the end of the checklist.
            var nextOrder = await _context.Subtasks
                .Where(s => s.TaskId == taskId)
                .Select(s => (int?)s.SortOrder)
                .MaxAsync() ?? 0;

            var subtask = new Subtask
            {
                TaskId = taskId,
                Title = dto.Title,
                IsCompleted = false,
                SortOrder = nextOrder + 1,
                AssignedToId = dto.AssignedToId,
                DueDate = dto.DueDate,
                CreatedAt = DateTime.UtcNow
            };

            _context.Subtasks.Add(subtask);
            await _context.SaveChangesAsync();

            await _context.Entry(subtask).Reference(s => s.AssignedTo).LoadAsync();

            return CreatedAtAction(nameof(GetSubtasks), new { taskId }, subtask.ToDto());
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<SubtaskDto>> UpdateSubtask(int taskId, int id, [FromBody] SubtaskDto dto)
        {
            var subtask = await _context.Subtasks
                .Include(s => s.Task).ThenInclude(t => t!.Project)
                .FirstOrDefaultAsync(s => s.Id == id && s.TaskId == taskId);

            if (subtask == null)
                return NotFound();

            if (!await CanModify(subtask.Task!))
                return Forbid();

            var wasCompleted = subtask.IsCompleted;

            subtask.Title = dto.Title;
            subtask.IsCompleted = dto.IsCompleted;
            subtask.SortOrder = dto.SortOrder;
            subtask.AssignedToId = dto.AssignedToId;
            subtask.DueDate = dto.DueDate;

            if (!wasCompleted && subtask.IsCompleted)
                subtask.CompletedAt = DateTime.UtcNow;
            if (wasCompleted && !subtask.IsCompleted)
                subtask.CompletedAt = null;

            await _context.SaveChangesAsync();
            return Ok(subtask.ToDto());
        }

        [HttpPut("reorder")]
        public async Task<ActionResult> ReorderSubtasks(int taskId, [FromBody] List<int> orderedIds)
        {
            var subtasks = await _context.Subtasks
                .Include(s => s.Task).ThenInclude(t => t!.Project)
                .Where(s => s.TaskId == taskId)
                .ToListAsync();

            if (subtasks.Count == 0)
                return NotFound();

            if (!await CanModify(subtasks.First().Task!))
                return Forbid();

            for (var i = 0; i < orderedIds.Count; i++)
            {
                var item = subtasks.FirstOrDefault(s => s.Id == orderedIds[i]);
                if (item != null)
                    item.SortOrder = i + 1;
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [Authorize(Roles = "SuperAdmin,OrganizationAdmin,Manager,Admin")]
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteSubtask(int taskId, int id)
        {
            var subtask = await _context.Subtasks
                .Include(s => s.Task).ThenInclude(t => t!.Project)
                .FirstOrDefaultAsync(s => s.Id == id && s.TaskId == taskId);

            if (subtask == null)
                return NotFound();

            if (!await CanModify(subtask.Task!))
                return Forbid();

            _context.Subtasks.Remove(subtask);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ── Helpers ──────────────────────────────────────────────────────
        private Task<bool> TaskAccessible(int taskId) =>
            _context.Tasks.AnyAsync(t => t.Id == taskId);

        private async Task<bool> CanModify(TaskItem task)
        {
            var role = _tenant.Role;
            // Users can toggle their own subtasks; managers/org-admins can edit anyone's.
            if (role is Roles.SuperAdmin or Roles.OrganizationAdmin or "Admin")
                return true;
            if (role == Roles.Manager)
                return await _context.Projects.AnyAsync(p => p.Id == task.ProjectId && p.ManagerId == _tenant.UserId);
            return task.AssignedToId == _tenant.UserId;
        }
    }
}
