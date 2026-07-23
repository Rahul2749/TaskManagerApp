using System.ComponentModel.DataAnnotations;

namespace TaskManager.Shared.DTOs;

public sealed class TaskDependencyDto
{
    public int Id { get; set; }
    public int PredecessorTaskId { get; set; }
    public string PredecessorTitle { get; set; } = string.Empty;
    public int SuccessorTaskId { get; set; }
    public string SuccessorTitle { get; set; } = string.Empty;
    public string DependencyType { get; set; } = "FS";
    public int LagDays { get; set; }
}

public sealed class CreateTaskDependencyDto
{
    [Required]
    public int PredecessorTaskId { get; set; }

    [Required]
    public int SuccessorTaskId { get; set; }

    [MaxLength(10)]
    public string DependencyType { get; set; } = "FS";

    public int LagDays { get; set; }
}

public sealed class TimelineTaskDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
}

public sealed class TimelineDto
{
    public List<TimelineTaskDto> Tasks { get; set; } = [];
    public List<TaskDependencyDto> Dependencies { get; set; } = [];
}

public sealed class TimeEntryDto
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public string? TaskTitle { get; set; }
    public int UserId { get; set; }
    public string? UserName { get; set; }
    public DateTime WorkDate { get; set; }
    public decimal Hours { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class CreateTimeEntryDto
{
    [Required]
    public int TaskId { get; set; }

    [Required]
    public DateTime WorkDate { get; set; }

    [Range(0.25, 24)]
    public decimal Hours { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public sealed class TimesheetSummaryDto
{
    public DateTime WeekStart { get; set; }
    public decimal TotalHours { get; set; }
    public List<TimeEntryDto> Entries { get; set; } = [];
}

public sealed class AutomationRuleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TriggerType { get; set; } = string.Empty;
    public string TriggerConfigJson { get; set; } = "{}";
    public string ActionType { get; set; } = string.Empty;
    public string ActionConfigJson { get; set; } = "{}";
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastRunAt { get; set; }
}

public sealed class UpsertAutomationRuleDto
{
    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string TriggerType { get; set; } = "task_status_changed";

    public string TriggerConfigJson { get; set; } = "{}";

    [Required, MaxLength(40)]
    public string ActionType { get; set; } = "add_comment";

    public string ActionConfigJson { get; set; } = "{}";

    public bool IsEnabled { get; set; } = true;
}

public sealed class SetRecurrenceDto
{
    /// <summary>none | daily | weekly | monthly</summary>
    [Required, MaxLength(20)]
    public string Frequency { get; set; } = "none";

    [Range(1, 365)]
    public int Interval { get; set; } = 1;

    public DateTime? EndDate { get; set; }
}
