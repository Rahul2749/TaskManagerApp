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
    }
}
