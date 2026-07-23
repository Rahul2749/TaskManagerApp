using System.ComponentModel.DataAnnotations;

namespace TaskManager.Shared.DTOs
{
    public class TaskDto
    {
        public int? Id { get; set; }

        [Required, MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public int ProjectId { get; set; }

        public string? ProjectName { get; set; }

        public int? AssignedToId { get; set; }

        public UserDto? AssignedTo { get; set; }

        public int? AssignedById { get; set; }

        public UserDto? AssignedBy { get; set; }

        [Required]
        public string Status { get; set; } = "NotAssigned";

        [Required]
        public string Priority { get; set; } = "Medium";

        [Range(0, 10000)]
        public decimal? EstimatedHours { get; set; }

        public decimal ActualHours { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? DueDate { get; set; }

        public DateTime? CompletedDate { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public List<TaskHistoryDto> History { get; set; } = new();

        // ── Rich-task collections (populated on detail views, empty on list views) ──
        public List<SubtaskDto> Subtasks { get; set; } = new();
        public List<CommentDto> Comments { get; set; } = new();
        public List<AttachmentDto> Attachments { get; set; } = new();
        public List<TagDto> Tags { get; set; } = new();

        /// <summary>Number of users explicitly watching this task (assignee excluded).</summary>
        public int WatcherCount { get; set; }

        /// <summary>Computed checklist completion 0–100. 0 when there are no subtasks.</summary>
        public decimal SubtaskProgress =>
            Subtasks.Count == 0
                ? 0
                : Math.Round((decimal)Subtasks.Count(s => s.IsCompleted) / Subtasks.Count * 100, 0);

        // Recurrence
        public string RecurrenceFrequency { get; set; } = "none";
        public int RecurrenceInterval { get; set; } = 1;
        public DateTime? RecurrenceEndDate { get; set; }
        public int? RecurrenceParentTaskId { get; set; }
        public DateTime? NextOccurrenceAt { get; set; }
    }
}
