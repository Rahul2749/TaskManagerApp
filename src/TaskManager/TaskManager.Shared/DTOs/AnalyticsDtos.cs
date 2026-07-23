namespace TaskManager.Shared.DTOs;

public sealed class WorkspaceAnalyticsDto
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public int TotalProjects { get; set; }
    public int ActiveProjects { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int OverdueTasks { get; set; }
    public int TotalUsers { get; set; }
    public double CompletionRate { get; set; }
    public decimal HoursLoggedThisWeek { get; set; }
    public List<NamedCountDto> StatusBreakdown { get; set; } = [];
    public List<NamedCountDto> PriorityBreakdown { get; set; } = [];
    public List<ProjectTaskSummary> ProjectSummaries { get; set; } = [];
}

public sealed class NamedCountDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percent { get; set; }
}
