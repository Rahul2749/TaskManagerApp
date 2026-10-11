using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Models;

namespace TaskManager.Helpers;

public static class TaskAssignments
{
    public static Task<bool> IsValidAssigneeAsync(ApplicationDbContext db, int organizationId,
        int userId, CancellationToken ct = default) =>
        db.Users.AnyAsync(u => u.Id == userId && u.OrganizationId == organizationId &&
            u.IsActive && u.Role == Roles.User, ct);

    public static async Task EnsureProjectMemberAsync(ApplicationDbContext db, int projectId,
        int userId, CancellationToken ct = default)
    {
        if (!await db.ProjectUsers.AnyAsync(p => p.ProjectId == projectId && p.UserId == userId, ct))
        {
            db.ProjectUsers.Add(new ProjectUser
            {
                ProjectId = projectId,
                UserId = userId,
                AssignedDate = DateTime.UtcNow
            });
        }
    }
}
