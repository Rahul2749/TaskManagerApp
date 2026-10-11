using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Helpers;
using TaskManager.Mapping;
using TaskManager.Models;
using TaskManager.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Controllers
{
    [Authorize]
    [Route("api/tasks/{taskId}/comments")]
    [ApiController]
    public class CommentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ITenantService _tenant;
        private readonly ICollaborationService _collaboration;

        public CommentsController(
            ApplicationDbContext context,
            ITenantService tenant,
            ICollaborationService collaboration)
        {
            _context = context;
            _tenant = tenant;
            _collaboration = collaboration;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CommentDto>>> GetComments(int taskId)
        {
            if (!await _context.Tasks.VisibleTo(_tenant).AnyAsync(t => t.Id == taskId))
                return NotFound();

            var comments = await _context.Comments
                .Include(c => c.Author)
                .Include(c => c.Replies).ThenInclude(r => r.Author)
                .Where(c => c.TaskId == taskId && c.ParentCommentId == null)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            return Ok(comments.Select(c => c.ToDto()).ToList());
        }

        [HttpPost]
        public async Task<ActionResult<CommentDto>> CreateComment(int taskId, [FromBody] CommentDto dto)
        {
            var task = await _context.Tasks.VisibleTo(_tenant).FirstOrDefaultAsync(t => t.Id == taskId);
            if (task is null)
                return NotFound();

            if (dto.ParentCommentId.HasValue &&
                !await _context.Comments.AnyAsync(c => c.Id == dto.ParentCommentId && c.TaskId == taskId))
                return BadRequest("Parent comment not found for this task.");

            var comment = new Comment
            {
                TaskId = taskId,
                AuthorId = _tenant.UserId!.Value,
                ParentCommentId = dto.ParentCommentId,
                Body = dto.Body,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            await _context.Entry(comment).Reference(c => c.Author).LoadAsync();
            var authorName = $"{comment.Author.FirstName} {comment.Author.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(authorName))
                authorName = comment.Author.Username;

            try
            {
                await _collaboration.HandleNewCommentAsync(task, comment, authorName);
            }
            catch
            {
                // Comment is already saved; realtime/notify failures should not fail the request.
            }

            return CreatedAtAction(nameof(GetComments), new { taskId }, comment.ToDto());
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<CommentDto>> UpdateComment(int taskId, int id, [FromBody] CommentDto dto)
        {
            if (!await _context.Tasks.VisibleTo(_tenant).AnyAsync(t => t.Id == taskId))
                return NotFound();

            var comment = await _context.Comments
                .FirstOrDefaultAsync(c => c.Id == id && c.TaskId == taskId);

            if (comment == null)
                return NotFound();

            if (comment.AuthorId != _tenant.UserId && _tenant.Role != Roles.SuperAdmin)
                return Forbid();

            comment.Body = dto.Body;
            comment.IsEdited = true;
            comment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await _context.Entry(comment).Reference(c => c.Author).LoadAsync();

            return Ok(comment.ToDto());
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteComment(int taskId, int id)
        {
            if (!await _context.Tasks.VisibleTo(_tenant).AnyAsync(t => t.Id == taskId))
                return NotFound();

            var comment = await _context.Comments
                .FirstOrDefaultAsync(c => c.Id == id && c.TaskId == taskId);

            if (comment == null)
                return NotFound();

            if (comment.AuthorId != _tenant.UserId && _tenant.Role != Roles.SuperAdmin)
                return Forbid();

            // Replies use a restrictive foreign key; remove the entire thread together.
            var comments = await _context.Comments.Where(c => c.TaskId == taskId).ToListAsync();
            var ids = new HashSet<int> { comment.Id };
            var pending = new Queue<int>();
            pending.Enqueue(comment.Id);
            while (pending.TryDequeue(out var parentId))
            {
                foreach (var reply in comments.Where(c => c.ParentCommentId == parentId))
                    if (ids.Add(reply.Id)) pending.Enqueue(reply.Id);
            }
            _context.Comments.RemoveRange(comments.Where(c => ids.Contains(c.Id)));
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
