using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskManager.Billing;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.Services;
using TaskManager.Services.Billing;
using TaskManager.Shared.DTOs;

namespace TaskManager.Controllers;

[Route("api/[controller]")]
[ApiController]
public class InvitesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantService _tenant;
    private readonly IEntitlementService _entitlements;
    private readonly IBackgroundJobClient _jobs;
    private readonly AppOptions _app;

    public InvitesController(
        ApplicationDbContext context,
        ITenantService tenant,
        IEntitlementService entitlements,
        IBackgroundJobClient jobs,
        IOptions<AppOptions> app)
    {
        _context = context;
        _tenant = tenant;
        _entitlements = entitlements;
        _jobs = jobs;
        _app = app.Value;
    }

    [Authorize(Roles = "OrganizationAdmin,Manager")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrganizationInviteDto>>> List(CancellationToken ct)
    {
        var invites = await _context.OrganizationInvites
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new OrganizationInviteDto
            {
                Id = i.Id,
                Email = i.Email,
                Role = i.Role,
                ExpiresAt = i.ExpiresAt,
                AcceptedAt = i.AcceptedAt,
                CreatedAt = i.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(invites);
    }

    [Authorize(Roles = "OrganizationAdmin,Manager")]
    [HttpPost]
    public async Task<ActionResult<OrganizationInviteDto>> Create([FromBody] CreateInviteDto dto, CancellationToken ct)
    {
        if (_tenant.OrganizationId is not int orgId || _tenant.UserId is not int userId)
            return BadRequest("Organization context required");

        var role = dto.Role.Trim();
        var canInvite = _tenant.Role switch
        {
            Roles.Manager => role == Roles.User,
            Roles.OrganizationAdmin => role is Roles.User or Roles.Manager,
            _ => false
        };

        if (!canInvite)
            return Forbid();

        var email = dto.Email.Trim().ToLowerInvariant();

        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == email, ct))
            return Conflict(new ProblemDetails
            {
                Title = "User already exists",
                Detail = "A user with that email is already in this workspace.",
                Status = StatusCodes.Status409Conflict
            });

        var now = DateTime.UtcNow;
        if (await _context.OrganizationInvites.AnyAsync(
                i => i.Email == email && i.AcceptedAt == null && i.ExpiresAt > now, ct))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Invite already pending",
                Detail = "An open invite already exists for that email.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var activeSeats = await _context.Users.CountAsync(u => u.IsActive, ct);
        var pendingInvites = await _context.OrganizationInvites.CountAsync(
            i => i.AcceptedAt == null && i.ExpiresAt > now, ct);

        if (!await _entitlements.IsWithinLimitAsync(orgId, LimitKeys.MaxSeats, activeSeats + pendingInvites, 1, ct))
        {
            return Problem(
                title: "Seat limit reached",
                detail: "Upgrade your plan to invite more teammates.",
                statusCode: StatusCodes.Status402PaymentRequired);
        }

        var org = await _context.Organizations.FindAsync([orgId], ct);
        if (org is null)
            return NotFound();

        var (raw, hash) = SecureTokenFactory.Create();
        var invite = new OrganizationInvite
        {
            OrganizationId = orgId,
            InvitedByUserId = userId,
            Email = email,
            Role = role,
            TokenHash = hash,
            ExpiresAt = now.AddDays(7),
            CreatedAt = now
        };

        _context.OrganizationInvites.Add(invite);
        await _context.SaveChangesAsync(ct);

        var inviteUrl = $"{_app.PublicBaseUrl.TrimEnd('/')}/accept-invite?token={Uri.EscapeDataString(raw)}";
        _jobs.Enqueue<EmailJobs>(j => j.SendInvite(email, org.Name, role, inviteUrl));

        return Ok(new OrganizationInviteDto
        {
            Id = invite.Id,
            Email = invite.Email,
            Role = invite.Role,
            ExpiresAt = invite.ExpiresAt,
            AcceptedAt = invite.AcceptedAt,
            CreatedAt = invite.CreatedAt
        });
    }

    [Authorize(Roles = "OrganizationAdmin,Manager")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Revoke(int id, CancellationToken ct)
    {
        var invite = await _context.OrganizationInvites.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invite is null)
            return NotFound();

        if (invite.AcceptedAt is not null)
            return BadRequest("Invite already accepted");

        _context.OrganizationInvites.Remove(invite);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpGet("preview")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<object>> Preview([FromQuery] string token, CancellationToken ct)
    {
        var invite = await FindValidInviteAsync(token, ct);
        if (invite is null)
            return NotFound(new { message = "Invite is invalid or expired." });

        return Ok(new
        {
            email = invite.Email,
            role = invite.Role,
            organizationName = invite.Organization.Name,
            expiresAt = invite.ExpiresAt
        });
    }

    [AllowAnonymous]
    [HttpPost("accept")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<TokenDto>> Accept(
        [FromBody] AcceptInviteDto dto,
        [FromServices] IAuthService authService,
        CancellationToken ct)
    {
        var invite = await FindValidInviteAsync(dto.Token, ct);
        if (invite is null)
            return BadRequest(new { message = "Invite is invalid or expired." });

        var normalizedUsername = dto.Username.Trim();
        var email = invite.Email;

        if (await _context.Users.IgnoreQueryFilters()
                .AnyAsync(u => u.Username.ToLower() == normalizedUsername.ToLower(), ct))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Username unavailable",
                Detail = "That username is already in use.",
                Status = StatusCodes.Status409Conflict
            });
        }

        if (await _context.Users.IgnoreQueryFilters()
                .AnyAsync(u => u.Email.ToLower() == email, ct))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Email already registered",
                Detail = "An account already exists for that email.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var activeSeats = await _context.Users.IgnoreQueryFilters()
            .CountAsync(u => u.OrganizationId == invite.OrganizationId && u.IsActive, ct);

        if (!await _entitlements.IsWithinLimitAsync(invite.OrganizationId, LimitKeys.MaxSeats, activeSeats, 1, ct))
        {
            return Problem(
                title: "Seat limit reached",
                detail: "This workspace cannot accept more members right now.",
                statusCode: StatusCodes.Status402PaymentRequired);
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            OrganizationId = invite.OrganizationId,
            Username = normalizedUsername,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Role = invite.Role,
            IsActive = true,
            CreatedBy = invite.InvitedByUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        _context.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = invite.OrganizationId,
            UserId = user.Id,
            Role = invite.Role,
            JoinedAt = now
        });

        invite.AcceptedAt = now;
        await _context.SaveChangesAsync(ct);

        var token = await authService.LoginAsync(new LoginDto
        {
            Username = normalizedUsername,
            Password = dto.Password
        });

        return token is null
            ? Problem(statusCode: StatusCodes.Status500InternalServerError)
            : Ok(token);
    }

    private async Task<OrganizationInvite?> FindValidInviteAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var hash = SecureTokenFactory.Hash(token.Trim());
        var now = DateTime.UtcNow;

        return await _context.OrganizationInvites
            .IgnoreQueryFilters()
            .Include(i => i.Organization)
            .FirstOrDefaultAsync(
                i => i.TokenHash == hash && i.AcceptedAt == null && i.ExpiresAt > now,
                ct);
    }
}
