using Bravo.Application.Users.Commands;
using Bravo.Application.Users.Queries;
using Bravo.Domain.Users.ViewModel;
using Bravo.Presentation.Authorization;
using Bravo.Presentation.Extensions;
using Bravo.Shared.Result;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bravo.Presentation.Controllers;

[ApiController]
[Route("api/v1/user")]
public class UserController(
    IMediator _mediator
) : ControllerBase
{

    [HttpGet("me")]
    public async Task<ActionResult<Result<GetUserViewModel?>>> Get()
    {
        var id = HttpContext.GetUserId();
        var response = await _mediator.Send(new GetUserQuery(id));
        return StatusCode(response.StatusCode, response);
    }
    [HttpGet("{id}")]
    [Authorize(Roles = AuthorizationRoles.Administrators)]
    public async Task<ActionResult<Result<GetUserViewModel?>>> Get([FromRoute] GetUserQuery query)
    {
        var response = await _mediator.Send(query);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet]
    [Authorize(Roles = AuthorizationRoles.Administrators)]
    public async Task<ActionResult<Result<IEnumerable<ListUserViewModel>>>> List([FromQuery] ListUserQuery query)
    {
        var response = await _mediator.Send(query);
        return StatusCode(response.StatusCode, response);
    }
    [HttpPost]
    [Authorize(Roles = AuthorizationRoles.Administrators)]
    public async Task<IActionResult> Create([FromBody] CreateUserCommand command)
    {
        var response = await _mediator.Send(command);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPut]
    [Authorize(Roles = AuthorizationRoles.Administrators)]
    public async Task<IActionResult> Update([FromBody] UpdateUserCommand command)
    {
        var response = await _mediator.Send(command);
        return StatusCode(response.StatusCode, response);
    }

    [HttpDelete]
    [Authorize(Roles = AuthorizationRoles.Administrators)]
    public async Task<IActionResult> Delete([FromBody] DeleteUserCommand command)
    {
        var response = await _mediator.Send(command);
        return StatusCode(response.StatusCode, response);
    }

}
