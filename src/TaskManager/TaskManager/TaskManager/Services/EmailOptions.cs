namespace TaskManager.Services;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string ApiKey { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "TaskManager";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(FromAddress);
}

public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Public site URL used in email links, e.g. https://taskmanager-app-plt1.onrender.com</summary>
    public string PublicBaseUrl { get; set; } = "https://localhost:7294";

    /// <summary>Days a past_due subscription keeps paid entitlements before soft-limiting to Free.</summary>
    public int BillingGracePeriodDays { get; set; } = 7;
}
