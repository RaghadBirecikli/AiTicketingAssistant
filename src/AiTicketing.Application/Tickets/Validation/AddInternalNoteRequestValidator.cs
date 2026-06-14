using FluentValidation;

namespace AiTicketing.Application.Tickets.Validation;

public sealed class AddInternalNoteRequestValidator : AbstractValidator<AddInternalNoteRequest>
{
    public AddInternalNoteRequestValidator()
    {
        RuleFor(request => request.Body)
            .NotEmpty()
            .MaximumLength(4000);
    }
}
