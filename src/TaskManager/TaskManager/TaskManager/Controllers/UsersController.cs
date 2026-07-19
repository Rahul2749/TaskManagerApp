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
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ITenantService _tenant;

        public UsersController(ApplicationDbContext context, ITenantService tenant)
        {
            _context = context;
            _tenant = tenant;
        }

        [Authorize(Roles = "SuperAdmin,OrganizationAdmin,Manager")]
        [HttpGet]
        public async Task<ActionResult> GetUsers(
            [FromQuery] string? role = null,
            [FromQuery] int? pageNumber = null,
            [FromQuery] int? pageSize = null)
        {
            var currentUserRole = _tenant.Role;

            var query = _context.Users.AsQueryable();

            // Managers can only see Users, not other Managers or admins
            if (currentUserRole == Roles.Manager)
            {
                query = query.Where(u => u.Role == Roles.User);
            }
            else if (!string.IsNullOrWhiteSpace(role))
            {
                query = query.Where(u => u.Role == role);
            }

            query = query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName);

            if (pageNumber.HasValue || pageSize.HasValue)
            {
                var page = await query.ToPagedResultAsync(pageNumber ?? 1, pageSize ?? 20);
                var paged = new PagedResult<UserDto>
                {
                    Items = page.Items.Select(u => u.ToDto()).ToList(),
                    PageNumber = page.PageNumber,
                    PageSize = page.PageSize,
                    TotalCount = page.TotalCount
                };
                return Ok(paged);
            }

            var users = await query.ToListAsync();
            return Ok(users.Select(u => u.ToDto()).ToList());
        }

        [Authorize(Roles = "SuperAdmin,OrganizationAdmin,Manager")]
        [HttpGet("{id}")]
        public async Task<ActionResult<UserDto>> GetUser(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
                return NotFound();

            var currentUserRole = _tenant.Role;

            // Managers can only access Users
            if (currentUserRole == Roles.Manager && user.Role != Roles.User)
                return Forbid();

            return Ok(user.ToDto());
        }

        [Authorize(Roles = "SuperAdmin,OrganizationAdmin,Manager")]
        [HttpPost]
        public async Task<ActionResult<UserDto>> CreateUser([FromBody] RegisterDto registerDto)
        {
            var currentUserRole = _tenant.Role;
            var currentUserId = _tenant.UserId!.Value;

            var canAssignRole = currentUserRole switch
            {
                Roles.Manager => registerDto.Role == Roles.User,
                Roles.OrganizationAdmin => registerDto.Role is Roles.User or Roles.Manager,
                _ => false
            };

            if (!canAssignRole)
                return Forbid();

            // Check if username or email already exists
            if (await _context.Users.AnyAsync(u => u.Username == registerDto.Username))
                return BadRequest("Username already exists");

            if (await _context.Users.AnyAsync(u => u.Email == registerDto.Email))
                return BadRequest("Email already exists");

            // Tenant users are created in the caller's organization. SuperAdmin must
            // specify a target organization explicitly (handled in a dedicated endpoint).
            if (currentUserRole == Roles.SuperAdmin && !_tenant.OrganizationId.HasValue)
                return BadRequest("Specify the organization to create the user in");

            var user = new User
            {
                Username = registerDto.Username,
                Email = registerDto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
                FirstName = registerDto.FirstName,
                LastName = registerDto.LastName,
                Role = registerDto.Role,
                OrganizationId = _tenant.OrganizationId,
                IsActive = true,
                CreatedBy = currentUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user.ToDto());
        }

        [Authorize(Roles = "SuperAdmin,OrganizationAdmin,Manager")]
        [HttpPut("{id}")]
        public async Task<ActionResult<UserDto>> UpdateUser(int id, [FromBody] RegisterDto updateDto)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
                return NotFound();

            var currentUserRole = _tenant.Role;

            // Managers can only update Users
            if (currentUserRole == Roles.Manager && user.Role != Roles.User)
                return Forbid();

            // Check username uniqueness
            if (user.Username != updateDto.Username &&
                await _context.Users.AnyAsync(u => u.Username == updateDto.Username))
                return BadRequest("Username already exists");

            // Check email uniqueness
            if (user.Email != updateDto.Email &&
                await _context.Users.AnyAsync(u => u.Email == updateDto.Email))
                return BadRequest("Email already exists");

            user.Username = updateDto.Username;
            user.Email = updateDto.Email;
            user.FirstName = updateDto.FirstName;
            user.LastName = updateDto.LastName;

            // Only update password if provided
            if (!string.IsNullOrWhiteSpace(updateDto.Password))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(updateDto.Password);
            }

            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(user.ToDto());
        }

        [Authorize(Roles = "SuperAdmin,OrganizationAdmin,Manager")]
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
                return NotFound();

            var currentUserRole = _tenant.Role;
            var currentUserId = _tenant.UserId!.Value;

            // Prevent self-deletion
            if (user.Id == currentUserId)
                return BadRequest("Cannot delete your own account");

            // Managers can only delete Users
            if (currentUserRole == Roles.Manager && user.Role != Roles.User)
                return Forbid();

            // Prevent deleting platform admins
            if (user.Role is Roles.SuperAdmin or Roles.OrganizationAdmin)
                return BadRequest("Cannot delete administrator account");

            // Soft delete - just mark as inactive
            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}

