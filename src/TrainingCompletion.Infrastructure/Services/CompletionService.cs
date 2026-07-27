using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrainingCompletion.Application;
using TrainingCompletion.Domain;
using TrainingCompletion.Infrastructure.Persistence;

namespace TrainingCompletion.Infrastructure.Services;

public sealed class CompletionService(TrainingDbContext dbContext, IClock clock)
{
    public async Task<CompletionCreationResult> CreateAsync(
        string learnerId,
        string courseId,
        string? idempotencyKey,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ValidateInput(learnerId, courseId, idempotencyKey);
        var requestHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{learnerId}\n{courseId}")));

        if (idempotencyKey is not null)
        {
            var replay = await dbContext.CourseCompletions
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
            if (replay is not null)
            {
                if (!string.Equals(replay.RequestHash, requestHash, StringComparison.Ordinal))
                {
                    throw new ConflictException(
                        "idempotency_key_reused",
                        "The Idempotency-Key was already used for another request.");
                }

                return new CompletionCreationResult(ToDto(replay), true);
            }
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var learnerExists = await dbContext.Learners.AnyAsync(
            x => x.Id == learnerId && x.IsActive,
            cancellationToken);
        if (!learnerExists)
        {
            throw new NotFoundException("learner_not_found", "The active learner was not found.");
        }

        var courseExists = await dbContext.Courses.AnyAsync(
            x => x.Id == courseId && x.IsActive,
            cancellationToken);
        if (!courseExists)
        {
            throw new NotFoundException("course_not_found", "The active course was not found.");
        }

        var enrollment = await dbContext.Enrollments.SingleOrDefaultAsync(
            x => x.CourseId == courseId && x.LearnerId == learnerId,
            cancellationToken);
        if (enrollment is null)
        {
            throw new ConflictException(
                "enrollment_required",
                "The learner must enroll before completing the course.");
        }

        if (await dbContext.CourseCompletions.AnyAsync(
                x => x.CourseId == courseId && x.LearnerId == learnerId,
                cancellationToken))
        {
            throw new ConflictException(
                "duplicate_completion",
                "The learner has already completed this course.");
        }

        var completedAt = clock.UtcNow;
        var completion = new CourseCompletion
        {
            Id = Guid.CreateVersion7(),
            CourseId = courseId,
            LearnerId = learnerId,
            CompletedAt = completedAt,
            CertificateStatus = CertificateStatus.Pending,
            ReportingStatus = WorkflowStatus.Pending,
            NotificationStatus = WorkflowStatus.Pending,
            Version = 1,
            IdempotencyKey = idempotencyKey,
            RequestHash = idempotencyKey is null ? null : requestHash
        };
        var message = new CourseCompletedEvent(
            Guid.CreateVersion7(),
            completion.Id,
            learnerId,
            courseId,
            completedAt,
            correlationId);
        var outbox = new OutboxMessage
        {
            Id = message.EventId,
            EventType = nameof(CourseCompletedEvent),
            Payload = JsonSerializer.Serialize(message),
            CreatedAt = completedAt,
            NextAttemptAt = completedAt
        };

        enrollment.Status = EnrollmentStatus.Completed;
        dbContext.CourseCompletions.Add(completion);
        dbContext.OutboxMessages.Add(outbox);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            if (idempotencyKey is not null)
            {
                var replay = await dbContext.CourseCompletions
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
                if (replay is not null && replay.RequestHash == requestHash)
                {
                    return new CompletionCreationResult(ToDto(replay), true);
                }
            }

            throw new ConflictException(
                "duplicate_completion",
                "The learner has already completed this course.");
        }

        return new CompletionCreationResult(ToDto(completion), false);
    }

    public async Task<CompletionStatusDto> GetAsync(Guid completionId, CancellationToken cancellationToken)
    {
        var completion = await dbContext.CourseCompletions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == completionId, cancellationToken)
            ?? throw new NotFoundException("completion_not_found", "The completion was not found.");
        return ToDto(completion);
    }

    private static void ValidateInput(string learnerId, string courseId, string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(learnerId) || string.IsNullOrWhiteSpace(courseId))
        {
            throw new ValidationException(
                "completion_input_required",
                "learnerId and courseId are required.");
        }

        if (idempotencyKey?.Length > 128)
        {
            throw new ValidationException(
                "idempotency_key_too_long",
                "Idempotency-Key cannot exceed 128 characters.");
        }
    }

    internal static CompletionStatusDto ToDto(CourseCompletion completion) =>
        new(
            completion.Id,
            completion.CourseId,
            completion.LearnerId,
            "Completed",
            completion.CertificateStatus,
            completion.ReportingStatus,
            completion.NotificationStatus,
            completion.Version,
            completion.CertificateStatus == CertificateStatus.Ready &&
            !string.IsNullOrWhiteSpace(completion.CertificateBlobName));
}
