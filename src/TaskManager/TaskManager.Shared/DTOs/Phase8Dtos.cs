using System.ComponentModel.DataAnnotations;

namespace TaskManager.Shared.DTOs;

public sealed class AuditLogEntryDto
{
    public long Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? ActorEmail { get; set; }
    public int? ActorUserId { get; set; }
    public string? DetailsJson { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class OrganizationSsoConfigDto
{
    public string Provider { get; set; } = "google";
    public string ClientId { get; set; } = string.Empty;
    public bool HasClientSecret { get; set; }
    public string? TenantId { get; set; }
    public string AllowedEmailDomains { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool AutoProvisionUsers { get; set; } = true;
    public string DefaultRole { get; set; } = "User";
    public string? LoginStartPath { get; set; }
}

public sealed class UpsertOrganizationSsoConfigDto
{
    [Required, MaxLength(40)]
    public string Provider { get; set; } = "google";

    [Required, MaxLength(200)]
    public string ClientId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ClientSecret { get; set; }

    [MaxLength(100)]
    public string? TenantId { get; set; }

    [MaxLength(500)]
    public string AllowedEmailDomains { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }
    public bool AutoProvisionUsers { get; set; } = true;

    [MaxLength(40)]
    public string DefaultRole { get; set; } = "User";
}

public sealed class SsoProviderInfoDto
{
    public string OrgSlug { get; set; } = string.Empty;
    public string OrgName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string StartUrl { get; set; } = string.Empty;
}

public sealed class SsoExchangeDto
{
    [Required]
    public string Code { get; set; } = string.Empty;
}
