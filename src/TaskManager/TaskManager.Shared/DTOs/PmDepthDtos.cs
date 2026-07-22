using System.ComponentModel.DataAnnotations;

namespace TaskManager.Shared.DTOs;

public sealed class SavedViewDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string EntityType { get; set; } = "task";
    public string FiltersJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}

public sealed class CreateSavedViewDto
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string EntityType { get; set; } = "task";

    [Required]
    public string FiltersJson { get; set; } = "{}";
}

public sealed class CustomFieldDefinitionDto
{
    public int Id { get; set; }
    public int? ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FieldType { get; set; } = "text";
    public string? OptionsJson { get; set; }
    public int SortOrder { get; set; }
    public bool IsRequired { get; set; }
}

public sealed class UpsertCustomFieldDefinitionDto
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string FieldType { get; set; } = "text";

    public int? ProjectId { get; set; }
    public string? OptionsJson { get; set; }
    public int SortOrder { get; set; }
    public bool IsRequired { get; set; }
}

public sealed class CustomFieldValueDto
{
    public int DefinitionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FieldType { get; set; } = "text";
    public string? OptionsJson { get; set; }
    public bool IsRequired { get; set; }
    public string? Value { get; set; }
}

public sealed class SetCustomFieldValuesDto
{
    public List<CustomFieldValueInput> Values { get; set; } = [];
}

public sealed class CustomFieldValueInput
{
    public int DefinitionId { get; set; }
    public string? Value { get; set; }
}

public sealed class TaskTemplateDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Priority { get; set; } = "Medium";
    public string? SubtasksJson { get; set; }
}

public sealed class UpsertTaskTemplateDto
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required, MaxLength(20)]
    public string Priority { get; set; } = "Medium";

    public string? SubtasksJson { get; set; }
}

public sealed class ApplyTaskTemplateDto
{
    [Required]
    public int ProjectId { get; set; }

    public int? AssignedToId { get; set; }
}

public sealed class ProjectTemplateDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? TasksJson { get; set; }
}

public sealed class UpsertProjectTemplateDto
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? TasksJson { get; set; }
}

public sealed class ApplyProjectTemplateDto
{
    [Required, MaxLength(200)]
    public string ProjectName { get; set; } = string.Empty;

    public string? Description { get; set; }
}
