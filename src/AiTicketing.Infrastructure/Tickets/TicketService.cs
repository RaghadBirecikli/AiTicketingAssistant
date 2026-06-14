using System.Text.Json;
using AiTicketing.Application.Ai;
using AiTicketing.Application.Auth;
using AiTicketing.Application.Common.Interfaces;
using AiTicketing.Application.Common.Models;
using AiTicketing.Application.Notifications;
using AiTicketing.Application.Tickets;
using AiTicketing.Application.Users;
using AiTicketing.Domain.Entities;
using AiTicketing.Domain.Enums;
using AiTicketing.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace AiTicketing.Infrastructure.Tickets;

public sealed class TicketService(
    ApplicationDbContext dbContext,
    IAiTicketAssistantService aiTicketAssistantService,
    IValidator<CreateTicketRequest> createTicketValidator,
    IValidator<AddTicketMessageRequest> addTicketMessageValidator,
    IValidator<AddInternalNoteRequest> addInternalNoteValidator,
    IValidator<SuggestReplyRequest> suggestReplyValidator,
    IValidator<SuggestTriageRequest> suggestTriageValidator,
    IValidator<ChangeTicketStatusRequest> changeTicketStatusValidator,
    IValidator<AssignTicketRequest> assignTicketValidator,
    IValidator<DeleteTicketRequest> deleteTicketValidator,
    ICurrentUserService currentUserService,
    INotificationService notificationService,
    IUserLookupService userLookupService) : ITicketService
{
    private const int MaxPageSize = 100;
    private const int MaxSearchLength = 100;

    public async Task<CreateTicketResponse> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken = default)
    {
        await createTicketValidator.ValidateAndThrowAsync(request, cancellationToken);

        var aiResult = await aiTicketAssistantService.AnalyzeAsync(
            new TicketAssistantRequest(
                request.Title,
                request.Description,
                request.CustomerEmail),
            cancellationToken);

        var customerEmail = NormalizeOptional(request.CustomerEmail);
        var customerName = NormalizeOptional(request.CustomerName);
        string? customerUserId = null;

        if (currentUserService.IsAuthenticated &&
            string.Equals(currentUserService.Role, AuthRoles.Customer, StringComparison.OrdinalIgnoreCase))
        {
            customerUserId = NormalizeOptional(currentUserService.UserId);
            customerEmail ??= NormalizeOptional(currentUserService.Email);
            customerName ??= NormalizeOptional(currentUserService.FullName);
        }

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Status = TicketStatus.Open,
            Priority = ParseEnum(aiResult.SuggestedPriority, TicketPriority.Medium),
            Category = ParseEnum(aiResult.SuggestedCategory, TicketCategory.General),
            Source = request.Source,
            CustomerEmail = customerEmail,
            CustomerName = customerName,
            CustomerUserId = customerUserId,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Tickets.Add(ticket);
        dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(Ticket),
            EntityId = ticket.Id,
            Action = "TicketCreated",
            NewValues = $"Ticket '{ticket.Title}' created.",
            PerformedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateTicketResponse(
            MapToDto(ticket),
            aiResult.Summary,
            aiResult.SuggestedReply);
    }

    public async Task<PagedResult<TicketDto>> GetListAsync(GetTicketsQuery query, CancellationToken cancellationToken = default)
    {
        ValidatePagination(query.Page, query.PageSize);
        var assignedToUserId = ValidateAndNormalizeAssignedToUserIdFilter(query.AssignedToUserId);
        var search = ValidateAndNormalizeSearchFilter(query.Search);
        var sorting = ValidateAndNormalizeSorting(query.SortBy, query.SortDirection);
        ValidateUnassignedFilter(query.Unassigned, assignedToUserId);

        var ticketsQuery = dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => !ticket.IsDeleted);

        ticketsQuery = ApplyReadAccessFilter(ticketsQuery);

        if (query.Status is not null)
        {
            ticketsQuery = ticketsQuery.Where(ticket => ticket.Status == query.Status);
        }

        if (query.Priority is not null)
        {
            ticketsQuery = ticketsQuery.Where(ticket => ticket.Priority == query.Priority);
        }

        if (assignedToUserId is not null)
        {
            ticketsQuery = ticketsQuery.Where(ticket => ticket.AssignedToUserId == assignedToUserId);
        }

        if (query.Unassigned is true)
        {
            ticketsQuery = ticketsQuery.Where(ticket =>
                ticket.AssignedToUserId == null || ticket.AssignedToUserId == string.Empty);
        }

        if (query.Category is not null)
        {
            ticketsQuery = ticketsQuery.Where(ticket => ticket.Category == query.Category);
        }

        if (!string.IsNullOrWhiteSpace(query.CustomerEmail))
        {
            var customerEmail = query.CustomerEmail.Trim().ToLower();
            ticketsQuery = ticketsQuery.Where(ticket =>
                ticket.CustomerEmail != null && ticket.CustomerEmail.ToLower() == customerEmail);
        }

        if (search is not null)
        {
            ticketsQuery = ticketsQuery.Where(ticket =>
                ticket.Title.ToLower().Contains(search) ||
                ticket.Description.ToLower().Contains(search));
        }

        var totalCount = await ticketsQuery.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)query.PageSize);

        var tickets = query.Page > totalPages
            ? []
            : await ApplySorting(ticketsQuery, sorting.SortBy, sorting.SortDirection)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);

        return new PagedResult<TicketDto>(
            tickets.Select(MapToDto).ToArray(),
            query.Page,
            query.PageSize,
            totalCount,
            totalPages,
            query.Page > 1,
            totalPages > query.Page);
    }

    public async Task<TicketDetailsDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ticket = await dbContext.Tickets
            .AsNoTracking()
            .Include(ticket => ticket.Messages)
            .Where(ticket => !ticket.IsDeleted)
            .SingleOrDefaultAsync(ticket => ticket.Id == id, cancellationToken);

        return ticket is null || !CanRead(ticket) ? null : MapToDetailsDto(ticket);
    }

    public async Task<TicketStatsResponse> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var stats = await dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => !ticket.IsDeleted)
            .GroupBy(_ => 1)
            .Select(group => new TicketStatsResponse(
                group.Count(),
                group.Count(ticket => ticket.Status == TicketStatus.Open),
                group.Count(ticket => ticket.Status == TicketStatus.InProgress),
                group.Count(ticket => ticket.Status == TicketStatus.Resolved),
                group.Count(ticket => ticket.Status == TicketStatus.Closed),
                group.Count(ticket => ticket.AssignedToUserId == null || ticket.AssignedToUserId == string.Empty),
                group.Count(ticket => ticket.Priority == TicketPriority.Low),
                group.Count(ticket => ticket.Priority == TicketPriority.Medium),
                group.Count(ticket => ticket.Priority == TicketPriority.High),
                group.Count(ticket => ticket.Priority == TicketPriority.Urgent)))
            .SingleOrDefaultAsync(cancellationToken);

        return stats ?? new TicketStatsResponse(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    public async Task<MyTicketStatsResponse> GetMyStatsAsync(CancellationToken cancellationToken = default)
    {
        var ticketsQuery = dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => !ticket.IsDeleted);

        if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
        {
            return EmptyMyTicketStats();
        }

        if (IsInRole(AuthRoles.Agent))
        {
            ticketsQuery = ticketsQuery.Where(ticket => ticket.AssignedToUserId == currentUserService.UserId);
        }
        else if (IsInRole(AuthRoles.Customer))
        {
            ticketsQuery = ticketsQuery.Where(ticket => ticket.CustomerUserId == currentUserService.UserId);
        }
        else
        {
            return EmptyMyTicketStats();
        }

        var stats = await ticketsQuery
            .GroupBy(_ => 1)
            .Select(group => new MyTicketStatsResponse(
                group.Count(),
                group.Count(ticket => ticket.Status == TicketStatus.Open),
                group.Count(ticket => ticket.Status == TicketStatus.InProgress),
                group.Count(ticket => ticket.Status == TicketStatus.Resolved),
                group.Count(ticket => ticket.Status == TicketStatus.Closed),
                group.Count(ticket => ticket.Priority == TicketPriority.Low),
                group.Count(ticket => ticket.Priority == TicketPriority.Medium),
                group.Count(ticket => ticket.Priority == TicketPriority.High),
                group.Count(ticket => ticket.Priority == TicketPriority.Urgent)))
            .SingleOrDefaultAsync(cancellationToken);

        return stats ?? EmptyMyTicketStats();
    }

    public async Task<AddTicketMessageResponse?> AddMessageAsync(
        Guid ticketId,
        AddTicketMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        await addTicketMessageValidator.ValidateAndThrowAsync(request, cancellationToken);

        var ticket = await dbContext.Tickets
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId && !ticket.IsDeleted, cancellationToken);

        if (ticket is null)
        {
            return null;
        }

        if (!CanAddMessage(ticket))
        {
            return null;
        }

        if (IsInRole(AuthRoles.Customer) && request.IsInternalNote)
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(request.IsInternalNote), "Customers cannot add internal notes.")
            ]);
        }

        var now = DateTime.UtcNow;
        var createdByUserId = currentUserService.IsAuthenticated
            ? currentUserService.UserId
            : NormalizeOptional(request.CreatedByUserId);
        var createdByDisplayName = currentUserService.IsAuthenticated
            ? NormalizeOptional(currentUserService.FullName) ?? NormalizeOptional(currentUserService.Email)
            : NormalizeOptional(request.CreatedByDisplayName);
        var message = new TicketMessage
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            Message = request.Message.Trim(),
            IsInternalNote = request.IsInternalNote,
            CreatedByUserId = createdByUserId,
            CreatedByDisplayName = createdByDisplayName,
            CreatedAtUtc = now
        };

        ticket.UpdatedAtUtc = now;

        dbContext.TicketMessages.Add(message);
        dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(TicketMessage),
            EntityId = message.Id,
            Action = request.IsInternalNote ? "InternalNoteAdded" : "MessageAdded",
            PerformedByUserId = createdByUserId,
            PerformedByDisplayName = createdByDisplayName,
            PerformedAtUtc = now
        });

        var notificationRecipientUserId = request.IsInternalNote
            ? null
            : ResolveMessageNotificationRecipient(ticket);
        if (notificationRecipientUserId is not null)
        {
            await notificationService.CreateAsync(new CreateNotificationRequest(
                notificationRecipientUserId,
                "New ticket message",
                $"A new message was added to ticket '{ticket.Title}'.",
                "TicketMessageCreated",
                ticket.Id), cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AddTicketMessageResponse(MapToMessageDto(message));
    }

    public async Task<AddInternalNoteResponse?> AddInternalNoteAsync(
        Guid ticketId,
        AddInternalNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        await addInternalNoteValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (!IsInRole(AuthRoles.Admin) && !IsInRole(AuthRoles.Agent))
        {
            return null;
        }

        var response = await AddMessageAsync(
            ticketId,
            new AddTicketMessageRequest(request.Body, true, null, null),
            cancellationToken);

        if (response is null)
        {
            return null;
        }

        return new AddInternalNoteResponse(new TicketDetailsMessageDto(
            response.Message.Id,
            response.Message.TicketId,
            response.Message.CreatedByUserId,
            currentUserService.Role ?? AuthRoles.Agent,
            response.Message.CreatedByDisplayName,
            response.Message.Message,
            response.Message.IsInternalNote,
            response.Message.CreatedAtUtc));
    }

    public async Task<SuggestedReplyResponse?> SuggestReplyAsync(
        Guid ticketId,
        SuggestReplyRequest request,
        CancellationToken cancellationToken = default)
    {
        var instruction = NormalizeOptional(request.Instruction);
        await suggestReplyValidator.ValidateAndThrowAsync(new SuggestReplyRequest(instruction), cancellationToken);

        var ticket = await dbContext.Tickets
            .AsNoTracking()
            .Include(ticket => ticket.Messages)
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId && !ticket.IsDeleted, cancellationToken);

        if (ticket is null || !CanChangeStatus(ticket))
        {
            return null;
        }

        var messages = ticket.Messages
            .Where(message => !message.IsInternalNote)
            .OrderBy(message => message.CreatedAtUtc)
            .Select(message => new TicketReplySuggestionMessage(
                ResolveMessageSenderRole(ticket, message.CreatedByUserId),
                message.Message))
            .ToArray();

        var suggestedReply = await aiTicketAssistantService.SuggestReplyAsync(
            new TicketReplySuggestionRequest(
                ticket.Title,
                ticket.Description,
                ticket.Status.ToString(),
                ticket.Priority.ToString(),
                messages,
                instruction),
            cancellationToken);

        return new SuggestedReplyResponse(suggestedReply);
    }

    public async Task<TicketSummaryResponse?> SummarizeAsync(
        Guid ticketId,
        SummarizeTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        var ticket = await dbContext.Tickets
            .AsNoTracking()
            .Include(ticket => ticket.Messages)
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId && !ticket.IsDeleted, cancellationToken);

        if (ticket is null || !CanChangeStatus(ticket))
        {
            return null;
        }

        if (request.IncludeInternalNotes && !IsInRole(AuthRoles.Admin))
        {
            return null;
        }

        var messages = ticket.Messages
            .Where(message => request.IncludeInternalNotes || !message.IsInternalNote)
            .OrderBy(message => message.CreatedAtUtc)
            .Select(message => new TicketSummaryMessage(
                ResolveMessageSenderRole(ticket, message.CreatedByUserId),
                message.CreatedByDisplayName,
                message.Message,
                ToUtcOffset(message.CreatedAtUtc),
                message.IsInternalNote))
            .ToArray();

        var summary = await aiTicketAssistantService.SummarizeTicketAsync(
            new TicketSummaryRequest(
                ticket.Title,
                ticket.Description,
                ticket.Status.ToString(),
                ticket.Priority.ToString(),
                ToUtcOffset(ticket.CreatedAtUtc),
                ResolveAssignedAgentDisplayName(ticket),
                request.IncludeInternalNotes,
                messages),
            cancellationToken);

        return new TicketSummaryResponse(summary);
    }

    public async Task<TicketTriageSuggestionResponse?> SuggestTriageAsync(
        Guid ticketId,
        SuggestTriageRequest request,
        CancellationToken cancellationToken = default)
    {
        var instruction = NormalizeOptional(request.Instruction);
        await suggestTriageValidator.ValidateAndThrowAsync(new SuggestTriageRequest(instruction), cancellationToken);

        var ticket = await dbContext.Tickets
            .AsNoTracking()
            .Include(ticket => ticket.Messages)
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId && !ticket.IsDeleted, cancellationToken);

        if (ticket is null || !CanChangeStatus(ticket))
        {
            return null;
        }

        var messages = ticket.Messages
            .Where(message => !message.IsInternalNote)
            .OrderBy(message => message.CreatedAtUtc)
            .Select(message => new TicketTriageMessage(
                ResolveMessageSenderRole(ticket, message.CreatedByUserId),
                message.CreatedByDisplayName,
                message.Message,
                ToUtcOffset(message.CreatedAtUtc)))
            .ToArray();

        var suggestion = await aiTicketAssistantService.SuggestTriageAsync(
            new TicketTriageRequest(
                ticket.Title,
                ticket.Description,
                ticket.Status,
                ticket.Priority,
                ticket.Category,
                ToUtcOffset(ticket.CreatedAtUtc),
                NormalizeOptional(ticket.AssignedToUserId),
                ResolveAssignedAgentDisplayName(ticket),
                messages,
                instruction),
            cancellationToken);

        return new TicketTriageSuggestionResponse(
            ticket.Priority,
            suggestion.SuggestedPriority,
            suggestion.SuggestedCategory,
            suggestion.EscalationRecommended,
            suggestion.EscalationReason,
            suggestion.Rationale);
    }

    public async Task<ChangeTicketStatusResponse?> ChangeStatusAsync(
        Guid ticketId,
        ChangeTicketStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        await changeTicketStatusValidator.ValidateAndThrowAsync(request, cancellationToken);

        var ticket = await dbContext.Tickets
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId && !ticket.IsDeleted, cancellationToken);

        if (ticket is null)
        {
            return null;
        }

        if (!CanChangeStatus(ticket))
        {
            return null;
        }

        if (ticket.Status == request.Status)
        {
            return new ChangeTicketStatusResponse(MapToDto(ticket));
        }

        var oldStatus = ticket.Status;
        var now = DateTime.UtcNow;
        var performedByUserId = currentUserService.IsAuthenticated
            ? currentUserService.UserId
            : NormalizeOptional(request.ChangedByUserId);
        var performedByDisplayName = currentUserService.IsAuthenticated
            ? NormalizeOptional(currentUserService.FullName) ?? NormalizeOptional(currentUserService.Email)
            : NormalizeOptional(request.ChangedByDisplayName);

        ticket.Status = request.Status;
        ticket.UpdatedAtUtc = now;

        if (request.Status == TicketStatus.Resolved && ticket.ResolvedAtUtc is null)
        {
            ticket.ResolvedAtUtc = now;
        }

        if (request.Status == TicketStatus.Closed && ticket.ClosedAtUtc is null)
        {
            ticket.ClosedAtUtc = now;
        }

        dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(Ticket),
            EntityId = ticket.Id,
            Action = "StatusChanged",
            OldValues = SerializeStatus(oldStatus),
            NewValues = SerializeStatus(request.Status),
            PerformedByUserId = performedByUserId,
            PerformedByDisplayName = performedByDisplayName,
            PerformedAtUtc = now
        });

        if (!string.IsNullOrWhiteSpace(ticket.CustomerUserId))
        {
            await notificationService.CreateAsync(new CreateNotificationRequest(
                ticket.CustomerUserId,
                "Ticket status updated",
                $"Your ticket '{ticket.Title}' status was updated to {ticket.Status}.",
                "TicketStatusChanged",
                ticket.Id), cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ChangeTicketStatusResponse(MapToDto(ticket));
    }

    public async Task<AssignTicketResponse?> AssignAsync(
        Guid ticketId,
        AssignTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        await assignTicketValidator.ValidateAndThrowAsync(request, cancellationToken);

        var ticket = await dbContext.Tickets
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId && !ticket.IsDeleted, cancellationToken);

        if (ticket is null)
        {
            return null;
        }

        var normalizedAssignedToUserId = request.AssignedToUserId.ToString();
        var assignedAgent = await userLookupService.GetActiveAgentByIdAsync(
            normalizedAssignedToUserId,
            cancellationToken);
        if (assignedAgent is null)
        {
            throw CreateValidationException(
                nameof(AssignTicketRequest.AssignedToUserId),
                "AssignedToUserId must reference an active Agent user.");
        }

        var oldAssignedToUserId = ticket.AssignedToUserId;
        var oldStatus = ticket.Status;
        var now = DateTime.UtcNow;
        var performedByUserId = currentUserService.IsAuthenticated
            ? currentUserService.UserId
            : null;
        var performedByDisplayName = currentUserService.IsAuthenticated
            ? NormalizeOptional(currentUserService.FullName) ?? NormalizeOptional(currentUserService.Email)
            : null;

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        ticket.AssignedToUserId = normalizedAssignedToUserId;
        ticket.UpdatedAtUtc = now;

        if (ticket.Status == TicketStatus.Open)
        {
            ticket.Status = TicketStatus.InProgress;
        }

        dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(Ticket),
            EntityId = ticket.Id,
            Action = "TicketAssigned",
            OldValues = SerializeAssignment(oldAssignedToUserId, oldStatus),
            NewValues = SerializeAssignment(ticket.AssignedToUserId, ticket.Status),
            PerformedByUserId = performedByUserId,
            PerformedByDisplayName = performedByDisplayName,
            PerformedAtUtc = now
        });

        await notificationService.CreateAsync(new CreateNotificationRequest(
            ticket.AssignedToUserId,
            "Ticket assigned",
            $"Ticket '{ticket.Title}' has been assigned to you.",
            "TicketAssigned",
            ticket.Id), cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new AssignTicketResponse(
            MapToDto(ticket),
            ticket.Id,
            assignedAgent.Id,
            assignedAgent.DisplayName ?? assignedAgent.Email,
            performedByUserId,
            performedByDisplayName,
            ToUtcOffset(now));
    }

    public async Task<IReadOnlyList<AuditLogDto>?> GetAuditLogsAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        var ticketExists = await dbContext.Tickets
            .AsNoTracking()
            .AnyAsync(ticket => ticket.Id == ticketId && !ticket.IsDeleted, cancellationToken);

        if (!ticketExists)
        {
            return null;
        }

        var ticketMessageIds = dbContext.TicketMessages
            .AsNoTracking()
            .Where(message => message.TicketId == ticketId)
            .Select(message => message.Id);

        var auditLogs = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(auditLog =>
                (auditLog.EntityName == nameof(Ticket) && auditLog.EntityId == ticketId) ||
                (auditLog.EntityName == nameof(TicketMessage) && ticketMessageIds.Contains(auditLog.EntityId)))
            .OrderBy(auditLog => auditLog.PerformedAtUtc)
            .ToListAsync(cancellationToken);

        return auditLogs.Select(MapToAuditLogDto).ToArray();
    }

    public async Task<DeleteTicketResponse?> DeleteAsync(
        Guid ticketId,
        DeleteTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        await deleteTicketValidator.ValidateAndThrowAsync(request, cancellationToken);

        var ticket = await dbContext.Tickets
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId && !ticket.IsDeleted, cancellationToken);

        if (ticket is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var performedByUserId = currentUserService.IsAuthenticated
            ? currentUserService.UserId
            : NormalizeOptional(request.DeletedByUserId);
        var performedByDisplayName = currentUserService.IsAuthenticated
            ? NormalizeOptional(currentUserService.FullName) ?? NormalizeOptional(currentUserService.Email)
            : NormalizeOptional(request.DeletedByDisplayName);

        ticket.IsDeleted = true;
        ticket.UpdatedAtUtc = now;

        dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(Ticket),
            EntityId = ticket.Id,
            Action = "TicketDeleted",
            OldValues = JsonSerializer.Serialize(new { isDeleted = false }),
            NewValues = JsonSerializer.Serialize(new
            {
                isDeleted = true,
                reason = NormalizeOptional(request.Reason)
            }),
            PerformedByUserId = performedByUserId,
            PerformedByDisplayName = performedByDisplayName,
            PerformedAtUtc = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new DeleteTicketResponse(ticket.Id, ticket.IsDeleted, ToUtcOffset(now));
    }

    private static TicketDto MapToDto(Ticket ticket) =>
        new(
            ticket.Id,
            ticket.Title,
            ticket.Description,
            ticket.Status,
            ticket.Priority,
            ticket.Category,
            ticket.Source,
            ticket.CustomerEmail,
            ticket.CustomerName,
            ticket.CustomerUserId,
            ticket.AssignedToUserId,
            ToUtcOffset(ticket.CreatedAtUtc),
            ToUtcOffset(ticket.UpdatedAtUtc),
            ToUtcOffset(ticket.ResolvedAtUtc),
            ToUtcOffset(ticket.ClosedAtUtc));

    private TicketDetailsDto MapToDetailsDto(Ticket ticket) =>
        new(
            ticket.Id,
            ticket.Title,
            ticket.Description,
            ticket.Status,
            ticket.Priority,
            ticket.Category,
            ticket.Source,
            ticket.CustomerEmail,
            ticket.CustomerName,
            ticket.CustomerUserId,
            ticket.AssignedToUserId,
            ToUtcOffset(ticket.CreatedAtUtc),
            ToUtcOffset(ticket.UpdatedAtUtc),
            ToUtcOffset(ticket.ResolvedAtUtc),
            ToUtcOffset(ticket.ClosedAtUtc),
            ticket.Messages
                .Where(message => !IsInRole(AuthRoles.Customer) || !message.IsInternalNote)
                .OrderBy(message => message.CreatedAtUtc)
                .Select(message => MapToDetailsMessageDto(ticket, message))
                .ToArray());

    private static TicketDetailsMessageDto MapToDetailsMessageDto(Ticket ticket, TicketMessage message) =>
        new(
            message.Id,
            message.TicketId,
            message.CreatedByUserId,
            ResolveMessageSenderRole(ticket, message.CreatedByUserId),
            message.CreatedByDisplayName,
            message.Message,
            message.IsInternalNote,
            ToUtcOffset(message.CreatedAtUtc));

    private static string ResolveMessageSenderRole(Ticket ticket, string? senderUserId)
    {
        if (string.IsNullOrWhiteSpace(senderUserId))
        {
            return "System";
        }

        if (string.Equals(senderUserId, ticket.CustomerUserId, StringComparison.Ordinal))
        {
            return AuthRoles.Customer;
        }

        if (string.Equals(senderUserId, ticket.AssignedToUserId, StringComparison.Ordinal))
        {
            return AuthRoles.Agent;
        }

        return AuthRoles.Admin;
    }

    private static string? ResolveAssignedAgentDisplayName(Ticket ticket) =>
        string.IsNullOrWhiteSpace(ticket.AssignedToUserId)
            ? null
            : ticket.Messages
                .Where(message => string.Equals(message.CreatedByUserId, ticket.AssignedToUserId, StringComparison.Ordinal))
                .OrderByDescending(message => message.CreatedAtUtc)
                .Select(message => message.CreatedByDisplayName)
                .FirstOrDefault(displayName => !string.IsNullOrWhiteSpace(displayName));

    private static TicketMessageDto MapToMessageDto(TicketMessage message) =>
        new(
            message.Id,
            message.TicketId,
            message.Message,
            message.IsInternalNote,
            message.CreatedByUserId,
            message.CreatedByDisplayName,
            ToUtcOffset(message.CreatedAtUtc));

    private static AuditLogDto MapToAuditLogDto(AuditLog auditLog) =>
        new(
            auditLog.Id,
            auditLog.EntityName,
            auditLog.EntityId,
            auditLog.Action,
            auditLog.OldValues,
            auditLog.NewValues,
            auditLog.PerformedByUserId,
            auditLog.PerformedByDisplayName,
            ToUtcOffset(auditLog.PerformedAtUtc));

    private static DateTimeOffset ToUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);

    private static DateTimeOffset? ToUtcOffset(DateTime? value) =>
        value is null ? null : ToUtcOffset(value.Value);

    private static string SerializeStatus(TicketStatus status) =>
        JsonSerializer.Serialize(new { status = status.ToString() });

    private static string SerializeAssignment(string? assignedToUserId, TicketStatus status) =>
        JsonSerializer.Serialize(new
        {
            assignedToUserId,
            status = status.ToString()
        });

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static TEnum ParseEnum<TEnum>(string value, TEnum fallback)
        where TEnum : struct, Enum
    {
        var normalized = NormalizeEnumValue(value);

        foreach (var enumValue in Enum.GetValues<TEnum>())
        {
            if (NormalizeEnumValue(enumValue.ToString()) == normalized)
            {
                return enumValue;
            }
        }

        return fallback;
    }

    private static string NormalizeEnumValue(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static MyTicketStatsResponse EmptyMyTicketStats() =>
        new(0, 0, 0, 0, 0, 0, 0, 0, 0);

    private IQueryable<Ticket> ApplyReadAccessFilter(IQueryable<Ticket> query)
    {
        if (!currentUserService.IsAuthenticated)
        {
            return query.Where(_ => false);
        }

        if (IsInRole(AuthRoles.Admin))
        {
            return query;
        }

        if (IsInRole(AuthRoles.Agent))
        {
            var userId = currentUserService.UserId;
            return string.IsNullOrWhiteSpace(userId)
                ? query.Where(_ => false)
                : query.Where(ticket => ticket.AssignedToUserId == userId);
        }

        if (IsInRole(AuthRoles.Customer))
        {
            var userId = currentUserService.UserId;
            return string.IsNullOrWhiteSpace(userId)
                ? query.Where(_ => false)
                : query.Where(ticket => ticket.CustomerUserId == userId);
        }

        return query.Where(_ => false);
    }

    private bool CanRead(Ticket ticket)
    {
        if (!currentUserService.IsAuthenticated)
        {
            return false;
        }

        if (IsInRole(AuthRoles.Admin))
        {
            return true;
        }

        if (IsInRole(AuthRoles.Agent))
        {
            return !string.IsNullOrWhiteSpace(currentUserService.UserId) &&
                ticket.AssignedToUserId == currentUserService.UserId;
        }

        if (IsInRole(AuthRoles.Customer))
        {
            return !string.IsNullOrWhiteSpace(currentUserService.UserId) &&
                ticket.CustomerUserId == currentUserService.UserId;
        }

        return false;
    }

    private bool CanAddMessage(Ticket ticket)
    {
        if (!currentUserService.IsAuthenticated)
        {
            return false;
        }

        if (IsInRole(AuthRoles.Admin))
        {
            return true;
        }

        if (IsInRole(AuthRoles.Agent))
        {
            return !string.IsNullOrWhiteSpace(currentUserService.UserId) &&
                ticket.AssignedToUserId == currentUserService.UserId;
        }

        if (IsInRole(AuthRoles.Customer))
        {
            return !string.IsNullOrWhiteSpace(currentUserService.UserId) &&
                ticket.CustomerUserId == currentUserService.UserId;
        }

        return false;
    }

    private bool CanChangeStatus(Ticket ticket)
    {
        if (!currentUserService.IsAuthenticated)
        {
            return false;
        }

        if (IsInRole(AuthRoles.Admin))
        {
            return true;
        }

        if (IsInRole(AuthRoles.Agent))
        {
            return !string.IsNullOrWhiteSpace(currentUserService.UserId) &&
                ticket.AssignedToUserId == currentUserService.UserId;
        }

        return false;
    }

    private string? ValidateAndNormalizeAssignedToUserIdFilter(string? assignedToUserId)
    {
        if (assignedToUserId is null)
        {
            return null;
        }

        var normalizedAssignedToUserId = NormalizeOptional(assignedToUserId);
        if (normalizedAssignedToUserId is null)
        {
            throw CreateValidationException(
                nameof(GetTicketsQuery.AssignedToUserId),
                "AssignedToUserId must not be empty.");
        }

        if (normalizedAssignedToUserId.Length > 100)
        {
            throw CreateValidationException(
                nameof(GetTicketsQuery.AssignedToUserId),
                "AssignedToUserId must not exceed 100 characters.");
        }

        if (IsInRole(AuthRoles.Customer))
        {
            throw CreateValidationException(
                nameof(GetTicketsQuery.AssignedToUserId),
                "Customers cannot filter tickets by assigned agent.");
        }

        if (IsInRole(AuthRoles.Agent) &&
            !string.Equals(normalizedAssignedToUserId, currentUserService.UserId, StringComparison.Ordinal))
        {
            throw CreateValidationException(
                nameof(GetTicketsQuery.AssignedToUserId),
                "Agents can only filter by their own user id.");
        }

        return normalizedAssignedToUserId;
    }

    private static ValidationException CreateValidationException(string propertyName, string message) =>
        new([new ValidationFailure(propertyName, message)]);

    private static void ValidatePagination(int page, int pageSize)
    {
        if (page < 1)
        {
            throw CreateValidationException(
                nameof(GetTicketsQuery.Page),
                "Page must be at least 1.");
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            throw CreateValidationException(
                nameof(GetTicketsQuery.PageSize),
                $"PageSize must be between 1 and {MaxPageSize}.");
        }
    }

    private static string? ValidateAndNormalizeSearchFilter(string? search)
    {
        if (search is null)
        {
            return null;
        }

        var normalizedSearch = search.Trim();
        if (normalizedSearch.Length == 0)
        {
            throw CreateValidationException(
                nameof(GetTicketsQuery.Search),
                "Search must not be empty.");
        }

        if (normalizedSearch.Length > MaxSearchLength)
        {
            throw CreateValidationException(
                nameof(GetTicketsQuery.Search),
                $"Search must not exceed {MaxSearchLength} characters.");
        }

        return normalizedSearch.ToLowerInvariant();
    }

    private static (string SortBy, string SortDirection) ValidateAndNormalizeSorting(
        string? sortBy,
        string? sortDirection)
    {
        var normalizedSortBy = sortBy?.Trim().ToLowerInvariant();
        var normalizedSortDirection = sortDirection?.Trim().ToLowerInvariant();

        if (sortBy is not null && string.IsNullOrEmpty(normalizedSortBy))
        {
            throw CreateValidationException(
                nameof(GetTicketsQuery.SortBy),
                "SortBy must not be empty.");
        }

        if (sortDirection is not null && string.IsNullOrEmpty(normalizedSortDirection))
        {
            throw CreateValidationException(
                nameof(GetTicketsQuery.SortDirection),
                "SortDirection must not be empty.");
        }

        if (normalizedSortBy is not null &&
            normalizedSortBy is not ("createdat" or "updatedat" or "priority" or "status"))
        {
            throw CreateValidationException(
                nameof(GetTicketsQuery.SortBy),
                "SortBy must be one of: createdAt, updatedAt, priority, status.");
        }

        if (normalizedSortDirection is not null &&
            normalizedSortDirection is not ("asc" or "desc"))
        {
            throw CreateValidationException(
                nameof(GetTicketsQuery.SortDirection),
                "SortDirection must be either asc or desc.");
        }

        return (normalizedSortBy ?? "createdat", sortBy is null ? "desc" : normalizedSortDirection ?? "desc");
    }

    private static IOrderedQueryable<Ticket> ApplySorting(
        IQueryable<Ticket> ticketsQuery,
        string sortBy,
        string sortDirection)
    {
        var descending = sortDirection == "desc";

        return sortBy switch
        {
            "updatedat" => descending
                ? ticketsQuery.OrderByDescending(ticket => ticket.UpdatedAtUtc).ThenBy(ticket => ticket.Id)
                : ticketsQuery.OrderBy(ticket => ticket.UpdatedAtUtc).ThenBy(ticket => ticket.Id),
            "priority" => descending
                ? ticketsQuery.OrderByDescending(ticket =>
                    ticket.Priority == TicketPriority.Low ? 1 :
                    ticket.Priority == TicketPriority.Medium ? 2 :
                    ticket.Priority == TicketPriority.High ? 3 : 4).ThenBy(ticket => ticket.Id)
                : ticketsQuery.OrderBy(ticket =>
                    ticket.Priority == TicketPriority.Low ? 1 :
                    ticket.Priority == TicketPriority.Medium ? 2 :
                    ticket.Priority == TicketPriority.High ? 3 : 4).ThenBy(ticket => ticket.Id),
            "status" => descending
                ? ticketsQuery.OrderByDescending(ticket =>
                    ticket.Status == TicketStatus.Open ? 1 :
                    ticket.Status == TicketStatus.InProgress ? 2 :
                    ticket.Status == TicketStatus.WaitingForCustomer ? 3 :
                    ticket.Status == TicketStatus.Resolved ? 4 : 5).ThenBy(ticket => ticket.Id)
                : ticketsQuery.OrderBy(ticket =>
                    ticket.Status == TicketStatus.Open ? 1 :
                    ticket.Status == TicketStatus.InProgress ? 2 :
                    ticket.Status == TicketStatus.WaitingForCustomer ? 3 :
                    ticket.Status == TicketStatus.Resolved ? 4 : 5).ThenBy(ticket => ticket.Id),
            _ => descending
                ? ticketsQuery.OrderByDescending(ticket => ticket.CreatedAtUtc).ThenBy(ticket => ticket.Id)
                : ticketsQuery.OrderBy(ticket => ticket.CreatedAtUtc).ThenBy(ticket => ticket.Id)
        };
    }

    private void ValidateUnassignedFilter(bool? unassigned, string? assignedToUserId)
    {
        if (unassigned is not true)
        {
            return;
        }

        if (assignedToUserId is not null)
        {
            throw CreateValidationException(
                nameof(GetTicketsQuery.Unassigned),
                "Unassigned cannot be combined with AssignedToUserId.");
        }

        if (!IsInRole(AuthRoles.Admin))
        {
            throw CreateValidationException(
                nameof(GetTicketsQuery.Unassigned),
                "Only Admin users can filter unassigned tickets.");
        }
    }

    private string? ResolveMessageNotificationRecipient(Ticket ticket)
    {
        string? recipientUserId = null;

        if (IsInRole(AuthRoles.Customer))
        {
            recipientUserId = NormalizeOptional(ticket.AssignedToUserId);
        }
        else if (IsInRole(AuthRoles.Agent) || IsInRole(AuthRoles.Admin))
        {
            recipientUserId = NormalizeOptional(ticket.CustomerUserId);
        }

        return string.Equals(recipientUserId, currentUserService.UserId, StringComparison.Ordinal)
            ? null
            : recipientUserId;
    }

    private bool IsInRole(string role) =>
        string.Equals(currentUserService.Role, role, StringComparison.OrdinalIgnoreCase);
}
