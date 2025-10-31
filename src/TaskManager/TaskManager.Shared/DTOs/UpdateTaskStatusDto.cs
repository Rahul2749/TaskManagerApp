using System.ComponentModel.DataAnnotations;

namespace TaskManager.Shared.DTOs
{
    public class UpdateTaskStatusDto
    {
        [Required]
        public string Status { get; set; } = string.Empty;

        public decimal? ActualHours { get; set; }

        public string? Comment { get; set; }
    }
}
