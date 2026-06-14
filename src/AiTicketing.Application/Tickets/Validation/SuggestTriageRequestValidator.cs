using FluentValidation;

namespace AiTicketing.Application.Tickets.Validation;

public sealed class SuggestTriageRequestValidator : AbstractValidator<SuggestTriageRequest>
{
    public SuggestTriageRequestValidator()
    {
        RuleFor(request => request.Instruction)
            .MaximumLength(500)
            .When(request => !string.IsNullOrWhiteSpace(request.Instruction));
    }
}
