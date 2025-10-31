using System.ComponentModel.DataAnnotations;

namespace TaskManager.Shared.DTOs
{
    public class ProjectUserMappingDto
    {
        [Required]
        public int ProjectId { get; set; }

        [Required]
        public List<int> UserIds { get; set; } = new();
    }
}
