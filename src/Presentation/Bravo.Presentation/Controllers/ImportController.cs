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
public class ImportController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = AuthorizationRoles.Administrators)]
    public async Task<ActionResult<Result<ImportJobCreatedViewModel>>> Create(
        [FromBody] CreateImportJobCommand command)
    {
        var response = await mediator.Send(command);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("types")]
    [Authorize(Roles = AuthorizationRoles.Administrators)]
    public async Task<ActionResult<Result<IReadOnlyList<string>>>> ListTypes()
    {
        var response = await mediator.Send(new ListImportTypesQuery());
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = AuthorizationRoles.Administrators)]
    public async Task<ActionResult<Result<ImportJobStatusViewModel?>>> GetStatus([FromRoute] Guid id)
    {
        var response = await mediator.Send(new GetImportJobStatusQuery(id));
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("{id:guid}/failures")]
    [Authorize(Roles = AuthorizationRoles.Administrators)]
    public async Task<ActionResult<Result<IReadOnlyList<ImportFailedRecordViewModel>>>> GetFailures(
        [FromRoute] Guid id,
        [FromQuery] int limit = 100)
    {
        var response = await mediator.Send(new ListImportFailedRecordsQuery(id, limit));
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("{id:guid}/retry")]
    [Authorize(Roles = AuthorizationRoles.Administrators)]
    public async Task<ActionResult<Result<ImportJobCreatedViewModel>>> Retry([FromRoute] Guid id)
    {
        var response = await mediator.Send(new RetryImportJobCommand(id));
        return StatusCode(response.StatusCode, response);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = AuthorizationRoles.Administrators)]
    public async Task<ActionResult<Result<Unit>>> Cancel([FromRoute] Guid id)
    {
        var response = await mediator.Send(new CancelImportJobCommand(id));
        return StatusCode(response.StatusCode, response);
    }
}
