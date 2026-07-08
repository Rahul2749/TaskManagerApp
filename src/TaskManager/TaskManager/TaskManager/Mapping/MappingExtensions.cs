using TaskManager.Models;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mapping
{
    /// <summary>
    /// Single source of truth for mapping domain entities to their DTOs.
    /// Eliminates the per-controller duplicate MapTo* methods that had drifted out of sync.
    /// </summary>
    public static class MappingExtensions
    {
        public static UserDto ToDto(this User user) => new()
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            OrganizationId = user.OrganizationId,
            OrganizationName = user.Organization?.Name,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        };

        public static ProjectDto ToDto(this Project project) => new()
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            StartDate = project.StartDate,
            EndDate = project.EndDate,
            Status = project.Status,
            ManagerId = project.ManagerId,
            Manager = project.Manager?.ToDto(),
            AssignedUsers = project.ProjectUsers?.Select(pu => pu.User.ToDto()).ToList() ?? new(),
            TaskCount = project.Tasks?.Count ?? 0,
            CompletedTaskCount = project.Tasks?.Count(t => t.IsCompleted()) ?? 0,
            CreatedAt = project.CreatedAt
        };

        public static TaskDto ToDto(this TaskItem task) => new()
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            ProjectId = task.ProjectId,
            ProjectName = task.Project?.Name,
            AssignedToId = task.AssignedToId,
            AssignedTo = task.AssignedTo?.ToDto(),
            AssignedById = task.AssignedById,
            AssignedBy = task.AssignedBy?.ToDto(),
            Status = task.Status,
            Priority = task.Priority,
            EstimatedHours = task.EstimatedHours,
            ActualHours = task.ActualHours,
            StartDate = task.StartDate,
            DueDate = task.DueDate,
            CompletedDate = task.CompletedDate,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            // Rich-task collections — only populated when the caller explicitly Include'd them.
            Subtasks = task.Subtasks?.Select(s => s.ToDto()).ToList() ?? new(),
            Comments = task.Comments?.Where(c => c.ParentCommentId == null).Select(c => c.ToDto()).ToList() ?? new(),
            Attachments = task.Attachments?.Select(a => a.ToDto()).ToList() ?? new(),
            Tags = task.TaskTags?.Select(tt => tt.Tag.ToDto()).ToList() ?? new(),
            WatcherCount = task.Watchers?.Count ?? 0
        };

        public static TaskHistoryDto ToDto(this TaskHistory history) => new()
        {
            Id = history.Id,
            FieldName = history.FieldName,
            OldValue = history.OldValue,
            NewValue = history.NewValue,
            Comment = history.Comment,
            ChangedAt = history.ChangedAt,
            ChangedBy = history.ChangedBy.ToDto()
        };

        public static SubtaskDto ToDto(this Subtask subtask) => new()
        {
            Id = subtask.Id,
            TaskId = subtask.TaskId,
            Title = subtask.Title,
            IsCompleted = subtask.IsCompleted,
            SortOrder = subtask.SortOrder,
            AssignedToId = subtask.AssignedToId,
            AssignedTo = subtask.AssignedTo?.ToDto(),
            DueDate = subtask.DueDate,
            CreatedAt = subtask.CreatedAt,
            CompletedAt = subtask.CompletedAt
        };

        public static CommentDto ToDto(this Comment comment) => new()
        {
            Id = comment.Id,
            TaskId = comment.TaskId,
            ParentCommentId = comment.ParentCommentId,
            Author = comment.Author.ToDto(),
            Body = comment.Body,
            IsEdited = comment.IsEdited,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.UpdatedAt,
            Replies = comment.Replies?.Select(r => r.ToDto()).ToList() ?? new()
        };

        public static AttachmentDto ToDto(this Attachment attachment) => new()
        {
            Id = attachment.Id,
            TaskId = attachment.TaskId,
            UploadedBy = attachment.UploadedBy.ToDto(),
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            FileSize = attachment.FileSize,
            UploadedAt = attachment.UploadedAt
        };

        public static TagDto ToDto(this Tag tag) => new()
        {
            Id = tag.Id,
            Name = tag.Name,
            Color = tag.Color,
            OrganizationId = tag.OrganizationId,
            CreatedAt = tag.CreatedAt
        };
    }

    /// <summary>
    /// Domain-level helpers for the "completed" concept, which currently spans three status values.
    /// Centralized here so the magic strings aren't repeated across controllers, queries and UI.
    /// </summary>
    public static class TaskItemExtensions
    {
        private static readonly HashSet<string> CompletedStatuses =
            new(StringComparer.OrdinalIgnoreCase) { "Completed", "Tested", "Closed" };

        public static bool IsCompleted(this TaskItem task) => CompletedStatuses.Contains(task.Status);

        public static bool IsCompletedStatus(string? status) =>
            !string.IsNullOrEmpty(status) && CompletedStatuses.Contains(status);
    }
}
