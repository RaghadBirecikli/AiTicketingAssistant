using AiTicketing.Application.Ai;
using AiTicketing.Application.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace AiTicketing.Api.Controllers;

[ApiController]
[Route("api/ai-ticket-assistant")]
[Produces("application/json")]
public sealed class AiTicketAssistantController(IAiTicketAssistantService assistantService) : ControllerBase
{
    [HttpPost("analyze")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<TicketAssistantResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TicketAssistantResult>>> Analyze(
        [FromBody] TicketAssistantRequest request,
        CancellationToken cancellationToken)
    {
        var result = await assistantService.AnalyzeAsync(request, cancellationToken);
        return Ok(ApiResponse<TicketAssistantResult>.Ok(result));
    }
}
