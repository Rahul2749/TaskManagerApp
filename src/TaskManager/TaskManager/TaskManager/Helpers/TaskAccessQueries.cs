using TaskManager.Models;
using TaskManager.Services;

namespace TaskManager.Helpers;

/// <summary>Apply task ownership as well as organization boundaries to every task feature.</summary>
public static class TaskAccessQueries
{
    public static IQueryable<TaskItem> VisibleTo(this IQueryable<TaskItem> query, ITenantService tenant)
    {
        if (tenant.IsSuperAdmin) return query;
        if (tenant.UserId is not int userId || tenant.OrganizationId is not int orgId)
            return query.Where(_ => false);

        query = query.Where(t => t.OrganizationId == orgId);
        return tenant.Role switch
        {
            Roles.OrganizationAdmin => query,
            Roles.Manager => query.Where(t => t.Project.ManagerId == userId),
            Roles.User => query.Where(t => t.AssignedToId == userId),
            _ => query.Where(_ => false)
        };
    }

    public static IQueryable<Project> VisibleTo(this IQueryable<Project> query, ITenantService tenant)
    {
        if (tenant.IsSuperAdmin) return query;
        if (tenant.UserId is not int userId || tenant.OrganizationId is not int orgId)
            return query.Where(_ => false);

        query = query.Where(p => p.OrganizationId == orgId);
        return tenant.Role switch
        {
            Roles.OrganizationAdmin => query,
            Roles.Manager => query.Where(p => p.ManagerId == userId),
            Roles.User => query.Where(p => p.ProjectUsers.Any(u => u.UserId == userId)),
            _ => query.Where(_ => false)
        };
    }

    public static IQueryable<TimeEntry> VisibleTo(this IQueryable<TimeEntry> query, ITenantService tenant)
    {
        if (tenant.IsSuperAdmin) return query;
        if (tenant.UserId is not int userId || tenant.OrganizationId is not int orgId)
            return query.Where(_ => false);

        query = query.Where(e => e.OrganizationId == orgId);
        return tenant.Role switch
        {
            Roles.OrganizationAdmin => query,
            Roles.Manager => query.Where(e => e.Task.Project.ManagerId == userId),
            Roles.User => query.Where(e => e.UserId == userId),
            _ => query.Where(_ => false)
        };
    }
}
