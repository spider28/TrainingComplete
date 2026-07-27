using Microsoft.EntityFrameworkCore;
using TrainingCompletion.Application;
using TrainingCompletion.Domain;
using TrainingCompletion.Infrastructure.Persistence;

namespace TrainingCompletion.Infrastructure.Consumers;

public sealed class ConsumerFailureService(TrainingDbContext dbContext, IClock clock)
{
    public async Task RecordAsync(
        CourseCompletedEvent message,
        string consumerName,
        int attempt,
        Exception exception,
        bool terminal,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        var failure = await dbContext.ConsumerFailures.SingleOrDefaultAsync(
            x => x.EventId == message.EventId && x.ConsumerName == consumerName,
            cancellationToken);
        if (failure is null)
        {
            failure = new ConsumerFailure
            {
                EventId = message.EventId,
                CompletionId = message.CompletionId,
                ConsumerName = consumerName,
                LastError = string.Empty
            };
            dbContext.ConsumerFailures.Add(failure);
        }

        failure.AttemptCount = Math.Max(attempt, failure.AttemptCount + 1);
        failure.LastError = SafeError.From(exception);
        failure.LastFailedAt = clock.UtcNow;
        failure.IsResolved = false;

        if (terminal)
        {
            var completion = await dbContext.CourseCompletions.SingleOrDefaultAsync(
                x => x.Id == message.CompletionId,
                cancellationToken);
            if (completion is not null)
            {
                switch (consumerName)
                {
                    case "certificate":
                        completion.CertificateStatus = CertificateStatus.Failed;
                        break;
                    case "reporting":
                        completion.ReportingStatus = WorkflowStatus.Failed;
                        break;
                    case "notification":
                        completion.NotificationStatus = WorkflowStatus.Failed;
                        break;
                }

                completion.Version++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
