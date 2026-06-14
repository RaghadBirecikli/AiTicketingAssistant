using AiTicketing.Application.Ai;

namespace AiTicketing.Tests.Fakes;

public sealed class FakeAiTicketAssistantService : IAiTicketAssistantService
{
    public Task<TicketAssistantResult> AnalyzeAsync(TicketAssistantRequest request, CancellationToken cancellationToken = default)
    {
        var result = new TicketAssistantResult(
            Summary: request.Title,
            SuggestedCategory: "Test Category",
            SuggestedPriority: "Low",
            SuggestedReply: "This is a fake AI assistant response for tests.",
            Tags: ["test"],
            Provider: "Fake");

        return Task.FromResult(result);
    }

    public Task<string> SuggestReplyAsync(
        TicketReplySuggestionRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult("This is a fake suggested customer reply.");

    public Task<string> SummarizeTicketAsync(
        TicketSummaryRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult("This is a fake ticket summary for tests.");

    public Task<TicketTriageSuggestion> SuggestTriageAsync(
        TicketTriageRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new TicketTriageSuggestion(
            request.CurrentPriority,
            request.CurrentCategory,
            false,
            null,
            "This is a fake triage suggestion for tests."));
}
