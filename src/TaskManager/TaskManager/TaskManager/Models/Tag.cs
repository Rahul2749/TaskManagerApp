using System.ComponentModel.DataAnnotations;

namespace TaskManager.Models
{
    /// <summary>
    /// A label/tag that can be applied to any number of tasks across the tenant.
    /// Tenant-scoped so each org defines its own label palette.
    /// </summary>
    public class Tag
    {
        public int Id { get; set; }

        public int OrganizationId { get; set; }

        [Required, MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Hex color used for the badge in the UI (e.g. #6366f1).</summary>
        [MaxLength(7)]
        public string Color { get; set; } = "#6366f1";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Organization Organization { get; set; } = null!;
        public ICollection<TaskTag> TaskTags { get; set; } = new List<TaskTag>();
    }
}
