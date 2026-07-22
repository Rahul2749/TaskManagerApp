using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Controllers;

[Authorize]
[Route("api/notifications")]
[ApiController]
public class NotificationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantService _tenant;

    public NotificationsController(ApplicationDbContext context, ITenantService tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AppNotificationDto>>> List(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        if (_tenant.UserId is not int userId)
            return Unauthorized();

        take = Math.Clamp(take, 1, 100);
        var query = _context.AppNotifications
            .AsNoTracking()
            .Include(n => n.ActorUser)
            .Where(n => n.UserId == userId);

        if (unreadOnly)
            query = query.Where(n => n.ReadAt == null);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .Select(n => new AppNotificationDto
            {
                Id = n.Id,
                Type = n.Type,
                Title = n.Title,
                Message = n.Message,
                LinkUrl = n.LinkUrl,
                TaskId = n.TaskId,
                ActorName = n.ActorUser != null
                    ? n.ActorUser.FirstName + " " + n.ActorUser.LastName
                    : null,
                IsRead = n.ReadAt != null,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<object>> UnreadCount(CancellationToken ct = default)
    {
        if (_tenant.UserId is not int userId)
            return Unauthorized();

        var count = await _context.AppNotifications
            .CountAsync(n => n.UserId == userId && n.ReadAt == null, ct);
        return Ok(new { count });
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct = default)
    {
        if (_tenant.UserId is not int userId)
            return Unauthorized();

        var item = await _context.AppNotifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);
        if (item is null)
            return NotFound();

        if (item.ReadAt is null)
        {
            item.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct = default)
    {
        if (_tenant.UserId is not int userId)
            return Unauthorized();

        var unread = await _context.AppNotifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var item in unread)
            item.ReadAt = now;

        await _context.SaveChangesAsync(ct);
        return NoContent();
    }
}
