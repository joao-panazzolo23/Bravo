using Bravo.Application.Acquisition.Commands;
using Bravo.Application.Acquisition.Queries;
using Bravo.Application.Acquisition.ViewModels;
using Bravo.Presentation.Authorization;
using Bravo.Shared.Result;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bravo.Presentation.Controllers;

[ApiController]
[Route("api/v1/import")]
public class ImportController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = AuthorizationRoles.Administrators)]
    public async Task<ActionResult<Result<StartImportResult>>> Start([FromBody] StartImportCommand command)
    {
        var response = await mediator.Send(command);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = AuthorizationRoles.Administrators)]
    public async Task<ActionResult<Result<ImportStatusViewModel?>>> GetStatus([FromRoute] Guid id)
    {
        var response = await mediator.Send(new GetImportStatusQuery(id));
        return StatusCode(response.StatusCode, response);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = AuthorizationRoles.Administrators)]
    public async Task<IActionResult> Cancel([FromRoute] Guid id)
    {
        var response = await mediator.Send(new CancelImportCommand(id));
        return StatusCode(response.StatusCode, response);
    }
}

