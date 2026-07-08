using System.ComponentModel.DataAnnotations;

namespace TaskManager.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required, MaxLength(255), EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(500)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        /// <summary>
        /// Platform role. SuperAdmin is platform-wide; OrganizationAdmin/Manager/User
        /// operate within the scope of <see cref="OrganizationId"/>.
        /// </summary>
        [Required, MaxLength(20)]
        public string Role { get; set; } = Roles.User;

        /// <summary>
        /// The organization this user belongs to. Null only for SuperAdmin accounts
        /// (which are platform-wide and not tenant-scoped).
        /// </summary>
        public int? OrganizationId { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public int? CreatedBy { get; set; }

        // Navigation properties
        public Organization? Organization { get; set; }
        public ICollection<OrganizationMember> OrganizationMemberships { get; set; } = new List<OrganizationMember>();
        public ICollection<Project> ManagedProjects { get; set; } = new List<Project>();
        public ICollection<ProjectUser> ProjectUsers { get; set; } = new List<ProjectUser>();
        public ICollection<TaskItem> AssignedTasks { get; set; } = new List<TaskItem>();
        public ICollection<TaskItem> CreatedTasks { get; set; } = new List<TaskItem>();
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    }
}
