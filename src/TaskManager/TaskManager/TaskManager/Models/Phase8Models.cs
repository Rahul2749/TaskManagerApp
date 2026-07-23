using System.ComponentModel.DataAnnotations;

namespace TaskManager.Models;

public class AuditLogEntry
{
    public long Id { get; set; }
    public int? OrganizationId { get; set; }
    public int? ActorUserId { get; set; }

    [MaxLength(200)]
    public string? ActorEmail { get; set; }

    [Required, MaxLength(80)]
    public string Action { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string EntityType { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? EntityId { get; set; }

    public string? DetailsJson { get; set; }

    [MaxLength(64)]
    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Organization? Organization { get; set; }
    public User? ActorUser { get; set; }
}

public class OrganizationSsoConfig
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }

    /// <summary>google | microsoft</summary>
    [Required, MaxLength(40)]
    public string Provider { get; set; } = "google";

    [Required, MaxLength(200)]
    public string ClientId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ClientSecret { get; set; }

    /// <summary>Azure AD tenant id, or "common".</summary>
    [MaxLength(100)]
    public string? TenantId { get; set; }

    /// <summary>Comma-separated email domains allowed to join via SSO, e.g. acme.com</summary>
    [MaxLength(500)]
    public string AllowedEmailDomains { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }
    public bool AutoProvisionUsers { get; set; } = true;

    [MaxLength(40)]
    public string DefaultRole { get; set; } = "User";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Organization Organization { get; set; } = null!;
}
