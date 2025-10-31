using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaskManager.Data;
using TaskManager.Shared.DTOs;

namespace TaskManager.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<DashboardDto>> GetDashboardData()
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

            var dashboard = new DashboardDto();

            if (currentUserRole == "Admin")
            {
                dashboard = await GetAdminDashboard();
            }
            else if (currentUserRole == "Manager")
            {
                dashboard = await GetManagerDashboard(currentUserId);
            }
            else // User
            {
                dashboard = await GetUserDashboard(currentUserId);
            }

            return Ok(dashboard);
        }

        private async Task<DashboardDto> GetAdminDashboard()
        {
            var projects = await _context.Projects.ToListAsync();
            var tasks = await _context.Tasks.ToListAsync();
            var users = await _context.Users.ToListAsync();

            var dashboard = new DashboardDto
            {
                TotalProjects = projects.Count,
                ActiveProjects = projects.Count(p => p.Status == "Active"),
                TotalTasks = tasks.Count,
                CompletedTasks = tasks.Count(t => t.Status == "Completed" || t.Status == "Tested" || t.Status == "Closed"),
                InProgressTasks = tasks.Count(t => t.Status == "InProgress"),
                OverdueTasks = tasks.Count(t => t.DueDate.HasValue && t.DueDate.Value < DateTime.Today &&
                                                 t.Status != "Completed" && t.Status != "Tested" && t.Status != "Closed"),
                TotalUsers = users.Count(u => u.Role == "User" || u.Role == "Manager"),
                ActiveUsers = users.Count(u => u.IsActive && (u.Role == "User" || u.Role == "Manager"))
            };

            // Project summaries
            var projectSummaries = await _context.Projects
                .Select(p => new ProjectTaskSummary
                {
                    ProjectId = p.Id,
                    ProjectName = p.Name,
                    TotalTasks = p.Tasks.Count,
                    CompletedTasks = p.Tasks.Count(t => t.Status == "Completed" || t.Status == "Tested" || t.Status == "Closed"),
                    InProgressTasks = p.Tasks.Count(t => t.Status == "InProgress"),
                    CompletionPercentage = p.Tasks.Count > 0 ?
                        Math.Round((double)p.Tasks.Count(t => t.Status == "Completed" || t.Status == "Tested" || t.Status == "Closed") / p.Tasks.Count * 100, 2) : 0
                })
                .ToListAsync();

            dashboard.ProjectSummaries = projectSummaries;

            // Recent tasks
            dashboard.RecentTasks = await GetRecentTasks(null);

            // Upcoming deadlines
            dashboard.UpcomingDeadlines = await GetUpcomingDeadlines(null);

            return dashboard;
        }

        private async Task<DashboardDto> GetManagerDashboard(int managerId)
        {
            var projects = await _context.Projects
                .Where(p => p.ManagerId == managerId)
                .ToListAsync();

            var projectIds = projects.Select(p => p.Id).ToList();

            var tasks = await _context.Tasks
                .Where(t => projectIds.Contains(t.ProjectId))
                .ToListAsync();

            var dashboard = new DashboardDto
            {
                TotalProjects = projects.Count,
                ActiveProjects = projects.Count(p => p.Status == "Active"),
                TotalTasks = tasks.Count,
                CompletedTasks = tasks.Count(t => t.Status == "Completed" || t.Status == "Tested" || t.Status == "Closed"),
                InProgressTasks = tasks.Count(t => t.Status == "InProgress"),
                OverdueTasks = tasks.Count(t => t.DueDate.HasValue && t.DueDate.Value < DateTime.Today &&
                                                 t.Status != "Completed" && t.Status != "Tested" && t.Status != "Closed"),
                TotalUsers = await _context.ProjectUsers
                    .Where(pu => projectIds.Contains(pu.ProjectId))
                    .Select(pu => pu.UserId)
                    .Distinct()
                    .CountAsync(),
                ActiveUsers = await _context.ProjectUsers
                    .Where(pu => projectIds.Contains(pu.ProjectId))
                    .Select(pu => pu.User)
                    .Where(u => u.IsActive)
                    .Distinct()
                    .CountAsync()
            };

            // Project summaries
            var projectSummaries = projects.Select(p => new ProjectTaskSummary
            {
                ProjectId = p.Id,
                ProjectName = p.Name,
                TotalTasks = tasks.Count(t => t.ProjectId == p.Id),
                CompletedTasks = tasks.Count(t => t.ProjectId == p.Id && (t.Status == "Completed" || t.Status == "Tested" || t.Status == "Closed")),
                InProgressTasks = tasks.Count(t => t.ProjectId == p.Id && t.Status == "InProgress"),
                CompletionPercentage = tasks.Count(t => t.ProjectId == p.Id) > 0 ?
                    Math.Round((double)tasks.Count(t => t.ProjectId == p.Id && (t.Status == "Completed" || t.Status == "Tested" || t.Status == "Closed")) /
                               tasks.Count(t => t.ProjectId == p.Id) * 100, 2) : 0
            }).ToList();

            dashboard.ProjectSummaries = projectSummaries;

            // Recent tasks
            dashboard.RecentTasks = await GetRecentTasks(managerId);

            // Upcoming deadlines
            dashboard.UpcomingDeadlines = await GetUpcomingDeadlines(managerId);

            return dashboard;
        }

        private async Task<DashboardDto> GetUserDashboard(int userId)
        {
            var tasks = await _context.Tasks
                .Where(t => t.AssignedToId == userId)
                .ToListAsync();

            var projectIds = tasks.Select(t => t.ProjectId).Distinct().ToList();

            var dashboard = new DashboardDto
            {
                TotalProjects = projectIds.Count,
                ActiveProjects = await _context.Projects
                    .Where(p => projectIds.Contains(p.Id) && p.Status == "Active")
                    .CountAsync(),
                TotalTasks = tasks.Count,
                CompletedTasks = tasks.Count(t => t.Status == "Completed" || t.Status == "Tested" || t.Status == "Closed"),
                InProgressTasks = tasks.Count(t => t.Status == "InProgress"),
                OverdueTasks = tasks.Count(t => t.DueDate.HasValue && t.DueDate.Value < DateTime.Today &&
                                                 t.Status != "Completed" && t.Status != "Tested" && t.Status == "Closed"),
                TotalUsers = 0,
                ActiveUsers = 0
            };

            // Recent tasks
            dashboard.RecentTasks = await GetRecentTasksForUser(userId);

            // Upcoming deadlines
            dashboard.UpcomingDeadlines = await GetUpcomingDeadlinesForUser(userId);

            return dashboard;
        }

        private async Task<List<TaskDto>> GetRecentTasks(int? managerId)
        {
            var query = _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssignedTo)
                .Include(t => t.AssignedBy)
                .AsQueryable();

            if (managerId.HasValue)
            {
                query = query.Where(t => t.Project.ManagerId == managerId.Value);
            }

            var tasks = await query
                .OrderByDescending(t => t.CreatedAt)
                .Take(10)
                .ToListAsync();

            return tasks.Select(MapToTaskDto).ToList();
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

            return tasks.Select(MapToTaskDto).ToList();
        }

        private async Task<List<TaskDto>> GetUpcomingDeadlines(int? managerId)
        {
            var query = _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssignedTo)
                .Include(t => t.AssignedBy)
                .Where(t => t.DueDate.HasValue &&
                           t.DueDate.Value >= DateTime.Today &&
                           t.Status != "Completed" && t.Status != "Tested" && t.Status != "Closed")
                .AsQueryable();

            if (managerId.HasValue)
            {
                query = query.Where(t => t.Project.ManagerId == managerId.Value);
            }

            var tasks = await query
                .OrderBy(t => t.DueDate)
                .Take(10)
                .ToListAsync();

            return tasks.Select(MapToTaskDto).ToList();
        }

        private async Task<List<TaskDto>> GetUpcomingDeadlinesForUser(int userId)
        {
            var tasks = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssignedTo)
                .Include(t => t.AssignedBy)
                .Where(t => t.AssignedToId == userId &&
                           t.DueDate.HasValue &&
                           t.DueDate.Value >= DateTime.Today &&
                           t.Status != "Completed" && t.Status != "Tested" && t.Status != "Closed")
                .OrderBy(t => t.DueDate)
                .Take(10)
                .ToListAsync();

            return tasks.Select(MapToTaskDto).ToList();
        }

        private static TaskDto MapToTaskDto(TaskManager.Models.TaskItem task)
        {
            return new TaskDto
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                ProjectId = task.ProjectId,
                ProjectName = task.Project?.Name,
                AssignedToId = task.AssignedToId,
                AssignedTo = task.AssignedTo != null ? MapToUserDto(task.AssignedTo) : null,
                Status = task.Status,
                Priority = task.Priority,
                EstimatedHours = task.EstimatedHours,
                ActualHours = task.ActualHours,
                StartDate = task.StartDate,
                DueDate = task.DueDate,
                CompletedDate = task.CompletedDate,
                CreatedAt = task.CreatedAt
            };
        }

        private static UserDto MapToUserDto(TaskManager.Models.User user)
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
