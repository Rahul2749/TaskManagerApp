using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskManager.Models;

/// <summary>Predecessor → successor edge between tasks (finish-to-start by default).</summary>
public class TaskDependency
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public int PredecessorTaskId { get; set; }
    public int SuccessorTaskId { get; set; }

    /// <summary>FS | SS | FF | SF — finish-to-start is the default.</summary>
    [Required, MaxLength(10)]
    public string DependencyType { get; set; } = "FS";

    public int LagDays { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Organization Organization { get; set; } = null!;
    public TaskItem PredecessorTask { get; set; } = null!;
    public TaskItem SuccessorTask { get; set; } = null!;
}

/// <summary>Logged work against a task (Professional+ time_tracking).</summary>
public class TimeEntry
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public int TaskId { get; set; }
    public int UserId { get; set; }

    /// <summary>Work day (UTC date component).</summary>
    public DateTime WorkDate { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Hours { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Organization Organization { get; set; } = null!;
    public TaskItem Task { get; set; } = null!;
    public User User { get; set; } = null!;
}

/// <summary>Simple if-this-then-that rule for a workspace (Professional+).</summary>
public class AutomationRule
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    /// <summary>task_created | task_status_changed | due_soon</summary>
    [Required, MaxLength(40)]
    public string TriggerType { get; set; } = "task_status_changed";

    /// <summary>JSON, e.g. {"toStatus":"Completed"} or {"daysBeforeDue":1}.</summary>
    public string TriggerConfigJson { get; set; } = "{}";

    /// <summary>set_status | add_comment | assign_user</summary>
    [Required, MaxLength(40)]
    public string ActionType { get; set; } = "add_comment";

    /// <summary>JSON, e.g. {"status":"Closed"} or {"comment":"..."} or {"userId":1}.</summary>
    public string ActionConfigJson { get; set; } = "{}";

    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastRunAt { get; set; }

    public Organization Organization { get; set; } = null!;
}
