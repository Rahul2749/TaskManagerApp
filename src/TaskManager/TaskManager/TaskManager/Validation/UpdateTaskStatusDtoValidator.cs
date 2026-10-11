using FluentValidation;
using TaskManager.Shared.DTOs;

namespace TaskManager.Validation;

public class UpdateTaskStatusDtoValidator : AbstractValidator<UpdateTaskStatusDto>
{
    public UpdateTaskStatusDtoValidator()
    {
        RuleFor(x => x.Status).NotEmpty()
            .Must(s => s is "NotAssigned" or "Assigned" or "InProgress" or "Completed" or "Tested" or "Closed")
            .WithMessage("Invalid status");
        RuleFor(x => x.ActualHours).GreaterThanOrEqualTo(0).LessThanOrEqualTo(10000);
    }
}
