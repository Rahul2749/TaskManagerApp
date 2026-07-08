using FluentValidation;
using TaskManager.Shared.DTOs;

namespace TaskManager.Validation
{
    /// <summary>
    /// FluentValidation rules for create/update task payloads.
    /// </summary>
    public class TaskDtoValidator : AbstractValidator<TaskDto>
    {
        public TaskDtoValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Title is required")
                .MaximumLength(300);

            RuleFor(x => x.ProjectId)
                .GreaterThan(0).WithMessage("A valid project is required");

            RuleFor(x => x.Status)
                .NotEmpty()
                .Must(BeValidStatus).WithMessage("Invalid status");

            RuleFor(x => x.Priority)
                .NotEmpty()
                .Must(p => p is "Low" or "Medium" or "High" or "Critical")
                .WithMessage("Priority must be Low, Medium, High or Critical");

            RuleFor(x => x.EstimatedHours)
                .GreaterThanOrEqualTo(0).LessThanOrEqualTo(10000);

            RuleFor(x => x.DueDate)
                .Must((dto, due) => !dto.StartDate.HasValue || !due.HasValue || due >= dto.StartDate)
                .WithMessage("Due date cannot be before the start date");
        }

        private static bool BeValidStatus(string? status) =>
            status is "NotAssigned" or "Assigned" or "InProgress" or "Completed" or "Tested" or "Closed";
    }
}
