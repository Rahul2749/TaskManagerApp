using TaskManager.Models;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mapping;

public static class ProjectSummaryQueries
{
    public static IQueryable<ProjectTaskSummary> SelectTaskCounts(this IQueryable<Project> projects) =>
        projects.Select(p => new ProjectTaskSummary
        {
            ProjectId = p.Id,
            ProjectName = p.Name,
            TotalTasks = p.Tasks.Count,
            CompletedTasks = p.Tasks.Count(t => t.Status == "Completed" || t.Status == "Tested" || t.Status == "Closed"),
            InProgressTasks = p.Tasks.Count(t => t.Status == "InProgress")
        });

    public static void SetCompletionPercentages(this IEnumerable<ProjectTaskSummary> summaries, int decimals)
    {
        // Run after materialization. Provider-side projection splitting can evaluate
        // division before a client-side conditional/Math.Round guard for empty projects.
        foreach (var summary in summaries)
            summary.CompletionPercentage = summary.TotalTasks > 0
                ? Math.Round(summary.CompletedTasks * 100.0 / summary.TotalTasks, decimals)
                : 0;
    }
}
