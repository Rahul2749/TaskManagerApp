using System.ComponentModel.DataAnnotations;

namespace TaskManager.Shared.DTOs
{
    public class SubtaskDto
    {
        public int? Id { get; set; }

        public int TaskId { get; set; }

        [Required, MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        public bool IsCompleted { get; set; }

        public int SortOrder { get; set; }

        public int? AssignedToId { get; set; }
        public UserDto? AssignedTo { get; set; }

        public DateTime? DueDate { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
