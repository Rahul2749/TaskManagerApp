using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskManager.Models
{
    public class TaskItem
    {
        public int Id { get; set; }

        [Required, MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int ProjectId { get; set; }

        /// <summary>
        /// Denormalized tenant key copied from the owning project so tasks can be
        /// filtered by tenant without always joining through the project.
        /// </summary>
        public int OrganizationId { get; set; }

        public int? AssignedToId { get; set; }

        public int AssignedById { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = "NotAssigned"; // NotAssigned, Assigned, InProgress, Completed, Tested, Closed

        [Required, MaxLength(20)]
        public string Priority { get; set; } = "Medium"; // Low, Medium, High, Critical

        [Column(TypeName = "decimal(10,2)")]
        public decimal? EstimatedHours { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal ActualHours { get; set; } = 0;

        public DateTime? StartDate { get; set; }

        public DateTime? DueDate { get; set; }

        public DateTime? CompletedDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Project Project { get; set; } = null!;
        public User? AssignedTo { get; set; }
        public User AssignedBy { get; set; } = null!;
        public ICollection<TaskHistory> History { get; set; } = new List<TaskHistory>();
        public ICollection<Subtask> Subtasks { get; set; } = new List<Subtask>();
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
        public ICollection<TaskTag> TaskTags { get; set; } = new List<TaskTag>();
        public ICollection<TaskWatcher> Watchers { get; set; } = new List<TaskWatcher>();

        // ── Recurrence (Phase 6) ────────────────────────────────────────────
        /// <summary>none | daily | weekly | monthly</summary>
        [MaxLength(20)]
        public string RecurrenceFrequency { get; set; } = "none";

        public int RecurrenceInterval { get; set; } = 1;
        public DateTime? RecurrenceEndDate { get; set; }

        /// <summary>Template task that spawned this instance (null for templates / non-recurring).</summary>
        public int? RecurrenceParentTaskId { get; set; }

        /// <summary>Next UTC time to spawn a child instance (templates only).</summary>
        public DateTime? NextOccurrenceAt { get; set; }

        public TaskItem? RecurrenceParentTask { get; set; }
    }
}
