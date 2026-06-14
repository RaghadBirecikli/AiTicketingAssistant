using FluentValidation;

namespace AiTicketing.Application.Tickets.Validation;

public sealed class DeleteTicketRequestValidator : AbstractValidator<DeleteTicketRequest>
{
    public DeleteTicketRequestValidator()
    {
        RuleFor(request => request.DeletedByUserId)
            .MaximumLength(100);

        RuleFor(request => request.DeletedByDisplayName)
            .MaximumLength(150);

        RuleFor(request => request.Reason)
            .MaximumLength(500);
    }
}
