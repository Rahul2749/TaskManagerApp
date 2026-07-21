using System.ComponentModel.DataAnnotations;

namespace TaskManager.Shared.DTOs;

public sealed class OrganizationSettingsDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Kolkata";
    public string? BrandPrimaryColor { get; set; }
    public bool OnboardingCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class UpdateOrganizationSettingsDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    [Required, MaxLength(100)]
    public string TimeZoneId { get; set; } = "Asia/Kolkata";

    [MaxLength(20)]
    public string? BrandPrimaryColor { get; set; }
}

public sealed class OnboardingStatusDto
{
    public bool Completed { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public int ProjectCount { get; set; }
    public int PendingInviteCount { get; set; }
    public int MemberCount { get; set; }
}
