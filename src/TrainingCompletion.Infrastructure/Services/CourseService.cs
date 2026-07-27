using Microsoft.EntityFrameworkCore;
using TrainingCompletion.Application;
using TrainingCompletion.Domain;
using TrainingCompletion.Infrastructure.Persistence;

namespace TrainingCompletion.Infrastructure.Services;

public sealed class CourseService(TrainingDbContext dbContext, IClock clock)
{
    public async Task<IReadOnlyList<CourseDto>> GetCoursesAsync(
        string? learnerId,
        CancellationToken cancellationToken)
    {
        var courses = await dbContext.Courses
            .AsNoTracking()
            .OrderBy(x => x.Title)
            .ToListAsync(cancellationToken);

        var enrollmentCounts = await dbContext.Enrollments
            .AsNoTracking()
            .GroupBy(x => x.CourseId)
            .Select(x => new { CourseId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.CourseId, x => x.Count, cancellationToken);

        var enrollments = string.IsNullOrWhiteSpace(learnerId)
            ? new Dictionary<string, EnrollmentStatus>()
            : await dbContext.Enrollments
                .AsNoTracking()
                .Where(x => x.LearnerId == learnerId)
                .ToDictionaryAsync(x => x.CourseId, x => x.Status, cancellationToken);

        var completions = string.IsNullOrWhiteSpace(learnerId)
            ? new Dictionary<string, Guid>()
            : await dbContext.CourseCompletions
                .AsNoTracking()
                .Where(x => x.LearnerId == learnerId)
                .ToDictionaryAsync(x => x.CourseId, x => x.Id, cancellationToken);

        return courses.Select(course => new CourseDto(
            course.Id,
            course.Title,
            course.Description,
            course.Capacity,
            Math.Max(0, course.Capacity - enrollmentCounts.GetValueOrDefault(course.Id)),
            course.IsActive,
            enrollments.GetValueOrDefault(course.Id),
            completions.GetValueOrDefault(course.Id) is var id && id != Guid.Empty ? id : null))
            .ToList();
    }

    public async Task<EnrollmentDto> EnrollAsync(
        string courseId,
        string learnerId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(learnerId))
        {
            throw new ValidationException("learner_required", "A learnerId is required.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var course = await dbContext.Courses
            .FromSqlInterpolated(
                $"""SELECT course_row.*, course_row.xmin FROM "Courses" AS course_row WHERE "Id" = {courseId} FOR UPDATE""")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("course_not_found", "The course was not found.");

        var learner = await dbContext.Learners.SingleOrDefaultAsync(
            x => x.Id == learnerId,
            cancellationToken)
            ?? throw new NotFoundException("learner_not_found", "The learner was not found.");

        if (!course.IsActive || !learner.IsActive)
        {
            throw new ConflictException("inactive_resource", "The learner or course is inactive.");
        }

        if (await dbContext.Enrollments.AnyAsync(
                x => x.CourseId == courseId && x.LearnerId == learnerId,
                cancellationToken))
        {
            throw new ConflictException("duplicate_enrollment", "The learner is already enrolled.");
        }

        var currentCount = await dbContext.Enrollments.CountAsync(
            x => x.CourseId == courseId,
            cancellationToken);
        if (currentCount >= course.Capacity)
        {
            throw new ConflictException("course_full", "The course has reached capacity.");
        }

        var enrollment = new Enrollment
        {
            Id = Guid.CreateVersion7(),
            CourseId = courseId,
            LearnerId = learnerId,
            Status = EnrollmentStatus.Enrolled,
            EnrolledAt = clock.UtcNow
        };
        dbContext.Enrollments.Add(enrollment);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("duplicate_enrollment", "The learner is already enrolled.");
        }

        return new EnrollmentDto(
            enrollment.Id,
            enrollment.CourseId,
            enrollment.LearnerId,
            enrollment.Status,
            enrollment.EnrolledAt);
    }
}
