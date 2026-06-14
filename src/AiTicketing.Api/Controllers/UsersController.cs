using AiTicketing.Application.Common.Models;
using AiTicketing.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiTicketing.Api.Controllers;

[ApiController]
[Route("api/users")]
[Produces("application/json")]
public sealed class UsersController(IUserLookupService userLookupService) : ControllerBase
{
    [HttpGet("agents")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AgentLookupResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AgentLookupResponse>>>> GetAgents(
        CancellationToken cancellationToken)
    {
        var response = await userLookupService.GetAgentsAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<AgentLookupResponse>>.Ok(response));
    }
}
