using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainingCompletion.Application;
using TrainingCompletion.Domain;
using TrainingCompletion.Infrastructure.Persistence;

namespace TrainingCompletion.Infrastructure.Consumers;

public sealed class CertificateHandler(
    TrainingDbContext dbContext,
    ICertificateDocumentGenerator documentGenerator,
    ICertificateStore certificateStore,
    IClock clock)
{
    public async Task HandleAsync(
        CourseCompletedEvent message,
        CancellationToken cancellationToken)
    {
        if (await IsProcessedAsync(message.EventId, "certificate", cancellationToken))
        {
            return;
        }

        var completion = await dbContext.CourseCompletions.SingleOrDefaultAsync(
            x => x.Id == message.CompletionId,
            cancellationToken)
            ?? throw new NotFoundException("completion_not_found", "The completion was not found.");
        completion.CertificateStatus = CertificateStatus.Processing;
        completion.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);

        var learnerName = await dbContext.Learners
            .Where(x => x.Id == message.LearnerId)
            .Select(x => x.DisplayName)
            .SingleAsync(cancellationToken);
        var courseTitle = await dbContext.Courses
            .Where(x => x.Id == message.CourseId)
            .Select(x => x.Title)
            .SingleAsync(cancellationToken);
        var blobName = $"{message.CompletionId:N}.pdf";
        var pdf = documentGenerator.Generate(learnerName, courseTitle, message.CompletedAt);
        await certificateStore.UploadAsync(blobName, pdf, cancellationToken);

        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (await IsProcessedAsync(message.EventId, "certificate", cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        completion = await dbContext.CourseCompletions.SingleAsync(
            x => x.Id == message.CompletionId,
            cancellationToken);
        completion.CertificateStatus = CertificateStatus.Ready;
        completion.CertificateBlobName = blobName;
        completion.Version++;
        await MarkProcessedAsync(message, "certificate", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private Task<bool> IsProcessedAsync(
        Guid eventId,
        string consumer,
        CancellationToken cancellationToken) =>
        dbContext.ProcessedMessages.AnyAsync(
            x => x.EventId == eventId && x.ConsumerName == consumer,
            cancellationToken);

    private async Task MarkProcessedAsync(
        CourseCompletedEvent message,
        string consumer,
        CancellationToken cancellationToken)
    {
        dbContext.ProcessedMessages.Add(new ProcessedMessage
        {
            EventId = message.EventId,
            ConsumerName = consumer,
            ProcessedAt = clock.UtcNow
        });
        var failure = await dbContext.ConsumerFailures.SingleOrDefaultAsync(
            x => x.EventId == message.EventId && x.ConsumerName == consumer,
            cancellationToken);
        if (failure is not null)
        {
            failure.IsResolved = true;
        }
    }
}

public sealed class ReportingHandler(TrainingDbContext dbContext, IClock clock)
{
    public async Task HandleAsync(
        CourseCompletedEvent message,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (await dbContext.ProcessedMessages.AnyAsync(
            x => x.EventId == message.EventId && x.ConsumerName == "reporting",
            cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "CourseCompletionSummaries" ("CourseId", "CompletionCount", "LastCompletedAt")
            VALUES ({message.CourseId}, 1, {message.CompletedAt})
            ON CONFLICT ("CourseId") DO UPDATE SET
                "CompletionCount" = "CourseCompletionSummaries"."CompletionCount" + 1,
                "LastCompletedAt" = EXCLUDED."LastCompletedAt"
            """, cancellationToken);
        var completion = await dbContext.CourseCompletions.SingleAsync(
            x => x.Id == message.CompletionId,
            cancellationToken);
        completion.ReportingStatus = WorkflowStatus.Completed;
        completion.Version++;
        await MarkProcessedAsync(message, "reporting", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task MarkProcessedAsync(
        CourseCompletedEvent message,
        string consumer,
        CancellationToken cancellationToken)
    {
        dbContext.ProcessedMessages.Add(new ProcessedMessage
        {
            EventId = message.EventId,
            ConsumerName = consumer,
            ProcessedAt = clock.UtcNow
        });
        var failure = await dbContext.ConsumerFailures.SingleOrDefaultAsync(
            x => x.EventId == message.EventId && x.ConsumerName == consumer,
            cancellationToken);
        if (failure is not null)
        {
            failure.IsResolved = true;
        }
    }
}

public sealed class NotificationHandler(
    TrainingDbContext dbContext,
    IClock clock,
    ILogger<NotificationHandler> logger)
{
    public async Task HandleAsync(
        CourseCompletedEvent message,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (await dbContext.ProcessedMessages.AnyAsync(
            x => x.EventId == message.EventId && x.ConsumerName == "notification",
            cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        dbContext.NotificationLogs.Add(new NotificationLog
        {
            Id = Guid.CreateVersion7(),
            CompletionId = message.CompletionId,
            LearnerId = message.LearnerId,
            Type = "CourseCompleted",
            Status = "Simulated",
            CreatedAt = clock.UtcNow
        });
        var completion = await dbContext.CourseCompletions.SingleAsync(
            x => x.Id == message.CompletionId,
            cancellationToken);
        completion.NotificationStatus = WorkflowStatus.Completed;
        completion.Version++;
        dbContext.ProcessedMessages.Add(new ProcessedMessage
        {
            EventId = message.EventId,
            ConsumerName = "notification",
            ProcessedAt = clock.UtcNow
        });
        var failure = await dbContext.ConsumerFailures.SingleOrDefaultAsync(
            x => x.EventId == message.EventId && x.ConsumerName == "notification",
            cancellationToken);
        if (failure is not null)
        {
            failure.IsResolved = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "Simulated completion notification for completion {CompletionId} and learner {LearnerId}.",
            message.CompletionId,
            message.LearnerId);
    }
}

