namespace TaskManager.Shared.DTOs.Billing
{
    /// <summary>A subscription plan as shown on the pricing page.</summary>
    public class PlanDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SortOrder { get; set; }
        public int TrialDays { get; set; }
        public bool IsCustomPricing { get; set; }
        public string Currency { get; set; } = "INR";
        public decimal MonthlyPricePerSeat { get; set; }
        public decimal AnnualPricePerSeat { get; set; }

        /// <summary>Enabled boolean feature keys.</summary>
        public List<string> Features { get; set; } = new();

        /// <summary>Numeric limits (null value = unlimited).</summary>
        public Dictionary<string, long?> Limits { get; set; } = new();
    }
}
