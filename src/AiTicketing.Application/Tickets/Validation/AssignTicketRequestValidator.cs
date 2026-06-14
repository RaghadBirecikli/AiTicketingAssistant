using FluentValidation;

namespace AiTicketing.Application.Tickets.Validation;

public sealed class AssignTicketRequestValidator : AbstractValidator<AssignTicketRequest>
{
    public AssignTicketRequestValidator()
    {
        RuleFor(request => request.AssignedToUserId)
            .NotEmpty();
    }
}
