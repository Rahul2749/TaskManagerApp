namespace TaskManager.Mobile.Helpers;

/// <summary>
/// Role strings must match the API / JWT claims (<c>OrganizationAdmin</c>, not <c>Admin</c>).
/// </summary>
public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string OrganizationAdmin = "OrganizationAdmin";
    public const string Manager = "Manager";
    public const string User = "User";

    public static bool IsOrgAdmin(string? role) =>
        role is OrganizationAdmin or SuperAdmin;

    public static bool IsOrgAdminOrManager(string? role) =>
        role is OrganizationAdmin or Manager or SuperAdmin;

    public static bool CanManageUsers(string? role) => IsOrgAdminOrManager(role);

    public static bool CanManageProjects(string? role) => IsOrgAdminOrManager(role);

    /// <summary>Roles the current user may assign when creating/editing users.</summary>
    public static IReadOnlyList<string> AssignableRoles(string? currentUserRole) =>
        currentUserRole switch
        {
            SuperAdmin or OrganizationAdmin => new[] { User, Manager, OrganizationAdmin },
            Manager => new[] { User },
            _ => Array.Empty<string>()
        };

    public static string DisplayName(string? role) => role switch
    {
        OrganizationAdmin => "Admin",
        SuperAdmin => "Super Admin",
        Manager => "Manager",
        User => "User",
        _ => role ?? "Unknown"
    };
}
