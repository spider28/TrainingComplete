using Microsoft.EntityFrameworkCore;
using TrainingCompletion.Application;
using TrainingCompletion.Infrastructure.Persistence;

namespace TrainingCompletion.Infrastructure.Services;

public sealed class DiagnosticsService(TrainingDbContext dbContext)
{
    public async Task<DiagnosticsDto> GetAsync(CancellationToken cancellationToken)
    {
        var pending = await dbContext.OutboxMessages.CountAsync(
            x => x.PublishedAt == null,
            cancellationToken);
        var outboxFailures = await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(x => x.PublishedAt == null && x.LastError != null)
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .Select(x => new OutboxFailureDto(
                x.Id,
                x.PublishAttempts,
                x.LastError!,
                x.NextAttemptAt))
            .ToListAsync(cancellationToken);
        var consumerFailures = await dbContext.ConsumerFailures
            .AsNoTracking()
            .Where(x => !x.IsResolved)
            .OrderByDescending(x => x.LastFailedAt)
            .Take(20)
            .Select(x => new ConsumerFailureDto(
                x.EventId,
                x.CompletionId,
                x.ConsumerName,
                x.AttemptCount,
                x.LastError,
                x.LastFailedAt))
            .ToListAsync(cancellationToken);

        return new DiagnosticsDto(pending, outboxFailures, consumerFailures);
    }
}
