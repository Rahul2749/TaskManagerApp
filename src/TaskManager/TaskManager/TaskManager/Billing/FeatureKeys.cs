namespace TaskManager.Billing
{
    /// <summary>
    /// Boolean capability flags gated by subscription plan. Stored as entitlement keys
    /// so plans can be reconfigured without code changes.
    /// </summary>
    public static class FeatureKeys
    {
        public const string BoardView = "board_view";
        public const string CalendarView = "calendar_view";
        public const string TimelineGantt = "timeline_gantt";
        public const string CustomFields = "custom_fields";
        public const string Automations = "automations";
        public const string TimeTracking = "time_tracking";
        public const string Integrations = "integrations";
        public const string PublicApi = "public_api";
        public const string AdvancedReports = "advanced_reports";
        public const string GuestUsers = "guest_users";
        public const string Sso = "sso";
        public const string SamlScim = "saml_scim";
        public const string AuditLog = "audit_log";
        public const string CustomRoles = "custom_roles";
        public const string PrioritySupport = "priority_support";

        public static readonly string[] All =
        {
            BoardView, CalendarView, TimelineGantt, CustomFields, Automations,
            TimeTracking, Integrations, PublicApi, AdvancedReports, GuestUsers,
            Sso, SamlScim, AuditLog, CustomRoles, PrioritySupport
        };
    }

    /// <summary>
    /// Numeric usage limits gated by subscription plan. A null limit means "unlimited".
    /// </summary>
    public static class LimitKeys
    {
        public const string MaxProjects = "max_projects";
        public const string MaxSeats = "max_seats";
        public const string StorageMb = "storage_mb";
        public const string FileSizeMb = "file_size_mb";
        public const string ApiCallsPerMonth = "api_calls_per_month";
        public const string AutomationRunsPerMonth = "automation_runs_per_month";

        public static readonly string[] All =
        {
            MaxProjects, MaxSeats, StorageMb, FileSizeMb,
            ApiCallsPerMonth, AutomationRunsPerMonth
        };
    }

    /// <summary>Canonical plan codes.</summary>
    public static class PlanCodes
    {
        public const string Free = "free";
        public const string Starter = "starter";
        public const string Professional = "professional";
        public const string Business = "business";
        public const string Enterprise = "enterprise";
    }

    /// <summary>Subscription lifecycle states (aligned with provider webhooks).</summary>
    public static class SubscriptionStatus
    {
        public const string Trialing = "trialing";
        public const string Active = "active";
        public const string PastDue = "past_due";
        public const string Canceled = "canceled";
        public const string Incomplete = "incomplete";

        /// <summary>Statuses that grant access to paid entitlements.</summary>
        public static bool GrantsAccess(string status) =>
            status == Trialing || status == Active || status == PastDue;
    }

    public static class BillingIntervals
    {
        public const string Monthly = "monthly";
        public const string Annual = "annual";
    }
}
