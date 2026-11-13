using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Bravo.Shared.Result;
using Bravo.Application.Authentication.Commands;
using Bravo.Domain.Auth.ViewModel;
using Bravo.Presentation.Extensions;

namespace Bravo.Presentation.Controllers;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting(SecurityExtensions.AuthenticationRateLimit)]
[Route("api/v1/login")]
public class LoginController(IMediator _mediator) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Result<LoginViewModel>>> Login([FromBody] LoginCommand command)
    {
        var result = await _mediator.Send(command);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<Result<LoginViewModel>>> Refresh([FromBody] RefreshTokenCommand command)
    {
        var result = await _mediator.Send(command);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("recovery")]
    public async Task<IActionResult> CreateRecovery([FromBody] RecoveryPasswordCommand command)
    {
        var result = await _mediator.Send(command);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("reset")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command)
    {
        var result = await _mediator.Send(command);
        return StatusCode(result.StatusCode, result);
    }
}
