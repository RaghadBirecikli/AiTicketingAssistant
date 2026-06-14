using AiTicketing.Application.Common.Models;

namespace AiTicketing.Application.Tickets;

public interface ITicketService
{
    Task<CreateTicketResponse> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<TicketDto>> GetListAsync(GetTicketsQuery query, CancellationToken cancellationToken = default);

    Task<TicketStatsResponse> GetStatsAsync(CancellationToken cancellationToken = default);

    Task<MyTicketStatsResponse> GetMyStatsAsync(CancellationToken cancellationToken = default);

    Task<TicketDetailsDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AddTicketMessageResponse?> AddMessageAsync(
        Guid ticketId,
        AddTicketMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<AddInternalNoteResponse?> AddInternalNoteAsync(
        Guid ticketId,
        AddInternalNoteRequest request,
        CancellationToken cancellationToken = default);

    Task<SuggestedReplyResponse?> SuggestReplyAsync(
        Guid ticketId,
        SuggestReplyRequest request,
        CancellationToken cancellationToken = default);

    Task<TicketSummaryResponse?> SummarizeAsync(
        Guid ticketId,
        SummarizeTicketRequest request,
        CancellationToken cancellationToken = default);

    Task<TicketTriageSuggestionResponse?> SuggestTriageAsync(
        Guid ticketId,
        SuggestTriageRequest request,
        CancellationToken cancellationToken = default);

    Task<ChangeTicketStatusResponse?> ChangeStatusAsync(
        Guid ticketId,
        ChangeTicketStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<AssignTicketResponse?> AssignAsync(
        Guid ticketId,
        AssignTicketRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLogDto>?> GetAuditLogsAsync(Guid ticketId, CancellationToken cancellationToken = default);

    Task<DeleteTicketResponse?> DeleteAsync(
        Guid ticketId,
        DeleteTicketRequest request,
        CancellationToken cancellationToken = default);
}
