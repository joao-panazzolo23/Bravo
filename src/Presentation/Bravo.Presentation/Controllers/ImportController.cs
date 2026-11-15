using Bravo.Application.Acquisition.Commands;
using Bravo.Application.Acquisition.Queries;
using Bravo.Application.Acquisition.ViewModels;
using Bravo.Presentation.Authorization;
using Bravo.Shared.Result;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bravo.Presentation.Controllers;

/// <summary>
/// On-demand acquisition of official IBGE, TSE and FGV datasets.
///
/// The scope accepted by <see cref="Start"/> is:
/// <list type="bullet">
/// <item><c>IbgeGeography</c>: no scope. Synchronizes states and municipalities.</item>
/// <item><c>FgvMunicipalityCrosswalk</c>: no scope. Requires the IBGE geography import first.</item>
/// <item><c>TseCandidates</c> / <c>TseElectionMetadata</c>: <c>&lt;year&gt;[:&lt;round&gt;][:&lt;UF&gt;]</c>.</item>
/// <item><c>TseResults</c>: <c>&lt;year&gt;[:&lt;round&gt;][:&lt;UF&gt;]</c>. Without a state the national
/// archive is used, which covers presidential contests only.</item>
/// </list>
///
/// Imports are persisted before they are dispatched and return an execution identifier; the
/// caller follows progress with <see cref="GetStatus"/> and stops the import with
/// <see cref="Cancel"/>.
/// </summary>
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
