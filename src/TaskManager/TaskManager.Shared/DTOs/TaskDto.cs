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
    }
}
