namespace TaskManager.Data;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public bool CreateSuperAdmin { get; set; }
    public bool SeedDemoData { get; set; }
    public string SuperAdminUsername { get; set; } = "superadmin";
    public string SuperAdminEmail { get; set; } = "superadmin@taskmanager.local";
    public string? SuperAdminPassword { get; set; }
}
