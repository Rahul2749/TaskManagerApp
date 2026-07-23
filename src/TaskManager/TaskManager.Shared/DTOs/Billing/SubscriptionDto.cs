namespace TaskManager.Shared.DTOs.Billing
{
    /// <summary>The current subscription state for an organization plus its entitlements.</summary>
    public class SubscriptionDto
    {
        public string PlanCode { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string BillingInterval { get; set; } = "monthly";
        public int Seats { get; set; }
        public DateTime? CurrentPeriodEnd { get; set; }
        public DateTime? TrialEndsAt { get; set; }
        public bool CancelAtPeriodEnd { get; set; }
        public bool IsActive { get; set; }
        public DateTime? PastDueSince { get; set; }
        public DateTime? GraceEndsAt { get; set; }
        public int GraceDaysRemaining { get; set; }
        public bool IsSoftLimited { get; set; }

        /// <summary>Resolved boolean features for this org.</summary>
        public List<string> Features { get; set; } = new();

        /// <summary>Resolved numeric limits (null = unlimited).</summary>
        public Dictionary<string, long?> Limits { get; set; } = new();
    }
}
