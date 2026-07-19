using FluentValidation;
using TaskManager.Models;
using TaskManager.Shared.DTOs;

namespace TaskManager.Validation
{
    /// <summary>
    /// FluentValidation rules for creating/updating a user via <see cref="RegisterDto"/>.
    /// Adds password-strength and allowed-role constraints the DataAnnotations can't express.
    /// </summary>
    public class RegisterDtoValidator : AbstractValidator<RegisterDto>
    {
        public RegisterDtoValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().MaximumLength(100);

            RuleFor(x => x.LastName)
                .NotEmpty().MaximumLength(100);

            RuleFor(x => x.Username)
                .NotEmpty().WithMessage("Username is required")
                .MaximumLength(100)
                .Matches("^[a-zA-Z0-9_.-]+$")
                .WithMessage("Username may only contain letters, numbers, dots, hyphens and underscores");

            RuleFor(x => x.Email)
                .NotEmpty().EmailAddress()
                .MaximumLength(255);

            // Password rules only apply when a password is supplied (update flow allows empty).
            RuleFor(x => x.Password)
                .MinimumLength(8).WithMessage("Password must be at least 8 characters")
                .MaximumLength(100)
                .Must(HaveStrength).WithMessage(
                    "Password must contain at least one uppercase letter, one lowercase letter and one digit")
                .When(x => !string.IsNullOrWhiteSpace(x.Password));

            RuleFor(x => x.Role)
                .NotEmpty()
                .Must(r => r is Roles.User or Roles.Manager)
                .WithMessage("Invalid role");
        }

        /// <summary>Minimal password-strength gate: upper + lower + digit.</summary>
        private static bool HaveStrength(string password) =>
            password.Any(char.IsUpper) &&
            password.Any(char.IsLower) &&
            password.Any(char.IsDigit);
    }
}
