using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Controllers;

[Authorize(Roles = "OrganizationAdmin,Manager,SuperAdmin")]
[Route("api/analytics")]
[ApiController]
public class AnalyticsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantService _tenant;

    public AnalyticsController(ApplicationDbContext db, ITenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<WorkspaceAnalyticsDto>> Summary(CancellationToken ct)
    {
        var role = _tenant.Role;
        var userId = _tenant.UserId;
        if (userId is null) return Unauthorized();

        var todayUtc = DateTime.UtcNow.Date;
        var weekStart = todayUtc.AddDays(-(int)todayUtc.DayOfWeek);
        var weekEnd = weekStart.AddDays(7);

        IQueryable<TaskItem> tasks = _db.Tasks.AsQueryable();
        IQueryable<Project> projects = _db.Projects.AsQueryable();

        if (role == Roles.Manager)
        {
            var managedIds = await _db.Projects
                .Where(p => p.ManagerId == userId)
                .Select(p => p.Id)
                .ToListAsync(ct);
            tasks = tasks.Where(t => managedIds.Contains(t.ProjectId));
            projects = projects.Where(p => managedIds.Contains(p.Id));
        }

        var taskRows = await tasks
            .Select(t => new { t.Status, t.Priority, t.DueDate })
            .ToListAsync(ct);

        var total = taskRows.Count;
        var completed = taskRows.Count(t => IsDone(t.Status));
        var inProgress = taskRows.Count(t => t.Status == "InProgress");
        var overdue = taskRows.Count(t =>
            t.DueDate.HasValue && t.DueDate.Value.Date < todayUtc && !IsDone(t.Status));

        var statusBreakdown = taskRows
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Status) ? "Unknown" : t.Status)
            .Select(g => new NamedCountDto
            {
                Name = g.Key,
                Count = g.Count(),
                Percent = total > 0 ? Math.Round(g.Count() * 100.0 / total, 1) : 0
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        var priorityBreakdown = taskRows
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Priority) ? "None" : t.Priority)
            .Select(g => new NamedCountDto
            {
                Name = g.Key,
                Count = g.Count(),
                Percent = total > 0 ? Math.Round(g.Count() * 100.0 / total, 1) : 0
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        var projectSummaries = await projects
            .Select(p => new ProjectTaskSummary
            {
                ProjectId = p.Id,
                ProjectName = p.Name,
                TotalTasks = p.Tasks.Count,
                CompletedTasks = p.Tasks.Count(t =>
                    t.Status == "Completed" || t.Status == "Tested" || t.Status == "Closed"),
                InProgressTasks = p.Tasks.Count(t => t.Status == "InProgress"),
                CompletionPercentage = p.Tasks.Count > 0
                    ? Math.Round(
                        (double)p.Tasks.Count(t =>
                            t.Status == "Completed" || t.Status == "Tested" || t.Status == "Closed")
                        / p.Tasks.Count * 100, 1)
                    : 0
            })
            .OrderByDescending(p => p.TotalTasks)
            .Take(20)
            .ToListAsync(ct);

        decimal hours = 0;
        try
        {
            var timeQuery = _db.TimeEntries.AsQueryable();
            if (role == Roles.Manager)
            {
                var managedIds = await _db.Projects
                    .Where(p => p.ManagerId == userId)
                    .Select(p => p.Id)
                    .ToListAsync(ct);
                timeQuery = timeQuery.Where(e => managedIds.Contains(e.Task.ProjectId));
            }

            hours = await timeQuery
                .Where(e => e.WorkDate >= weekStart && e.WorkDate < weekEnd)
                .SumAsync(e => (decimal?)e.Hours, ct) ?? 0;
        }
        catch
        {
            hours = 0;
        }

        var totalProjects = await projects.CountAsync(ct);
        var activeProjects = await projects.CountAsync(p => p.Status == "Active", ct);
        var totalUsers = role == Roles.Manager
            ? 0
            : await _db.Users.CountAsync(u => u.Role == Roles.User || u.Role == Roles.Manager, ct);

        return Ok(new WorkspaceAnalyticsDto
        {
            GeneratedAt = DateTime.UtcNow,
            TotalProjects = totalProjects,
            ActiveProjects = activeProjects,
            TotalTasks = total,
            CompletedTasks = completed,
            InProgressTasks = inProgress,
            OverdueTasks = overdue,
            TotalUsers = totalUsers,
            CompletionRate = total > 0 ? Math.Round(completed * 100.0 / total, 1) : 0,
            HoursLoggedThisWeek = hours,
            StatusBreakdown = statusBreakdown,
            PriorityBreakdown = priorityBreakdown,
            ProjectSummaries = projectSummaries
        });
    }

    private static bool IsDone(string? status) =>
        status is "Completed" or "Tested" or "Closed";
}
