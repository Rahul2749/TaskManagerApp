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
    public class DashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ITenantService _tenant;

        public DashboardController(ApplicationDbContext context, ITenantService tenant)
        {
            _context = context;
            _tenant = tenant;
        }

        [HttpGet]
        public async Task<ActionResult<DashboardDto>> GetDashboardData()
        {
            var currentUserId = _tenant.UserId!.Value;
            var currentUserRole = _tenant.Role;

            if (currentUserRole is not (
                Roles.SuperAdmin or
                Roles.OrganizationAdmin or
                Roles.Manager or
                Roles.User))
            {
                return Forbid();
            }

            var dashboard = currentUserRole switch
            {
                Roles.SuperAdmin or Roles.OrganizationAdmin => await GetAdminDashboard(currentUserId, currentUserRole),
                Roles.Manager => await GetManagerDashboard(currentUserId),
                Roles.User => await GetUserDashboard(currentUserId),
                _ => throw new InvalidOperationException("Unsupported role")
            };

            return Ok(dashboard);
        }

        private async Task<DashboardDto> GetAdminDashboard(int currentUserId, string role)
        {
            // Postgres timestamptz columns must be compared to UTC dates, not DateTime.Today (unspecified kind).
            var todayUtc = DateTime.UtcNow.Date;

            // Scope: SuperAdmin sees every tenant; OrganizationAdmin is narrowed by the
            // EF query filter to their own organization.
            var baseTasks = _context.Tasks.AsQueryable();
            var baseProjects = _context.Projects.AsQueryable();
            var baseUsers = _context.Users.AsQueryable();

            // Aggregates computed in SQL, not in memory.
            var stats = await baseTasks
                .GroupBy(t => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Completed = g.Count(t => t.Status == "Completed" || t.Status == "Tested" || t.Status == "Closed"),
                    InProgress = g.Count(t => t.Status == "InProgress"),
                    Overdue = g.Count(t => t.DueDate.HasValue && t.DueDate.Value < todayUtc
                                           && t.Status != "Completed" && t.Status != "Tested" && t.Status != "Closed")
                })
                .FirstOrDefaultAsync() ?? new { Total = 0, Completed = 0, InProgress = 0, Overdue = 0 };

            var totalProjects = await baseProjects.CountAsync();
            var activeProjects = await baseProjects.CountAsync(p => p.Status == "Active");
            var totalUsers = await baseUsers.CountAsync(u => u.Role == Roles.User || u.Role == Roles.Manager);
            var activeUsers = await baseUsers.CountAsync(u => u.IsActive && (u.Role == Roles.User || u.Role == Roles.Manager));

            // Project summaries with translated aggregates (one round-trip, all in SQL).
            var projectSummaries = await baseProjects
                .Select(p => new ProjectTaskSummary
                {
                    ProjectId = p.Id,
                    ProjectName = p.Name,
                    TotalTasks = p.Tasks.Count,
                    CompletedTasks = p.Tasks.Count(t => t.Status == "Completed" || t.Status == "Tested" || t.Status == "Closed"),
                    InProgressTasks = p.Tasks.Count(t => t.Status == "InProgress"),
                    CompletionPercentage = p.Tasks.Count > 0
                        ? Math.Round((double)p.Tasks.Count(t => t.Status == "Completed" || t.Status == "Tested" || t.Status == "Closed") / p.Tasks.Count * 100, 2)
                        : 0
                })
                .ToListAsync();

            return new DashboardDto
            {
                TotalProjects = totalProjects,
                ActiveProjects = activeProjects,
                TotalTasks = stats.Total,
                CompletedTasks = stats.Completed,
                InProgressTasks = stats.InProgress,
                OverdueTasks = stats.Overdue,
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                ProjectSummaries = projectSummaries,
                RecentTasks = await GetRecentTasks(null),
                UpcomingDeadlines = await GetUpcomingDeadlines(null)
            };
        }

        private async Task<DashboardDto> GetManagerDashboard(int managerId)
        {
            var todayUtc = DateTime.UtcNow.Date;

            var managerProjectIds = await _context.Projects
                .Where(p => p.ManagerId == managerId)
                .Select(p => p.Id)
                .ToListAsync();

            var stats = await _context.Tasks
                .Where(t => managerProjectIds.Contains(t.ProjectId))
                .GroupBy(t => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Completed = g.Count(t => t.Status == "Completed" || t.Status == "Tested" || t.Status == "Closed"),
                    InProgress = g.Count(t => t.Status == "InProgress"),
                    Overdue = g.Count(t => t.DueDate.HasValue && t.DueDate.Value < todayUtc
                                           && t.Status != "Completed" && t.Status != "Tested" && t.Status != "Closed")
                })
                .FirstOrDefaultAsync() ?? new { Total = 0, Completed = 0, InProgress = 0, Overdue = 0 };

            var projectSummaries = await _context.Projects
                .Where(p => p.ManagerId == managerId)
                .Select(p => new ProjectTaskSummary
                {
                    ProjectId = p.Id,
                    ProjectName = p.Name,
                    TotalTasks = p.Tasks.Count,
                    CompletedTasks = p.Tasks.Count(t => t.Status == "Completed" || t.Status == "Tested" || t.Status == "Closed"),
                    InProgressTasks = p.Tasks.Count(t => t.Status == "InProgress"),
                    CompletionPercentage = p.Tasks.Count > 0
                        ? Math.Round((double)p.Tasks.Count(t => t.Status == "Completed" || t.Status == "Tested" || t.Status == "Closed") / p.Tasks.Count * 100, 2)
                        : 0
                })
                .ToListAsync();

            var totalUsers = await _context.ProjectUsers
                .Where(pu => managerProjectIds.Contains(pu.ProjectId))
                .Select(pu => pu.UserId)
                .Distinct()
                .CountAsync();

            return new DashboardDto
            {
                TotalProjects = managerProjectIds.Count,
                ActiveProjects = await _context.Projects.CountAsync(p => p.ManagerId == managerId && p.Status == "Active"),
                TotalTasks = stats.Total,
                CompletedTasks = stats.Completed,
                InProgressTasks = stats.InProgress,
                OverdueTasks = stats.Overdue,
                TotalUsers = totalUsers,
                ActiveUsers = totalUsers,
                ProjectSummaries = projectSummaries,
                RecentTasks = await GetRecentTasks(managerId),
                UpcomingDeadlines = await GetUpcomingDeadlines(managerId)
            };
        }

        private async Task<DashboardDto> GetUserDashboard(int userId)
        {
            var todayUtc = DateTime.UtcNow.Date;

            var stats = await _context.Tasks
                .Where(t => t.AssignedToId == userId)
                .GroupBy(t => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Completed = g.Count(t => t.Status == "Completed" || t.Status == "Tested" || t.Status == "Closed"),
                    InProgress = g.Count(t => t.Status == "InProgress"),
                    Overdue = g.Count(t => t.DueDate.HasValue && t.DueDate.Value < todayUtc
                                           && t.Status != "Completed" && t.Status != "Tested" && t.Status != "Closed")
                })
                .FirstOrDefaultAsync() ?? new { Total = 0, Completed = 0, InProgress = 0, Overdue = 0 };

            var projectIds = await _context.Tasks
                .Where(t => t.AssignedToId == userId)
                .Select(t => t.ProjectId)
                .Distinct()
                .ToListAsync();

            return new DashboardDto
            {
                TotalProjects = projectIds.Count,
                ActiveProjects = await _context.Projects.CountAsync(p => projectIds.Contains(p.Id) && p.Status == "Active"),
                TotalTasks = stats.Total,
                CompletedTasks = stats.Completed,
                InProgressTasks = stats.InProgress,
                OverdueTasks = stats.Overdue,
                TotalUsers = 0,
                ActiveUsers = 0,
                RecentTasks = await GetRecentTasksForUser(userId),
                UpcomingDeadlines = await GetUpcomingDeadlinesForUser(userId)
            };
        }

        private async Task<List<TaskDto>> GetRecentTasks(int? managerId)
        {
            var query = _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssignedTo)
                .Include(t => t.AssignedBy)
                .AsQueryable();

            if (managerId.HasValue)
                query = query.Where(t => t.Project.ManagerId == managerId.Value);

            var tasks = await query
                .OrderByDescending(t => t.CreatedAt)
                .Take(10)
                .ToListAsync();

            return tasks.Select(t => t.ToDto()).ToList();
        }

        private async Task<List<TaskDto>> GetRecentTasksForUser(int userId)
        {
            var tasks = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssignedTo)
                .Include(t => t.AssignedBy)
                .Where(t => t.AssignedToId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(10)
                .ToListAsync();

            return tasks.Select(t => t.ToDto()).ToList();
        }

        private async Task<List<TaskDto>> GetUpcomingDeadlines(int? managerId)
        {
            var todayUtc = DateTime.UtcNow.Date;

            var query = _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssignedTo)
                .Include(t => t.AssignedBy)
                .Where(t => t.DueDate.HasValue && t.DueDate.Value >= todayUtc
                           && t.Status != "Completed" && t.Status != "Tested" && t.Status != "Closed")
                .AsQueryable();

            if (managerId.HasValue)
                query = query.Where(t => t.Project.ManagerId == managerId.Value);

            var tasks = await query
                .OrderBy(t => t.DueDate)
                .Take(10)
                .ToListAsync();

            return tasks.Select(t => t.ToDto()).ToList();
        }

        private async Task<List<TaskDto>> GetUpcomingDeadlinesForUser(int userId)
        {
            var todayUtc = DateTime.UtcNow.Date;

            var tasks = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssignedTo)
                .Include(t => t.AssignedBy)
                .Where(t => t.AssignedToId == userId && t.DueDate.HasValue && t.DueDate.Value >= todayUtc
                           && t.Status != "Completed" && t.Status != "Tested" && t.Status != "Closed")
                .OrderBy(t => t.DueDate)
                .Take(10)
                .ToListAsync();

            return tasks.Select(t => t.ToDto()).ToList();
        }
    }
}
