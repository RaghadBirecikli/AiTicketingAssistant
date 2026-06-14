using FluentValidation;

namespace AiTicketing.Application.Tickets.Validation;

public sealed class SuggestReplyRequestValidator : AbstractValidator<SuggestReplyRequest>
{
    public SuggestReplyRequestValidator()
    {
        RuleFor(request => request.Instruction)
            .MaximumLength(500)
            .When(request => !string.IsNullOrWhiteSpace(request.Instruction));
    }
}
