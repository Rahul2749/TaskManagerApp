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
    [Route("api/tasks/{taskId}/comments")]
    [ApiController]
    public class CommentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ITenantService _tenant;

        public CommentsController(ApplicationDbContext context, ITenantService tenant)
        {
            _context = context;
            _tenant = tenant;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CommentDto>>> GetComments(int taskId)
        {
            if (!await _context.Tasks.AnyAsync(t => t.Id == taskId))
                return NotFound();

            // Top-level comments + their replies, oldest first.
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
            if (!await _context.Tasks.AnyAsync(t => t.Id == taskId))
                return NotFound();

            // Validate parent comment belongs to the same task if a reply.
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

            return CreatedAtAction(nameof(GetComments), new { taskId }, comment.ToDto());
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<CommentDto>> UpdateComment(int taskId, int id, [FromBody] CommentDto dto)
        {
            var comment = await _context.Comments
                .FirstOrDefaultAsync(c => c.Id == id && c.TaskId == taskId);

            if (comment == null)
                return NotFound();

            // Only the author can edit their own comment.
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
            var comment = await _context.Comments
                .FirstOrDefaultAsync(c => c.Id == id && c.TaskId == taskId);

            if (comment == null)
                return NotFound();

            if (comment.AuthorId != _tenant.UserId && _tenant.Role != Roles.SuperAdmin)
                return Forbid();

            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
