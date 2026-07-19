using System.ComponentModel.DataAnnotations;

namespace TaskManager.Models
{
    /// <summary>
    /// An organization's current subscription. Exactly one per organization.
    /// The payment provider (Razorpay) is the source of truth for status; webhooks
    /// keep these fields in sync.
    /// </summary>
    public class Subscription
    {
        public int Id { get; set; }

        public int OrganizationId { get; set; }

        public int PlanId { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = "trialing";

        [Required, MaxLength(20)]
        public string BillingInterval { get; set; } = "monthly";

        public int Seats { get; set; } = 1;

        public DateTime? CurrentPeriodStart { get; set; }
        public DateTime? CurrentPeriodEnd { get; set; }
        public DateTime? TrialEndsAt { get; set; }

        public bool CancelAtPeriodEnd { get; set; }
        public DateTime? CanceledAt { get; set; }

        [MaxLength(30)]
        public string Provider { get; set; } = "razorpay";

        [MaxLength(100)]
        public string? ProviderCustomerId { get; set; }

        [MaxLength(100)]
        public string? ProviderSubscriptionId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Organization Organization { get; set; } = null!;
        public Plan Plan { get; set; } = null!;
    }
}
