using Bravo.Application.Importing.Commands;
using Bravo.Application.Importing.Queries;
using Bravo.Application.Importing.ViewModels;
using Bravo.Presentation.Authorization;
using Bravo.Shared.Result;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bravo.Presentation.Controllers;

[ApiController]
[Route("api/v1/import")]
[AllowAnonymous]
public class ImportController(IMediator mediator) : ControllerBase
{
    [HttpPost("ibge")]
    // [Authorize(Roles = AuthorizationRoles.Administrators)]
    public async Task<ActionResult<Result<ImportJobCreatedViewModel>>> StartIbge(
        [FromBody] StartIbgeImportCommand command
    )
    {
        var response = await mediator.Send(command);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("{id:guid}")]
    // [Authorize(Roles = AuthorizationRoles.Administrators)]
    public async Task<ActionResult<Result<ImportJobStatusViewModel?>>> GetStatus(
        [FromRoute] Guid id
    )
    {
        var response = await mediator.Send(new GetImportJobStatusQuery(id));
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("{id:guid}/failures")]
    // [Authorize(Roles = AuthorizationRoles.Administrators)]
    public async Task<ActionResult<Result<IReadOnlyList<ImportFailedRecordViewModel>>>> GetFailures(
        [FromRoute] Guid id,
        [FromQuery] int limit = 100)
    {
        var response = await mediator.Send(new ListImportFailedRecordsQuery(id, limit));
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("{id:guid}/retry")]
    // [Authorize(Roles = AuthorizationRoles.Administrators)]
    public async Task<ActionResult<Result<ImportJobCreatedViewModel>>> Retry([FromRoute] Guid id)
    {
        var response = await mediator.Send(new RetryImportJobCommand(id));
        return StatusCode(response.StatusCode, response);
    }

    [HttpDelete("{id:guid}")]
    // [Authorize(Roles = AuthorizationRoles.Administrators)]
    public async Task<IActionResult> Cancel([FromRoute] Guid id)
    {
        var response = await mediator.Send(new CancelImportJobCommand(id));
        return StatusCode(response.StatusCode, response);
    }
}
