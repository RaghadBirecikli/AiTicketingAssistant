namespace AiTicketing.Domain.Entities;

public sealed class TicketMessage
{
    public Guid Id { get; set; }

    public Guid TicketId { get; set; }

    public Ticket Ticket { get; set; } = null!;

    public string Message { get; set; } = string.Empty;

    public bool IsInternalNote { get; set; }

    public string? CreatedByUserId { get; set; }

    public string? CreatedByDisplayName { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
