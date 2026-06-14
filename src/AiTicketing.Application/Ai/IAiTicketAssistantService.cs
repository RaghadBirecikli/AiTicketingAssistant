namespace AiTicketing.Application.Ai;

public interface IAiTicketAssistantService
{
    Task<TicketAssistantResult> AnalyzeAsync(TicketAssistantRequest request, CancellationToken cancellationToken = default);

    Task<string> SuggestReplyAsync(
        TicketReplySuggestionRequest request,
        CancellationToken cancellationToken = default);

    Task<string> SummarizeTicketAsync(
        TicketSummaryRequest request,
        CancellationToken cancellationToken = default);

    Task<TicketTriageSuggestion> SuggestTriageAsync(
        TicketTriageRequest request,
        CancellationToken cancellationToken = default);
}
