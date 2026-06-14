using AiTicketing.Application.Auth;
using AiTicketing.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiTicketing.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
[Produces("application/json")]
public sealed class MeController(ICurrentUserProfileService currentUserProfileService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<CurrentUserResponse>>> Get(
        CancellationToken cancellationToken)
    {
        var response = await currentUserProfileService.GetAsync(cancellationToken);

        if (response is null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Authenticated user is no longer available."));
        }

        return Ok(ApiResponse<CurrentUserResponse>.Ok(response));
    }
}
