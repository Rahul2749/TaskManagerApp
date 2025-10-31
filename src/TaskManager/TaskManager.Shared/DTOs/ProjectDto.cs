using System.ComponentModel.DataAnnotations;

namespace TaskManager.Shared.DTOs
{
    public class ProjectDto
    {
        public int? Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        [Required]
        public string Status { get; set; } = "Active";

        public int? ManagerId { get; set; }

        public UserDto? Manager { get; set; }

        public List<UserDto> AssignedUsers { get; set; } = new();

        public int TaskCount { get; set; }

        public int CompletedTaskCount { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
}
