using System.ComponentModel.DataAnnotations;

namespace TaskManager.Models
{
    /// <summary>
    /// Tracks metered usage per organization for enforcing numeric plan limits
    /// (e.g. storage used, API calls this month).
    /// </summary>
    public class UsageCounter
    {
        public int Id { get; set; }

        public int OrganizationId { get; set; }

        [Required, MaxLength(60)]
        public string Key { get; set; } = string.Empty;

        /// <summary>Reset window, e.g. "2026-07" for monthly counters or "lifetime".</summary>
        [Required, MaxLength(20)]
        public string Period { get; set; } = "lifetime";

        public long Value { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Organization Organization { get; set; } = null!;
    }
}
