namespace TaskManager.Shared.Billing
{
    /// <summary>Boolean capability flags gated by subscription plan.</summary>
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
    }

    public static class PlanCodes
    {
        public const string Free = "free";
        public const string Starter = "starter";
        public const string Professional = "professional";
        public const string Business = "business";
        public const string Enterprise = "enterprise";
    }

    public static class BillingIntervals
    {
        public const string Monthly = "monthly";
        public const string Annual = "annual";
    }
}
