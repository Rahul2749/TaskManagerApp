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
    [Route("api/[controller]")]
    [ApiController]
    public class TagsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ITenantService _tenant;

        public TagsController(ApplicationDbContext context, ITenantService tenant)
        {
            _context = context;
            _tenant = tenant;
        }

        /// <summary>Lists all tags defined in the current tenant (the org's label palette).</summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TagDto>>> GetTags()
        {
            var tags = await _context.Tags.OrderBy(t => t.Name).ToListAsync();
            return Ok(tags.Select(t => t.ToDto()).ToList());
        }

        [Authorize(Roles = "SuperAdmin,OrganizationAdmin,Manager,Admin")]
        [HttpPost]
        public async Task<ActionResult<TagDto>> CreateTag([FromBody] TagDto dto)
        {
            if (!_tenant.OrganizationId.HasValue)
                return BadRequest("Tags belong to an organization; SuperAdmin must target one explicitly.");

            // Unique per org
            if (await _context.Tags.AnyAsync(t => t.Name == dto.Name))
                return BadRequest("A tag with this name already exists.");

            var tag = new Tag
            {
                Name = dto.Name,
                Color = string.IsNullOrWhiteSpace(dto.Color) ? "#6366f1" : dto.Color,
                OrganizationId = _tenant.OrganizationId.Value,
                CreatedAt = DateTime.UtcNow
            };

            _context.Tags.Add(tag);
            await _context.SaveChangesAsync();

            return Ok(tag.ToDto());
        }

        [Authorize(Roles = "SuperAdmin,OrganizationAdmin,Manager,Admin")]
        [HttpPut("{id}")]
        public async Task<ActionResult<TagDto>> UpdateTag(int id, [FromBody] TagDto dto)
        {
            var tag = await _context.Tags.FindAsync(id);
            if (tag == null)
                return NotFound();

            tag.Name = dto.Name;
            tag.Color = dto.Color;

            await _context.SaveChangesAsync();
            return Ok(tag.ToDto());
        }

        [Authorize(Roles = "SuperAdmin,OrganizationAdmin,Manager,Admin")]
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteTag(int id)
        {
            var tag = await _context.Tags.FindAsync(id);
            if (tag == null)
                return NotFound();

            _context.Tags.Remove(tag);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ── Apply / remove a tag on a task ────────────────────────────────
        [Authorize(Roles = "SuperAdmin,OrganizationAdmin,Manager,Admin,User")]
        [HttpPost("{tagId}/tasks/{taskId}")]
        public async Task<ActionResult> ApplyTag(int tagId, int taskId)
        {
            var task = await _context.Tasks.Include(t => t.Project).FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null)
                return NotFound("Task not found");

            if (!await _context.Tags.AnyAsync(t => t.Id == tagId))
                return NotFound("Tag not found");

            if (await _context.TaskTags.AnyAsync(tt => tt.TaskId == taskId && tt.TagId == tagId))
                return Ok(new { message = "Already applied" });

            _context.TaskTags.Add(new TaskTag
            {
                TaskId = taskId,
                TagId = tagId,
                AppliedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return Ok(new { message = "Tag applied" });
        }

        [Authorize(Roles = "SuperAdmin,OrganizationAdmin,Manager,Admin,User")]
        [HttpDelete("{tagId}/tasks/{taskId}")]
        public async Task<ActionResult> RemoveTag(int tagId, int taskId)
        {
            var link = await _context.TaskTags
                .FirstOrDefaultAsync(tt => tt.TaskId == taskId && tt.TagId == tagId);

            if (link == null)
                return NotFound();

            _context.TaskTags.Remove(link);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
