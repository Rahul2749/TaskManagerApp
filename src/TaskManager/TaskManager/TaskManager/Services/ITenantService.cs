using System.Security.Claims;

namespace TaskManager.Services
{
    /// <summary>
    /// Resolves the current tenant (organization) from the authenticated request.
    /// Implemented as a scoped service so a single value is computed per HTTP request
    /// and reused by the EF Core query filters in <see cref="Data.ApplicationDbContext"/>.
    /// </summary>
    public interface ITenantService
    {
        /// <summary>The organization id of the current user, or null for SuperAdmin/anonymous.</summary>
        int? OrganizationId { get; }

        /// <summary>The platform role of the current user (SuperAdmin, OrganizationAdmin, ...).</summary>
        string? Role { get; }

        int? UserId { get; }

        /// <summary>True when the current user is platform-wide (SuperAdmin) and must bypass the tenant filter.</summary>
        bool IsSuperAdmin { get; }
    }

    public class TenantService : ITenantService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TenantService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;

            var user = httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                UserId = ParseInt(user.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                Role = user.FindFirst(ClaimTypes.Role)?.Value;
                OrganizationId = ParseInt(user.FindFirst("organizationId")?.Value);
                IsSuperAdmin = Role == Models.Roles.SuperAdmin;
            }
        }

        public int? OrganizationId { get; }
        public string? Role { get; }
        public int? UserId { get; }
        public bool IsSuperAdmin { get; }

        private static int? ParseInt(string? value) =>
            int.TryParse(value, out var result) ? result : null;
    }
}
