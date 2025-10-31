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
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers([FromQuery] string? role = null)
        {
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

            var query = _context.Users.AsQueryable();

            // Managers can only see Users, not other Managers or Admins
            if (currentUserRole == "Manager")
            {
                query = query.Where(u => u.Role == "User");
            }
            else if (!string.IsNullOrWhiteSpace(role))
            {
                query = query.Where(u => u.Role == role);
            }

            var users = await query
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Role = u.Role,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();

            return Ok(users);
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("{id}")]
        public async Task<ActionResult<UserDto>> GetUser(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
                return NotFound();

            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

            // Managers can only access Users
            if (currentUserRole == "Manager" && user.Role != "User")
                return Forbid();

            return Ok(MapToUserDto(user));
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpPost]
        public async Task<ActionResult<UserDto>> CreateUser([FromBody] RegisterDto registerDto)
        {
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            // Validate role permissions
            if (currentUserRole == "Manager" && registerDto.Role != "User")
                return Forbid("Managers can only create Users");

            if (currentUserRole == "Admin" && registerDto.Role == "Admin")
                return BadRequest("Cannot create another Admin user");

            // Check if username or email already exists
            if (await _context.Users.AnyAsync(u => u.Username == registerDto.Username))
                return BadRequest("Username already exists");

            if (await _context.Users.AnyAsync(u => u.Email == registerDto.Email))
                return BadRequest("Email already exists");

            var user = new User
            {
                Username = registerDto.Username,
                Email = registerDto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
                FirstName = registerDto.FirstName,
                LastName = registerDto.LastName,
                Role = registerDto.Role,
                IsActive = true,
                CreatedBy = currentUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, MapToUserDto(user));
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpPut("{id}")]
        public async Task<ActionResult<UserDto>> UpdateUser(int id, [FromBody] RegisterDto updateDto)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
                return NotFound();

            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

            // Managers can only update Users
            if (currentUserRole == "Manager" && user.Role != "User")
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

            return Ok(MapToUserDto(user));
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
                return NotFound();

            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            // Prevent self-deletion
            if (user.Id == currentUserId)
                return BadRequest("Cannot delete your own account");

            // Managers can only delete Users
            if (currentUserRole == "Manager" && user.Role != "User")
                return Forbid();

            // Prevent deleting Admin
            if (user.Role == "Admin")
                return BadRequest("Cannot delete Admin user");

            // Soft delete - just mark as inactive
            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return NoContent();
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
