using AiTicketing.Api.Filters;
using AiTicketing.Application.Ai;
using AiTicketing.Application.Common.Models;
using AiTicketing.Application.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace AiTicketing.Api.Controllers;

[ApiController]
[Route("api/tickets")]
[Produces("application/json")]
public sealed class TicketsController(ITicketService ticketService) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin,Agent,Customer")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<TicketDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<TicketDto>>>> GetList(
        [FromQuery] GetTicketsQuery query,
        CancellationToken cancellationToken)
    {
        if (Request.Query.ContainsKey("assignedToUserId") &&
            string.IsNullOrWhiteSpace(Request.Query["assignedToUserId"]))
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Validation failed.",
                ["AssignedToUserId must not be empty."]));
        }

        if (Request.Query.ContainsKey("search") &&
            string.IsNullOrWhiteSpace(Request.Query["search"]))
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Validation failed.",
                ["Search must not be empty."]));
        }

        if (Request.Query.ContainsKey("sortBy") &&
            string.IsNullOrWhiteSpace(Request.Query["sortBy"]))
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Validation failed.",
                ["SortBy must not be empty."]));
        }

        if (Request.Query.ContainsKey("sortDirection") &&
            string.IsNullOrWhiteSpace(Request.Query["sortDirection"]))
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Validation failed.",
                ["SortDirection must not be empty."]));
        }

        var response = await ticketService.GetListAsync(query, cancellationToken);

        return Ok(ApiResponse<PagedResult<TicketDto>>.Ok(response));
    }

    [HttpGet("stats")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<TicketStatsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<TicketStatsResponse>>> GetStats(
        CancellationToken cancellationToken)
    {
        var response = await ticketService.GetStatsAsync(cancellationToken);

        return Ok(ApiResponse<TicketStatsResponse>.Ok(response));
    }

    [HttpGet("my-stats")]
    [Authorize(Roles = "Agent,Customer")]
    [ProducesResponseType(typeof(ApiResponse<MyTicketStatsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<MyTicketStatsResponse>>> GetMyStats(
        CancellationToken cancellationToken)
    {
        var response = await ticketService.GetMyStatsAsync(cancellationToken);

        return Ok(ApiResponse<MyTicketStatsResponse>.Ok(response));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,Agent,Customer")]
    [ProducesResponseType(typeof(ApiResponse<TicketDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TicketDetailsDto>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var ticket = await ticketService.GetByIdAsync(id, cancellationToken);

        if (ticket is null)
        {
            return NotFound(ApiResponse<object>.Fail("Ticket was not found."));
        }

        return Ok(ApiResponse<TicketDetailsDto>.Ok(ticket));
    }

    [HttpGet("{id:guid}/audit-logs")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AuditLogDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditLogDto>>>> GetAuditLogs(
        Guid id,
        CancellationToken cancellationToken)
    {
        var auditLogs = await ticketService.GetAuditLogsAsync(id, cancellationToken);

        if (auditLogs is null)
        {
            return NotFound(ApiResponse<object>.Fail("Ticket was not found."));
        }

        return Ok(ApiResponse<IReadOnlyList<AuditLogDto>>.Ok(auditLogs));
    }

    [HttpPost("{id:guid}/messages")]
    [Authorize(Roles = "Admin,Agent,Customer")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<AddTicketMessageResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AddTicketMessageResponse>>> AddMessage(
        Guid id,
        [FromBody] AddTicketMessageRequest request,
        CancellationToken cancellationToken)
    {
        var response = await ticketService.AddMessageAsync(id, request, cancellationToken);

        if (response is null)
        {
            return NotFound(ApiResponse<object>.Fail("Ticket was not found."));
        }

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<AddTicketMessageResponse>.Ok(response, "Ticket message added successfully."));
    }

    [HttpPost("{id:guid}/internal-notes")]
    [Authorize(Roles = "Admin,Agent")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<AddInternalNoteResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AddInternalNoteResponse>>> AddInternalNote(
        Guid id,
        [FromBody] AddInternalNoteRequest request,
        CancellationToken cancellationToken)
    {
        var response = await ticketService.AddInternalNoteAsync(id, request, cancellationToken);

        if (response is null)
        {
            return NotFound(ApiResponse<object>.Fail("Ticket was not found."));
        }

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<AddInternalNoteResponse>.Ok(response, "Internal note added successfully."));
    }

    [HttpPost("{id:guid}/ai/suggest-reply")]
    [Authorize(Roles = "Admin,Agent")]
    [EnableRateLimiting(AiRateLimitPolicyNames.AiEndpoints)]
    [AiOperationTelemetry(AiOperationNames.SuggestReply)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<SuggestedReplyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<SuggestedReplyResponse>>> SuggestReply(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] SuggestReplyRequest? request,
        CancellationToken cancellationToken)
    {
        var response = await ticketService.SuggestReplyAsync(
            id,
            request ?? new SuggestReplyRequest(),
            cancellationToken);

        if (response is null)
        {
            return NotFound(ApiResponse<object>.Fail("Ticket was not found."));
        }

        return Ok(ApiResponse<SuggestedReplyResponse>.Ok(response));
    }

    [HttpPost("{id:guid}/ai/summarize")]
    [Authorize(Roles = "Admin,Agent")]
    [EnableRateLimiting(AiRateLimitPolicyNames.AiEndpoints)]
    [AiOperationTelemetry(AiOperationNames.Summarize)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<TicketSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<TicketSummaryResponse>>> Summarize(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] SummarizeTicketRequest? request,
        CancellationToken cancellationToken)
    {
        request ??= new SummarizeTicketRequest();

        if (request.IncludeInternalNotes && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var response = await ticketService.SummarizeAsync(id, request, cancellationToken);

        if (response is null)
        {
            return NotFound(ApiResponse<object>.Fail("Ticket was not found."));
        }

        return Ok(ApiResponse<TicketSummaryResponse>.Ok(response));
    }

    [HttpPost("{id:guid}/ai/suggest-triage")]
    [Authorize(Roles = "Admin,Agent")]
    [EnableRateLimiting(AiRateLimitPolicyNames.AiEndpoints)]
    [AiOperationTelemetry(AiOperationNames.SuggestTriage)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<TicketTriageSuggestionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<TicketTriageSuggestionResponse>>> SuggestTriage(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] SuggestTriageRequest? request,
        CancellationToken cancellationToken)
    {
        var response = await ticketService.SuggestTriageAsync(
            id,
            request ?? new SuggestTriageRequest(),
            cancellationToken);

        if (response is null)
        {
            return NotFound(ApiResponse<object>.Fail("Ticket was not found."));
        }

        return Ok(ApiResponse<TicketTriageSuggestionResponse>.Ok(response));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Admin,Agent")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<ChangeTicketStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ChangeTicketStatusResponse>>> ChangeStatus(
        Guid id,
        [FromBody] ChangeTicketStatusRequest request,
        CancellationToken cancellationToken)
    {
        var response = await ticketService.ChangeStatusAsync(id, request, cancellationToken);

        if (response is null)
        {
            return NotFound(ApiResponse<object>.Fail("Ticket was not found."));
        }

        return Ok(ApiResponse<ChangeTicketStatusResponse>.Ok(response, "Ticket status changed successfully."));
    }

    [HttpPatch("{id:guid}/assign")]
    [Authorize(Roles = "Admin")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<AssignTicketResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AssignTicketResponse>>> Assign(
        Guid id,
        [FromBody] AssignTicketRequest request,
        CancellationToken cancellationToken)
    {
        var response = await ticketService.AssignAsync(id, request, cancellationToken);

        if (response is null)
        {
            return NotFound(ApiResponse<object>.Fail("Ticket was not found."));
        }

        return Ok(ApiResponse<AssignTicketResponse>.Ok(response, "Ticket assigned successfully."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<DeleteTicketResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DeleteTicketResponse>>> Delete(
        Guid id,
        [FromBody] DeleteTicketRequest request,
        CancellationToken cancellationToken)
    {
        var response = await ticketService.DeleteAsync(id, request, cancellationToken);

        if (response is null)
        {
            return NotFound(ApiResponse<object>.Fail("Ticket was not found."));
        }

        return Ok(ApiResponse<DeleteTicketResponse>.Ok(response, "Ticket deleted successfully."));
    }

    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<CreateTicketResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<CreateTicketResponse>>> Create(
        [FromBody] CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        var response = await ticketService.CreateAsync(request, cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<CreateTicketResponse>.Ok(response, "Ticket created successfully."));
    }
}
