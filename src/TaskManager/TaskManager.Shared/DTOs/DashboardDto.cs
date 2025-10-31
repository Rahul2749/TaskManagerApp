namespace TaskManager.Shared.DTOs
{
    public class DashboardDto
    {
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int OverdueTasks { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }

        public List<ProjectTaskSummary> ProjectSummaries { get; set; } = new();
        public List<TaskDto> RecentTasks { get; set; } = new();
        public List<TaskDto> UpcomingDeadlines { get; set; } = new();
    }
    public class ProjectTaskSummary
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int InProgressTasks { get; set; }
        public double CompletionPercentage { get; set; }
    }
}
