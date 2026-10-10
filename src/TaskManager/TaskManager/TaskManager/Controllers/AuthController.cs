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
        private readonly ITwoFactorService _twoFactor;
        private readonly IAuditService _audit;

        public AuthController(
            IAuthService authService,
            ApplicationDbContext context,
            IBackgroundJobClient jobs,
            IOptions<AppOptions> app,
            ITwoFactorService twoFactor,
            IAuditService audit)
        {
            _authService = authService;
            _context = context;
            _jobs = jobs;
            _app = app.Value;
            _twoFactor = twoFactor;
            _audit = audit;
        }

        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginDto loginDto)
        {
            LoginResponseDto? result;
            try
            {
                result = await _authService.LoginAsync(loginDto);
            }
            catch (TwoFactorThrottleException ex)
            {
                return StatusCode(429, new { message = ex.Message });
            }

            if (result == null)
                return Unauthorized(new { message = "Invalid username or password" });

            return Ok(result);
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private Task<User?> GetCurrentUserEntityAsync(CancellationToken ct) =>
            _context.Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == GetUserId(), ct);

        private async Task RevokeRefreshTokensAsync(int userId, CancellationToken ct)
        {
            var tokens = await _context.RefreshTokens.IgnoreQueryFilters()
                .Where(t => t.UserId == userId && !t.IsRevoked)
                .ToListAsync(ct);
            foreach (var token in tokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync(ct);
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

            return token?.Tokens is null
                ? Problem(statusCode: StatusCodes.Status500InternalServerError)
                : Ok(token.Tokens);
        }

        [AllowAnonymous]
        [HttpPost("two-factor/login/verify")]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<TokenDto>> VerifyLoginTwoFactor(
            [FromBody] VerifyTwoFactorDto dto,
            CancellationToken ct)
        {
            var valid = await _twoFactor.VerifyChallengeAsync(
                dto.ChallengeToken, dto.Code, TwoFactorService.LoginPurpose, ct);
            if (!valid)
                return Unauthorized(new { message = "The verification code is invalid or expired." });

            var challengeHash = SecureTokenFactory.Hash(dto.ChallengeToken.Trim());
            var userId = await _context.TwoFactorChallenges.IgnoreQueryFilters()
                .Where(c => c.ChallengeTokenHash == challengeHash)
                .Select(c => c.UserId)
                .SingleOrDefaultAsync(ct);
            var tokens = await _authService.IssueTokensForUserAsync(userId, twoFactorVerified: true);
            if (tokens is null)
                return Unauthorized(new { message = "Unable to complete sign-in. Please try again." });

            await _audit.LogAsync("auth.login", "User", userId.ToString(), new { method = "password+email_otp" }, actorUserId: userId, ct: ct);
            return Ok(tokens);
        }

        [Authorize]
        [HttpGet("two-factor")]
        public async Task<ActionResult<TwoFactorStatusDto>> GetTwoFactorStatus(CancellationToken ct)
        {
            var userId = GetUserId();
            var user = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user is null) return Unauthorized();
            return Ok(new TwoFactorStatusDto
            {
                Enabled = user.TwoFactorEnabled,
                RecoveryCodesRemaining = await _twoFactor.GetRecoveryCodesRemainingAsync(userId, ct)
            });
        }

        [Authorize]
        [HttpPost("two-factor/enable/start")]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<TwoFactorChallengeDto>> StartTwoFactorEnable(
            [FromBody] TwoFactorPasswordDto dto,
            CancellationToken ct)
        {
            var user = await GetCurrentUserEntityAsync(ct);
            if (user is null) return Unauthorized();
            if (user.TwoFactorEnabled) return Conflict(new { message = "Two-factor authentication is already enabled." });
            if (!string.IsNullOrWhiteSpace(dto.Password) && !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return Unauthorized(new { message = "Password is incorrect." });

            try
            {
                var challenge = await _twoFactor.StartChallengeAsync(user, TwoFactorService.EnablePurpose, ct);
                return Ok(new TwoFactorChallengeDto { ChallengeToken = challenge, Message = "A verification code was sent to your account email." });
            }
            catch (TwoFactorThrottleException ex) { return StatusCode(429, new { message = ex.Message }); }
        }

        [Authorize]
        [HttpPost("two-factor/enable/verify")]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<TwoFactorRecoveryCodesDto>> VerifyTwoFactorEnable(
            [FromBody] VerifyTwoFactorDto dto,
            CancellationToken ct)
        {
            var user = await GetCurrentUserEntityAsync(ct);
            if (user is null) return Unauthorized();
            if (user.TwoFactorEnabled) return Conflict(new { message = "Two-factor authentication is already enabled." });
            if (!await _twoFactor.VerifyChallengeAsync(dto.ChallengeToken, dto.Code, TwoFactorService.EnablePurpose, ct, user.Id))
                return BadRequest(new { message = "The verification code is invalid or expired." });

            user.TwoFactorEnabled = true;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            await RevokeRefreshTokensAsync(user.Id, ct);
            var codes = await _twoFactor.ReplaceRecoveryCodesAsync(user.Id, ct);
            var session = await _authService.IssueTokensForUserAsync(user.Id, twoFactorVerified: true);
            if (session is null)
                return Problem(statusCode: StatusCodes.Status500InternalServerError);
            await _audit.LogAsync("auth.two_factor.enabled", "User", user.Id.ToString(), organizationId: user.OrganizationId, ct: ct, actorUserId: user.Id, actorEmail: user.Email);
            return Ok(new TwoFactorRecoveryCodesDto { RecoveryCodes = codes, Session = session });
        }

        [Authorize]
        [HttpPost("two-factor/disable/start")]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<TwoFactorChallengeDto>> StartTwoFactorDisable(
            [FromBody] TwoFactorPasswordDto dto,
            CancellationToken ct)
        {
            var user = await GetCurrentUserEntityAsync(ct);
            if (user is null) return Unauthorized();
            if (!user.TwoFactorEnabled) return Conflict(new { message = "Two-factor authentication is not enabled." });
            if (!string.IsNullOrWhiteSpace(dto.Password) && !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return Unauthorized(new { message = "Password is incorrect." });

            try
            {
                var challenge = await _twoFactor.StartChallengeAsync(user, TwoFactorService.DisablePurpose, ct);
                return Ok(new TwoFactorChallengeDto { ChallengeToken = challenge, Message = "A verification code was sent to your account email." });
            }
            catch (TwoFactorThrottleException ex) { return StatusCode(429, new { message = ex.Message }); }
        }

        [Authorize]
        [HttpPost("two-factor/disable/verify")]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<TwoFactorUpdateDto>> VerifyTwoFactorDisable(
            [FromBody] VerifyTwoFactorDto dto,
            CancellationToken ct)
        {
            var user = await GetCurrentUserEntityAsync(ct);
            if (user is null) return Unauthorized();
            if (!user.TwoFactorEnabled) return Conflict(new { message = "Two-factor authentication is not enabled." });
            if (!await _twoFactor.VerifyChallengeAsync(dto.ChallengeToken, dto.Code, TwoFactorService.DisablePurpose, ct, user.Id))
                return BadRequest(new { message = "The verification code is invalid or expired." });

            user.TwoFactorEnabled = false;
            user.UpdatedAt = DateTime.UtcNow;
            var codes = await _context.TwoFactorRecoveryCodes.IgnoreQueryFilters()
                .Where(c => c.UserId == user.Id).ToListAsync(ct);
            _context.TwoFactorRecoveryCodes.RemoveRange(codes);
            await _context.SaveChangesAsync(ct);
            await RevokeRefreshTokensAsync(user.Id, ct);
            var session = await _authService.IssueTokensForUserAsync(user.Id);
            if (session is null)
                return Problem(statusCode: StatusCodes.Status500InternalServerError);
            await _audit.LogAsync("auth.two_factor.disabled", "User", user.Id.ToString(), organizationId: user.OrganizationId, ct: ct, actorUserId: user.Id, actorEmail: user.Email);
            return Ok(new TwoFactorUpdateDto { Session = session });
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
