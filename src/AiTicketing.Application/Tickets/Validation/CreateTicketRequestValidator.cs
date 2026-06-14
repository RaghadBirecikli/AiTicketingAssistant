using FluentValidation;

namespace AiTicketing.Application.Tickets.Validation;

public sealed class CreateTicketRequestValidator : AbstractValidator<CreateTicketRequest>
{
    public CreateTicketRequestValidator()
    {
        RuleFor(request => request.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Description)
            .NotEmpty()
            .MaximumLength(4000);

        RuleFor(request => request.CustomerEmail)
            .MaximumLength(256)
            .EmailAddress()
            .When(request => !string.IsNullOrWhiteSpace(request.CustomerEmail));

        RuleFor(request => request.CustomerName)
            .MaximumLength(150);

        RuleFor(request => request.Source)
            .IsInEnum();
    }
}
