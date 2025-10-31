namespace TaskManager.Models
{
    public class ProjectUser
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }

        public int UserId { get; set; }

        public DateTime AssignedDate { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Project Project { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}
