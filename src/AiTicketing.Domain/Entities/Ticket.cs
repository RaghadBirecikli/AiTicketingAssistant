using AiTicketing.Domain.Enums;

namespace AiTicketing.Domain.Entities;

public sealed class Ticket
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public TicketStatus Status { get; set; } = TicketStatus.Open;

    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    public TicketCategory Category { get; set; } = TicketCategory.General;

    public TicketSource Source { get; set; } = TicketSource.Web;

    public string? CustomerEmail { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerUserId { get; set; }

    public string? AssignedToUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public bool IsDeleted { get; set; }

    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
}
