using AiTicketing.Domain.Enums;

namespace AiTicketing.Application.Tickets;

public sealed class GetTicketsQuery
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public string? Search { get; init; }

    public TicketStatus? Status { get; init; }

    public TicketPriority? Priority { get; init; }

    public string? AssignedToUserId { get; init; }

    public bool? Unassigned { get; init; }

    public TicketCategory? Category { get; init; }

    public string? CustomerEmail { get; init; }

    public string? SortBy { get; init; }

    public string? SortDirection { get; init; }
}
