namespace TaskManager.Shared.DTOs;

public sealed class AppNotificationDto
{
    public int Id { get; set; }
    public string Type { get; set; } = "info";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public int? TaskId { get; set; }
    public string? ActorName { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ActivityItemDto
{
    public string Kind { get; set; } = "task_change";
    public string Summary { get; set; } = string.Empty;
    public string? ActorName { get; set; }
    public int? TaskId { get; set; }
    public string? TaskTitle { get; set; }
    public int? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public DateTime OccurredAt { get; set; }
}

public sealed class RealtimeEventDto
{
    public string EventType { get; set; } = string.Empty;
    public int? TaskId { get; set; }
    public int? ProjectId { get; set; }
    public string? Message { get; set; }
}
