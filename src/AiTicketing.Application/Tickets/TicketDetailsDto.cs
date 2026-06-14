using AiTicketing.Domain.Enums;

namespace AiTicketing.Application.Tickets;

public sealed record TicketDetailsDto(
    Guid Id,
    string Title,
    string Description,
    TicketStatus Status,
    TicketPriority Priority,
    TicketCategory Category,
    TicketSource Source,
    string? CustomerEmail,
    string? CustomerName,
    string? CustomerUserId,
    string? AssignedToUserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    DateTimeOffset? ResolvedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    IReadOnlyList<TicketDetailsMessageDto> Messages);
