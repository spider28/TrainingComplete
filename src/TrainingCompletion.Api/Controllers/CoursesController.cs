using Microsoft.AspNetCore.Mvc;
using TrainingCompletion.Application;
using TrainingCompletion.Infrastructure.Services;

namespace TrainingCompletion.Api.Controllers;

[ApiController]
[Route("api/courses")]
public sealed class CoursesController(CourseService courseService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CourseDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CourseDto>>> Get(
        [FromQuery] string? learnerId,
        CancellationToken cancellationToken) =>
        Ok(await courseService.GetCoursesAsync(learnerId, cancellationToken));

    [HttpPost("{courseId}/enrollments")]
    [ProducesResponseType<EnrollmentDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<EnrollmentDto>> Enroll(
        string courseId,
        EnrollmentRequest request,
        CancellationToken cancellationToken)
    {
        var enrollment = await courseService.EnrollAsync(
            courseId,
            request.LearnerId,
            cancellationToken);
        return Created($"/api/courses/{courseId}", enrollment);
    }
}

public sealed record EnrollmentRequest(string LearnerId);

