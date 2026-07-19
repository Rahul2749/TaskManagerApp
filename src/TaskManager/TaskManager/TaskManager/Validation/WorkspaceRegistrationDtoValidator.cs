using FluentValidation;
using TaskManager.Shared.DTOs;

namespace TaskManager.Validation;

public sealed class WorkspaceRegistrationDtoValidator : AbstractValidator<WorkspaceRegistrationDto>
{
    public WorkspaceRegistrationDtoValidator()
    {
        RuleFor(x => x.OrganizationName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Username)
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[a-zA-Z0-9_.-]+$")
            .WithMessage("Username may only contain letters, numbers, dots, hyphens and underscores");

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(100)
            .Must(password =>
                password.Any(char.IsUpper) &&
                password.Any(char.IsLower) &&
                password.Any(char.IsDigit))
            .WithMessage("Password must contain an uppercase letter, a lowercase letter, and a digit");
    }
}
