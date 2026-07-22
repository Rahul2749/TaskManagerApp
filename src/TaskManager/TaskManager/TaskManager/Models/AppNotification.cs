using System.ComponentModel.DataAnnotations;

namespace TaskManager.Models;

/// <summary>In-app notification delivered to a specific user.</summary>
public class AppNotification
{
    public int Id { get; set; }

    public int OrganizationId { get; set; }

    public int UserId { get; set; }

    [Required, MaxLength(50)]
    public string Type { get; set; } = "info";

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional deep link, e.g. /user/task/12</summary>
    [MaxLength(300)]
    public string? LinkUrl { get; set; }

    public int? TaskId { get; set; }

    public int? ActorUserId { get; set; }

    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Organization Organization { get; set; } = null!;
    public User User { get; set; } = null!;
    public User? ActorUser { get; set; }
    public TaskItem? Task { get; set; }
}

public static class NotificationTypes
{
    public const string Mention = "mention";
    public const string Comment = "comment";
    public const string TaskAssigned = "task_assigned";
    public const string TaskStatus = "task_status";
    public const string Info = "info";
}
