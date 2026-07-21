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

        /// <summary>IANA timezone id, e.g. Asia/Kolkata.</summary>
        [MaxLength(100)]
        public string TimeZoneId { get; set; } = "Asia/Kolkata";

        /// <summary>Optional brand accent color as CSS hex, e.g. #4F46E5.</summary>
        [MaxLength(20)]
        public string? BrandPrimaryColor { get; set; }

        /// <summary>When the org admin finished the first-run wizard; null = still needed.</summary>
        public DateTime? OnboardingCompletedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<OrganizationMember> Members { get; set; } = new List<OrganizationMember>();
        public ICollection<Project> Projects { get; set; } = new List<Project>();
    }
}
