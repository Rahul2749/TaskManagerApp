using FluentValidation;
using TaskManager.Shared.DTOs;

namespace TaskManager.Validation
{
    /// <summary>
    /// FluentValidation rules for <see cref="LoginDto"/>.
    /// Runs in addition to the DataAnnotations already on the DTO.
    /// </summary>
    public class LoginDtoValidator : AbstractValidator<LoginDto>
    {
        public LoginDtoValidator()
        {
            RuleFor(x => x.Username)
                .NotEmpty().WithMessage("Username is required")
                .MaximumLength(100);

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required");
        }
    }
}
