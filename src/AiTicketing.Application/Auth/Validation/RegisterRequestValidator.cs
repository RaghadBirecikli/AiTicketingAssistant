using FluentValidation;

namespace AiTicketing.Application.Auth.Validation;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.FullName)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(request => request.Email)
            .NotEmpty()
            .MaximumLength(256)
            .EmailAddress();

        RuleFor(request => request.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(100);

        RuleFor(request => request.Role)
            .NotEmpty()
            .Must(AuthRoles.All.Contains)
            .WithMessage("Role must be one of: Admin, Agent, Customer.");
    }
}
