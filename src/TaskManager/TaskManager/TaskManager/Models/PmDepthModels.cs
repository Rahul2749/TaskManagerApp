using System.ComponentModel.DataAnnotations;

namespace TaskManager.Models;

/// <summary>Named filter snapshot for a user's task (or project) list.</summary>
public class SavedView
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public int UserId { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>"task" or "project".</summary>
    [Required, MaxLength(20)]
    public string EntityType { get; set; } = "task";

    /// <summary>JSON filter payload, e.g. {"projectId":1,"status":"InProgress","assignedToId":null}.</summary>
    [Required]
    public string FiltersJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Organization Organization { get; set; } = null!;
    public User User { get; set; } = null!;
}

/// <summary>Org- or project-scoped custom field definition (Professional+).</summary>
public class CustomFieldDefinition
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public int? ProjectId { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>text | number | date | select</summary>
    [Required, MaxLength(20)]
    public string FieldType { get; set; } = "text";

    /// <summary>JSON string array for select options.</summary>
    public string? OptionsJson { get; set; }

    public int SortOrder { get; set; }
    public bool IsRequired { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Organization Organization { get; set; } = null!;
    public Project? Project { get; set; }
    public ICollection<CustomFieldValue> Values { get; set; } = new List<CustomFieldValue>();
}

public class CustomFieldValue
{
    public int Id { get; set; }
    public int DefinitionId { get; set; }
    public int TaskId { get; set; }

    [MaxLength(2000)]
    public string? Value { get; set; }

    public CustomFieldDefinition Definition { get; set; } = null!;
    public TaskItem Task { get; set; } = null!;
}

/// <summary>Reusable task blueprint for creating tasks quickly.</summary>
public class TaskTemplate
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required, MaxLength(20)]
    public string Priority { get; set; } = "Medium";

    /// <summary>JSON array of subtask titles.</summary>
    public string? SubtasksJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Organization Organization { get; set; } = null!;
}

/// <summary>Reusable project blueprint with starter task titles.</summary>
public class ProjectTemplate
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>JSON array of { title, priority } task stubs.</summary>
    public string? TasksJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Organization Organization { get; set; } = null!;
}
