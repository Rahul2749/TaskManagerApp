namespace TaskManager.Services;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; set; } = "smtp";
    public string FromAddress { get; set; } = "noreply@taskmanager.local";
    public string FromName { get; set; } = "TaskManager";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(FromAddress);
}

public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Public site URL used in email links, e.g. https://taskmanager-app-plt1.onrender.com</summary>
    public string PublicBaseUrl { get; set; } = "https://localhost:7294";
}
