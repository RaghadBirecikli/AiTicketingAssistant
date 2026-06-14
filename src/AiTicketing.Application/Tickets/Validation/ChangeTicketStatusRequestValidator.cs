using FluentValidation;

namespace AiTicketing.Application.Tickets.Validation;

public sealed class ChangeTicketStatusRequestValidator : AbstractValidator<ChangeTicketStatusRequest>
{
    public ChangeTicketStatusRequestValidator()
    {
        RuleFor(request => request.Status)
            .IsInEnum();

        RuleFor(request => request.ChangedByDisplayName)
            .MaximumLength(150);
    }
}
