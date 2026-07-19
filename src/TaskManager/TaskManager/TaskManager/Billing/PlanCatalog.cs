namespace TaskManager.Billing
{
    /// <summary>
    /// In-memory definition of a plan tier and its entitlements. Used both to seed the
    /// database and as a safe fallback so entitlement checks work even before billing
    /// tables are populated.
    /// </summary>
    public sealed class PlanDefinition
    {
        public required string Code { get; init; }
        public required string Name { get; init; }
        public required string Description { get; init; }
        public required int SortOrder { get; init; }
        public int TrialDays { get; init; }

        /// <summary>Price per seat per month, in INR. 0 = free or custom.</summary>
        public decimal MonthlyPricePerSeat { get; init; }

        /// <summary>Price per seat per year, in INR. 0 = free or custom.</summary>
        public decimal AnnualPricePerSeat { get; init; }

        public string Currency { get; init; } = "INR";

        /// <summary>Enterprise uses "contact sales" rather than self-serve pricing.</summary>
        public bool IsCustomPricing { get; init; }

        /// <summary>Boolean capability flags enabled on this plan.</summary>
        public HashSet<string> Features { get; init; } = new();

        /// <summary>Numeric limits. Missing key OR null value = unlimited.</summary>
        public Dictionary<string, long?> Limits { get; init; } = new();

        public bool HasFeature(string key) => Features.Contains(key);

        /// <summary>Returns the limit for a key, or null when unlimited/undefined.</summary>
        public long? GetLimit(string key) =>
            Limits.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>The canonical 5-tier catalog (India / INR / per-seat).</summary>
    public static class PlanCatalog
    {
        public static readonly IReadOnlyList<PlanDefinition> Plans = new List<PlanDefinition>
        {
            new PlanDefinition
            {
                Code = PlanCodes.Free,
                Name = "Free",
                Description = "For individuals getting started.",
                SortOrder = 0,
                MonthlyPricePerSeat = 0,
                AnnualPricePerSeat = 0,
                Features = { FeatureKeys.BoardView },
                Limits =
                {
                    [LimitKeys.MaxProjects] = 2,
                    [LimitKeys.MaxSeats] = 3,
                    [LimitKeys.StorageMb] = 100,
                    [LimitKeys.FileSizeMb] = 10,
                    [LimitKeys.ApiCallsPerMonth] = 0,
                    [LimitKeys.AutomationRunsPerMonth] = 0,
                }
            },
            new PlanDefinition
            {
                Code = PlanCodes.Starter,
                Name = "Starter",
                Description = "For small teams organizing their work.",
                SortOrder = 1,
                TrialDays = 14,
                MonthlyPricePerSeat = 149,
                AnnualPricePerSeat = 1490,
                Features = { FeatureKeys.BoardView, FeatureKeys.CalendarView },
                Limits =
                {
                    [LimitKeys.MaxProjects] = null,
                    [LimitKeys.MaxSeats] = 10,
                    [LimitKeys.StorageMb] = 5120,
                    [LimitKeys.FileSizeMb] = 25,
                    [LimitKeys.ApiCallsPerMonth] = 0,
                    [LimitKeys.AutomationRunsPerMonth] = 0,
                }
            },
            new PlanDefinition
            {
                Code = PlanCodes.Professional,
                Name = "Professional",
                Description = "For growing teams that need advanced project management.",
                SortOrder = 2,
                TrialDays = 14,
                MonthlyPricePerSeat = 399,
                AnnualPricePerSeat = 3990,
                Features =
                {
                    FeatureKeys.BoardView, FeatureKeys.CalendarView, FeatureKeys.TimelineGantt,
                    FeatureKeys.CustomFields, FeatureKeys.Automations, FeatureKeys.TimeTracking,
                    FeatureKeys.Integrations, FeatureKeys.PublicApi
                },
                Limits =
                {
                    [LimitKeys.MaxProjects] = null,
                    [LimitKeys.MaxSeats] = 50,
                    [LimitKeys.StorageMb] = 102400,
                    [LimitKeys.FileSizeMb] = 100,
                    [LimitKeys.ApiCallsPerMonth] = 100000,
                    [LimitKeys.AutomationRunsPerMonth] = 10000,
                }
            },
            new PlanDefinition
            {
                Code = PlanCodes.Business,
                Name = "Business",
                Description = "For larger organizations that need control and insight.",
                SortOrder = 3,
                TrialDays = 14,
                MonthlyPricePerSeat = 799,
                AnnualPricePerSeat = 7990,
                Features =
                {
                    FeatureKeys.BoardView, FeatureKeys.CalendarView, FeatureKeys.TimelineGantt,
                    FeatureKeys.CustomFields, FeatureKeys.Automations, FeatureKeys.TimeTracking,
                    FeatureKeys.Integrations, FeatureKeys.PublicApi, FeatureKeys.AdvancedReports,
                    FeatureKeys.GuestUsers, FeatureKeys.Sso, FeatureKeys.AuditLog
                },
                Limits =
                {
                    [LimitKeys.MaxProjects] = null,
                    [LimitKeys.MaxSeats] = 200,
                    [LimitKeys.StorageMb] = 1048576,
                    [LimitKeys.FileSizeMb] = 250,
                    [LimitKeys.ApiCallsPerMonth] = 1000000,
                    [LimitKeys.AutomationRunsPerMonth] = 100000,
                }
            },
            new PlanDefinition
            {
                Code = PlanCodes.Enterprise,
                Name = "Enterprise",
                Description = "For enterprises needing security, scale and dedicated support.",
                SortOrder = 4,
                IsCustomPricing = true,
                Features =
                {
                    FeatureKeys.BoardView, FeatureKeys.CalendarView, FeatureKeys.TimelineGantt,
                    FeatureKeys.CustomFields, FeatureKeys.Automations, FeatureKeys.TimeTracking,
                    FeatureKeys.Integrations, FeatureKeys.PublicApi, FeatureKeys.AdvancedReports,
                    FeatureKeys.GuestUsers, FeatureKeys.Sso, FeatureKeys.SamlScim,
                    FeatureKeys.AuditLog, FeatureKeys.CustomRoles, FeatureKeys.PrioritySupport
                },
                Limits =
                {
                    [LimitKeys.MaxProjects] = null,
                    [LimitKeys.MaxSeats] = null,
                    [LimitKeys.StorageMb] = null,
                    [LimitKeys.FileSizeMb] = 1024,
                    [LimitKeys.ApiCallsPerMonth] = null,
                    [LimitKeys.AutomationRunsPerMonth] = null,
                }
            },
        };

        public static PlanDefinition Free => GetByCode(PlanCodes.Free)!;

        public static PlanDefinition? GetByCode(string? code) =>
            code is null ? null : Plans.FirstOrDefault(p => p.Code == code);
    }
}
