using Microsoft.AspNetCore.Mvc;
using TrainingCompletion.Application;
using TrainingCompletion.Infrastructure.Services;

namespace TrainingCompletion.Api.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminController(DiagnosticsService diagnosticsService) : ControllerBase
{
    [HttpGet("diagnostics")]
    [ProducesResponseType<DiagnosticsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DiagnosticsDto>> GetDiagnostics(
        CancellationToken cancellationToken) =>
        Ok(await diagnosticsService.GetAsync(cancellationToken));
}

