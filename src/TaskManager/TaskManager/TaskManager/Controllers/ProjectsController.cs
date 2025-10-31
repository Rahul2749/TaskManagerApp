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
    public class ProjectsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ProjectsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProjectDto>>> GetProjects()
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

            IQueryable<Project> query = _context.Projects;

            // Filter projects based on role
            if (currentUserRole == "User")
            {
                // Users only see projects they're assigned to
                query = query.Where(p => p.ProjectUsers.Any(pu => pu.UserId == currentUserId));
            }
            else if (currentUserRole == "Manager")
            {
                // Managers see projects they manage
                query = query.Where(p => p.ManagerId == currentUserId);
            }
            // Admins see all projects

            var projects = await query
                .Include(p => p.Manager)
                .Include(p => p.ProjectUsers)
                    .ThenInclude(pu => pu.User)
                .Include(p => p.Tasks)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var projectDtos = projects.Select(p => new ProjectDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                Status = p.Status,
                ManagerId = p.ManagerId,
                Manager = MapToUserDto(p.Manager),
                AssignedUsers = p.ProjectUsers.Select(pu => MapToUserDto(pu.User)).ToList(),
                TaskCount = p.Tasks.Count,
                CompletedTaskCount = p.Tasks.Count(t => t.Status == "Completed" || t.Status == "Tested" || t.Status == "Closed"),
                CreatedAt = p.CreatedAt
            }).ToList();

            return Ok(projectDtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ProjectDto>> GetProject(int id)
        {
            var project = await _context.Projects
                .Include(p => p.Manager)
                .Include(p => p.ProjectUsers)
                    .ThenInclude(pu => pu.User)
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound();

            // Check access rights
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (currentUserRole == "User" && !project.ProjectUsers.Any(pu => pu.UserId == currentUserId))
                return Forbid();

            if (currentUserRole == "Manager" && project.ManagerId != currentUserId)
                return Forbid();

            return Ok(MapToProjectDto(project));
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpPost]
        public async Task<ActionResult<ProjectDto>> CreateProject([FromBody] ProjectDto projectDto)
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

            var project = new Project
            {
                Name = projectDto.Name,
                Description = projectDto.Description,
                StartDate = projectDto.StartDate,
                EndDate = projectDto.EndDate,
                Status = projectDto.Status,
                ManagerId = currentUserRole == "Manager" ? currentUserId : (projectDto.ManagerId ?? currentUserId),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            // Reload with navigation properties
            await _context.Entry(project)
                .Reference(p => p.Manager)
                .LoadAsync();

            return CreatedAtAction(nameof(GetProject), new { id = project.Id }, MapToProjectDto(project));
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpPut("{id}")]
        public async Task<ActionResult<ProjectDto>> UpdateProject(int id, [FromBody] ProjectDto projectDto)
        {
            var project = await _context.Projects.FindAsync(id);

            if (project == null)
                return NotFound();

            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

            // Managers can only update their own projects
            if (currentUserRole == "Manager" && project.ManagerId != currentUserId)
                return Forbid();

            project.Name = projectDto.Name;
            project.Description = projectDto.Description;
            project.StartDate = projectDto.StartDate;
            project.EndDate = projectDto.EndDate;
            project.Status = projectDto.Status;
            project.UpdatedAt = DateTime.UtcNow;

            // Admin can change manager
            if (currentUserRole == "Admin" && projectDto.ManagerId.HasValue)
            {
                project.ManagerId = projectDto.ManagerId.Value;
            }

            await _context.SaveChangesAsync();

            // Reload with navigation properties
            await _context.Entry(project)
                .Reference(p => p.Manager)
                .LoadAsync();

            return Ok(MapToProjectDto(project));
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteProject(int id)
        {
            var project = await _context.Projects.FindAsync(id);

            if (project == null)
                return NotFound();

            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

            // Managers can only delete their own projects
            if (currentUserRole == "Manager" && project.ManagerId != currentUserId)
                return Forbid();

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpPost("{id}/users")]
        public async Task<ActionResult> AssignUsersToProject(int id, [FromBody] ProjectUserMappingDto mapping)
        {
            var project = await _context.Projects
                .Include(p => p.ProjectUsers)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound();

            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (currentUserRole == "Manager" && project.ManagerId != currentUserId)
                return Forbid();

            // Remove existing mappings
            var existingMappings = project.ProjectUsers.ToList();
            _context.ProjectUsers.RemoveRange(existingMappings);

            // Add new mappings
            foreach (var userId in mapping.UserIds)
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null && user.Role == "User")
                {
                    project.ProjectUsers.Add(new ProjectUser
                    {
                        ProjectId = id,
                        UserId = userId,
                        AssignedDate = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Users assigned successfully" });
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("{id}/users")]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetProjectUsers(int id)
        {
            var project = await _context.Projects
                .Include(p => p.ProjectUsers)
                    .ThenInclude(pu => pu.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound();

            var users = project.ProjectUsers.Select(pu => MapToUserDto(pu.User)).ToList();

            return Ok(users);
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

        private ProjectDto MapToProjectDto(Project project)
        {
            return new ProjectDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                Status = project.Status,
                ManagerId = project.ManagerId,
                Manager = MapToUserDto(project.Manager),
                AssignedUsers = project.ProjectUsers?.Select(pu => MapToUserDto(pu.User)).ToList() ?? new List<UserDto>(),
                TaskCount = project.Tasks?.Count ?? 0,
                CompletedTaskCount = project.Tasks?.Count(t => t.Status == "Completed" || t.Status == "Tested" || t.Status == "Closed") ?? 0,
                CreatedAt = project.CreatedAt
            };
        }
    }
}
