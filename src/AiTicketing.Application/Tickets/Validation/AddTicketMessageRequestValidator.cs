using FluentValidation;

namespace AiTicketing.Application.Tickets.Validation;

public sealed class AddTicketMessageRequestValidator : AbstractValidator<AddTicketMessageRequest>
{
    public AddTicketMessageRequestValidator()
    {
        RuleFor(request => request.Message)
            .NotEmpty()
            .MaximumLength(4000);

        RuleFor(request => request.CreatedByDisplayName)
            .MaximumLength(150);
    }
}
