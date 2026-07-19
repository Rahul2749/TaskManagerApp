using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskManager.Models
{
    /// <summary>
    /// A subscription plan tier (Free, Starter, ...). Prices are per seat, in INR.
    /// Provider plan IDs link to the payment gateway's own plan objects.
    /// </summary>
    public class Plan
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int SortOrder { get; set; }

        public bool IsActive { get; set; } = true;

        public int TrialDays { get; set; }

        public bool IsCustomPricing { get; set; }

        [MaxLength(10)]
        public string Currency { get; set; } = "INR";

        [Column(TypeName = "decimal(12,2)")]
        public decimal MonthlyPricePerSeat { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal AnnualPricePerSeat { get; set; }

        /// <summary>Provider (e.g. Razorpay) plan id for the monthly price.</summary>
        [MaxLength(100)]
        public string? ProviderMonthlyPlanId { get; set; }

        [MaxLength(100)]
        public string? ProviderAnnualPlanId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<PlanFeature> Features { get; set; } = new List<PlanFeature>();
    }

    /// <summary>
    /// A single entitlement on a plan: either a boolean feature flag or a numeric limit.
    /// A null <see cref="Limit"/> on a limit-type entitlement means "unlimited".
    /// </summary>
    public class PlanFeature
    {
        public int Id { get; set; }

        public int PlanId { get; set; }

        [Required, MaxLength(60)]
        public string Key { get; set; } = string.Empty;

        /// <summary>True for enabled boolean features.</summary>
        public bool IsEnabled { get; set; }

        /// <summary>Numeric limit for limit-type keys; null = unlimited.</summary>
        public long? Limit { get; set; }

        public Plan Plan { get; set; } = null!;
    }
}
