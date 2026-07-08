using System.ComponentModel.DataAnnotations;

namespace TaskManager.Shared.DTOs
{
    public class TagDto
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(7)]
        public string Color { get; set; } = "#6366f1";

        public int? OrganizationId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
