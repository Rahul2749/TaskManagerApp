using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
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
    public class ProjectsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ITenantService _tenant;

        public ProjectsController(ApplicationDbContext context, ITenantService tenant)
        {
            _context = context;
            _tenant = tenant;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProjectDto>>> GetProjects()
        {
            var currentUserId = _tenant.UserId!.Value;
            var currentUserRole = _tenant.Role;

            IQueryable<Project> query = _context.Projects;

            // Filter projects based on role (tenant filter already applied by EF query filter)
            if (currentUserRole == Roles.User)
            {
                // Users only see projects they're assigned to
                query = query.Where(p => p.ProjectUsers.Any(pu => pu.UserId == currentUserId));
            }
            else if (currentUserRole == Roles.Manager)
            {
                // Managers see projects they manage
                query = query.Where(p => p.ManagerId == currentUserId);
            }
            // SuperAdmin / OrganizationAdmin see all projects (within their tenant)

            var projects = await query
                .Include(p => p.Manager)
                .Include(p => p.ProjectUsers)
                    .ThenInclude(pu => pu.User)
                .Include(p => p.Tasks)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return Ok(projects.Select(p => p.ToDto()).ToList());
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
            var currentUserId = _tenant.UserId!.Value;
            var currentUserRole = _tenant.Role;

            if (currentUserRole == Roles.User && !project.ProjectUsers.Any(pu => pu.UserId == currentUserId))
                return Forbid();

            if (currentUserRole == Roles.Manager && project.ManagerId != currentUserId)
                return Forbid();

            return Ok(project.ToDto());
        }

        [Authorize(Roles = "SuperAdmin,OrganizationAdmin,Manager")]
        [HttpPost]
        public async Task<ActionResult<ProjectDto>> CreateProject([FromBody] ProjectDto projectDto)
        {
            var currentUserId = _tenant.UserId!.Value;
            var currentUserRole = _tenant.Role;

            // Every project must belong to a tenant.
            if (!_tenant.OrganizationId.HasValue)
                return BadRequest("No active organization for this account.");

            var project = new Project
            {
                Name = projectDto.Name,
                Description = projectDto.Description,
                StartDate = projectDto.StartDate,
                EndDate = projectDto.EndDate,
                Status = projectDto.Status,
                ManagerId = currentUserRole == Roles.Manager ? currentUserId : (projectDto.ManagerId ?? currentUserId),
                OrganizationId = _tenant.OrganizationId.Value,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            // Reload with navigation properties
            await _context.Entry(project)
                .Reference(p => p.Manager)
                .LoadAsync();

            return CreatedAtAction(nameof(GetProject), new { id = project.Id }, project.ToDto());
        }

        [Authorize(Roles = "SuperAdmin,OrganizationAdmin,Manager")]
        [HttpPut("{id}")]
        public async Task<ActionResult<ProjectDto>> UpdateProject(int id, [FromBody] ProjectDto projectDto)
        {
            var project = await _context.Projects.FindAsync(id);

            if (project == null)
                return NotFound();

            var currentUserId = _tenant.UserId!.Value;
            var currentUserRole = _tenant.Role;

            // Managers can only update their own projects
            if (currentUserRole == Roles.Manager && project.ManagerId != currentUserId)
                return Forbid();

            project.Name = projectDto.Name;
            project.Description = projectDto.Description;
            project.StartDate = projectDto.StartDate;
            project.EndDate = projectDto.EndDate;
            project.Status = projectDto.Status;
            project.UpdatedAt = DateTime.UtcNow;

            // Org admin / super admin can reassign manager
            if (currentUserRole is Roles.SuperAdmin or Roles.OrganizationAdmin
                && projectDto.ManagerId.HasValue)
            {
                project.ManagerId = projectDto.ManagerId.Value;
            }

            await _context.SaveChangesAsync();

            // Reload with navigation properties
            await _context.Entry(project)
                .Reference(p => p.Manager)
                .LoadAsync();

            return Ok(project.ToDto());
        }

        [Authorize(Roles = "SuperAdmin,OrganizationAdmin,Manager")]
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteProject(int id)
        {
            var project = await _context.Projects.FindAsync(id);

            if (project == null)
                return NotFound();

            var currentUserId = _tenant.UserId!.Value;
            var currentUserRole = _tenant.Role;

            // Managers can only delete their own projects
            if (currentUserRole == Roles.Manager && project.ManagerId != currentUserId)
                return Forbid();

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [Authorize(Roles = "SuperAdmin,OrganizationAdmin,Manager")]
        [HttpPost("{id}/users")]
        public async Task<ActionResult> AssignUsersToProject(int id, [FromBody] ProjectUserMappingDto mapping)
        {
            var project = await _context.Projects
                .Include(p => p.ProjectUsers)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound();

            var currentUserId = _tenant.UserId!.Value;
            var currentUserRole = _tenant.Role;

            if (currentUserRole == Roles.Manager && project.ManagerId != currentUserId)
                return Forbid();

            // Remove existing mappings
            var existingMappings = project.ProjectUsers.ToList();
            _context.ProjectUsers.RemoveRange(existingMappings);

            // Add new mappings
            foreach (var userId in mapping.UserIds)
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null && user.Role == Roles.User)
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

        [Authorize(Roles = "SuperAdmin,OrganizationAdmin,Manager")]
        [HttpGet("{id}/users")]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetProjectUsers(int id)
        {
            var project = await _context.Projects
                .Include(p => p.ProjectUsers)
                    .ThenInclude(pu => pu.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound();

            var users = project.ProjectUsers.Select(pu => pu.User.ToDto()).ToList();

            return Ok(users);
        }
    }
}

