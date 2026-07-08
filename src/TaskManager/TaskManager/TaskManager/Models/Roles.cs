namespace TaskManager.Models
{
    /// <summary>
    /// Central, strongly-typed definition of every role used across the platform.
    /// Keep the string values identical to what is stored in <see cref="User.Role"/>
    /// and emitted as JWT role claims so existing role-based authorization keeps working.
    /// </summary>
    public static class Roles
    {
        /// <summary>Platform-wide administrator. Not bound to any organization.</summary>
        public const string SuperAdmin = "SuperAdmin";

        /// <summary>Administrator of a specific organization.</summary>
        public const string OrganizationAdmin = "OrganizationAdmin";

        /// <summary>Manages projects within an organization.</summary>
        public const string Manager = "Manager";

        /// <summary>End user who executes assigned tasks.</summary>
        public const string User = "User";

        /// <summary>All roles that operate within a single tenant.</summary>
        public static readonly string[] TenantRoles =
        {
            OrganizationAdmin,
            Manager,
            User
        };

        /// <summary>All roles that can manage other users.</summary>
        public static readonly string[] UserManagers =
        {
            SuperAdmin,
            OrganizationAdmin,
            Manager
        };

        /// <summary>Roles allowed to create/edit projects and tasks.</summary>
        public static readonly string[] ContentManagers =
        {
            SuperAdmin,
            OrganizationAdmin,
            Manager
        };
    }
}
