using System.ComponentModel.DataAnnotations;

namespace TaskManager.Models
{
    /// <summary>
    /// A tenant (customer workspace) in the multi-tenant model.
    /// Every project, task and organization-scoped user belongs to exactly one organization.
    /// SuperAdmin users are NOT tied to an organization (they manage the platform itself).
    /// </summary>
    public class Organization
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// URL-friendly identifier. Unique across the platform.
        /// </summary>
        [Required, MaxLength(100)]
        public string Slug { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>
        /// Tenant lifecycle: Trial, Active, Suspended, Archived.
        /// </summary>
        [Required, MaxLength(20)]
        public string Status { get; set; } = "Active";

        public string? LogoUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<OrganizationMember> Members { get; set; } = new List<OrganizationMember>();
        public ICollection<Project> Projects { get; set; } = new List<Project>();
    }
}
