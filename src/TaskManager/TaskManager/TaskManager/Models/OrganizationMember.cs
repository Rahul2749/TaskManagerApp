using System.ComponentModel.DataAnnotations;

namespace TaskManager.Models
{
    /// <summary>
    /// Membership of a user within an organization, carrying the role that user holds
    /// inside THAT organization (OrganizationAdmin, Manager or User).
    /// This keeps the platform-level role independent from per-org responsibilities
    /// and lets a user belong to multiple organizations if needed.
    /// </summary>
    public class OrganizationMember
    {
        public int Id { get; set; }

        public int OrganizationId { get; set; }

        public int UserId { get; set; }

        /// <summary>
        /// Role within this organization: OrganizationAdmin, Manager, User.
        /// </summary>
        [Required, MaxLength(20)]
        public string Role { get; set; } = "User";

        public bool IsActive { get; set; } = true;

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Organization Organization { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}
