using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.RegularExpressions;
using TaskManager.Billing;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ApplicationDbContext _context;
        private readonly IBackgroundJobClient _jobs;
        private readonly AppOptions _app;

        public AuthController(
            IAuthService authService,
            ApplicationDbContext context,
            IBackgroundJobClient jobs,
            IOptions<AppOptions> app)
        {
            _authService = authService;
            _context = context;
            _jobs = jobs;
            _app = app.Value;
        }

        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<TokenDto>> Login([FromBody] LoginDto loginDto)
        {
            var result = await _authService.LoginAsync(loginDto);

            if (result == null)
                return Unauthorized(new { message = "Invalid username or password" });

            return Ok(result);
        }

        [AllowAnonymous]
        [HttpPost("register")]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<TokenDto>> Register([FromBody] WorkspaceRegistrationDto dto)
        {
            var normalizedUsername = dto.Username.Trim();
            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

            if (await _context.Users.IgnoreQueryFilters()
                .AnyAsync(u => u.Username.ToLower() == normalizedUsername.ToLower()))
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Username unavailable",
                    Detail = "That username is already in use.",
                    Status = StatusCodes.Status409Conflict
                });
            }

            if (await _context.Users.IgnoreQueryFilters()
                .AnyAsync(u => u.Email.ToLower() == normalizedEmail))
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Email already registered",
                    Detail = "An account already exists for that email address.",
                    Status = StatusCodes.Status409Conflict
                });
            }

            var freePlan = await _context.Plans.SingleOrDefaultAsync(p => p.Code == PlanCodes.Free);
            if (freePlan is null)
            {
                return Problem(
                    title: "Registration temporarily unavailable",
                    detail: "The default subscription plan has not been configured.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var slug = await CreateUniqueSlugAsync(dto.OrganizationName);
            var now = DateTime.UtcNow;
            var organization = new Organization
            {
                Name = dto.OrganizationName.Trim(),
                Slug = slug,
                Status = "Active",
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Organizations.Add(organization);
            await _context.SaveChangesAsync();

            var user = new User
            {
                OrganizationId = organization.Id,
                Username = normalizedUsername,
                Email = normalizedEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                FirstName = dto.FirstName.Trim(),
                LastName = dto.LastName.Trim(),
                Role = Roles.OrganizationAdmin,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _context.OrganizationMembers.Add(new OrganizationMember
            {
                OrganizationId = organization.Id,
                UserId = user.Id,
                Role = Roles.OrganizationAdmin,
                JoinedAt = now
            });

            _context.Subscriptions.Add(new Subscription
            {
                OrganizationId = organization.Id,
                PlanId = freePlan.Id,
                Status = "active",
                BillingInterval = "monthly",
                Seats = 1,
                Provider = "internal",
                CreatedAt = now,
                UpdatedAt = now
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _jobs.Enqueue<EmailJobs>(j =>
                j.SendWelcome(user.Email, user.FirstName, organization.Name));

            var token = await _authService.LoginAsync(new LoginDto
            {
                Username = normalizedUsername,
                Password = dto.Password
            });

            return token is null
                ? Problem(statusCode: StatusCodes.Status500InternalServerError)
                : Ok(token);
        }

        [AllowAnonymous]
        [HttpPost("forgot-password")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var email = dto.Email.Trim().ToLowerInvariant();
            var user = await _context.Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email && u.IsActive);

            if (user is not null)
            {
                var unused = await _context.PasswordResetTokens
                    .IgnoreQueryFilters()
                    .Where(t => t.UserId == user.Id && t.UsedAt == null)
                    .ToListAsync();
                foreach (var prior in unused)
                    prior.UsedAt = DateTime.UtcNow;

                var (raw, hash) = SecureTokenFactory.Create();
                _context.PasswordResetTokens.Add(new PasswordResetToken
                {
                    UserId = user.Id,
                    TokenHash = hash,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                var resetUrl =
                    $"{_app.PublicBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(raw)}";
                _jobs.Enqueue<EmailJobs>(j =>
                    j.SendPasswordReset(user.Email, user.FirstName, resetUrl));
            }

            return Ok(new { message = "If that email exists, a reset link has been sent." });
        }

        [AllowAnonymous]
        [HttpPost("reset-password")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var hash = SecureTokenFactory.Hash(dto.Token.Trim());
            var now = DateTime.UtcNow;

            var reset = await _context.PasswordResetTokens
                .IgnoreQueryFilters()
                .Include(t => t.User)
                .FirstOrDefaultAsync(t =>
                    t.TokenHash == hash &&
                    t.UsedAt == null &&
                    t.ExpiresAt > now);

            if (reset is null)
            {
                return BadRequest(new { message = "Reset link is invalid or has expired." });
            }

            reset.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            reset.User.UpdatedAt = now;
            reset.UsedAt = now;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Password updated. You can sign in with your new password." });
        }

        [HttpPost("refresh")]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<TokenDto>> RefreshToken([FromBody] RefreshTokenDto dto)
        {
            var result = await _authService.RefreshTokenAsync(dto.RefreshToken);

            if (result == null)
                return Unauthorized(new { message = "Invalid or expired refresh token" });

            return Ok(result);
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<ActionResult> Logout()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userIdClaim == null)
                return Unauthorized();

            var userId = int.Parse(userIdClaim);
            await _authService.LogoutAsync(userId);

            return Ok(new { message = "Logged out successfully" });
        }

        [Authorize]
        [HttpGet("me")]
        public ActionResult<object> GetCurrentUser()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var firstName = User.FindFirst("firstName")?.Value;
            var lastName = User.FindFirst("lastName")?.Value;

            return Ok(new
            {
                id = int.Parse(userIdClaim!),
                username,
                email,
                firstName,
                lastName,
                role
            });
        }

        private async Task<string> CreateUniqueSlugAsync(string organizationName)
        {
            var baseSlug = Regex.Replace(
                    organizationName.Trim().ToLowerInvariant(),
                    "[^a-z0-9]+",
                    "-")
                .Trim('-');

            if (string.IsNullOrWhiteSpace(baseSlug))
                baseSlug = "workspace";

            baseSlug = baseSlug[..Math.Min(baseSlug.Length, 90)];
            var slug = baseSlug;
            var suffix = 2;

            while (await _context.Organizations.AnyAsync(o => o.Slug == slug))
            {
                slug = $"{baseSlug}-{suffix++}";
            }

            return slug;
        }
    }
}
