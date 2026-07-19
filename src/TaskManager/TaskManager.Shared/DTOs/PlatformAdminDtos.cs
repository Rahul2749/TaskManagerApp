using System.ComponentModel.DataAnnotations;

namespace TaskManager.Shared.DTOs;

public sealed class PlatformSummaryDto
{
    public int TotalOrganizations { get; set; }
    public int ActiveOrganizations { get; set; }
    public int SuspendedOrganizations { get; set; }
    public int TotalUsers { get; set; }
    public int ActiveSubscriptions { get; set; }
    public decimal EstimatedMonthlyRecurringRevenue { get; set; }
}

public sealed class PlatformOrganizationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int UserCount { get; set; }
    public int ProjectCount { get; set; }
    public string PlanName { get; set; } = "Free";
    public string SubscriptionStatus { get; set; } = "active";
    public int Seats { get; set; }
}

public sealed class PlatformOrganizationStatusDto
{
    [Required]
    public string Status { get; set; } = string.Empty;
}
