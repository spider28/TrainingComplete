using Microsoft.AspNetCore.Mvc;
using TrainingCompletion.Application;
using TrainingCompletion.Infrastructure.Services;

namespace TrainingCompletion.Api.Controllers;

[ApiController]
[Route("api/course-completions")]
public sealed class CourseCompletionsController(
    CompletionService completionService,
    CertificateDownloadService certificateDownloadService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CompletionStatusDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<CompletionStatusDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CompletionStatusDto>> Create(
        CompletionRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await completionService.CreateAsync(
            request.LearnerId,
            request.CourseId,
            string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim(),
            HttpContext.TraceIdentifier,
            cancellationToken);

        var location = Url.ActionLink(nameof(Get), values: new { completionId = result.Completion.CompletionId })
            ?? $"/api/course-completions/{result.Completion.CompletionId}";
        if (result.Replayed)
        {
            Response.Headers["Idempotency-Replayed"] = "true";
            Response.Headers.Location = location;
            return Ok(result.Completion);
        }

        return Created(location, result.Completion);
    }

    [HttpGet("{completionId:guid}")]
    [ProducesResponseType<CompletionStatusDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CompletionStatusDto>> Get(
        Guid completionId,
        CancellationToken cancellationToken) =>
        Ok(await completionService.GetAsync(completionId, cancellationToken));

    [HttpGet("{completionId:guid}/certificate")]
    [Produces("application/pdf")]
    public async Task<IActionResult> DownloadCertificate(
        Guid completionId,
        CancellationToken cancellationToken)
    {
        var certificate = await certificateDownloadService.DownloadAsync(
            completionId,
            cancellationToken);
        return File(
            certificate.Content,
            certificate.ContentType,
            $"course-certificate-{completionId:N}.pdf",
            enableRangeProcessing: false);
    }
}

public sealed record CompletionRequest(string LearnerId, string CourseId);

