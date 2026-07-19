namespace TaskManager.Shared.Billing
{
    /// <summary>Human-readable labels for plan features shown in the UI.</summary>
    public static class FeatureLabels
    {
        private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
        {
            [FeatureKeys.BoardView] = "Kanban board view",
            [FeatureKeys.CalendarView] = "Calendar view",
            [FeatureKeys.TimelineGantt] = "Timeline & Gantt",
            [FeatureKeys.CustomFields] = "Custom fields",
            [FeatureKeys.Automations] = "Workflow automations",
            [FeatureKeys.TimeTracking] = "Time tracking",
            [FeatureKeys.Integrations] = "Third-party integrations",
            [FeatureKeys.PublicApi] = "Public REST API",
            [FeatureKeys.AdvancedReports] = "Advanced reports",
            [FeatureKeys.GuestUsers] = "Guest users",
            [FeatureKeys.Sso] = "Single sign-on (SSO)",
            [FeatureKeys.SamlScim] = "SAML & SCIM provisioning",
            [FeatureKeys.AuditLog] = "Audit log",
            [FeatureKeys.CustomRoles] = "Custom roles & permissions",
            [FeatureKeys.PrioritySupport] = "Priority support",
        };

        public static string Get(string key) =>
            Labels.TryGetValue(key, out var label) ? label : key.Replace('_', ' ');
    }
}
