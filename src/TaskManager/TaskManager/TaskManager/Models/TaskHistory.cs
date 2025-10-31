using System.ComponentModel.DataAnnotations;

namespace TaskManager.Models
{
    public class TaskHistory
    {
        public int Id { get; set; }

        public int TaskId { get; set; }

        public int ChangedById { get; set; }

        [Required, MaxLength(100)]
        public string FieldName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? OldValue { get; set; }

        [MaxLength(500)]
        public string? NewValue { get; set; }

        public string? Comment { get; set; }

        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public TaskItem Task { get; set; } = null!;
        public User ChangedBy { get; set; } = null!;
    }
}
