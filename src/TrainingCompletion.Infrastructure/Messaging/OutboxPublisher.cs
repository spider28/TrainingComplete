using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrainingCompletion.Application;
using TrainingCompletion.Infrastructure.Persistence;

namespace TrainingCompletion.Infrastructure.Messaging;

public sealed class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    IClock clock,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("ServiceBus:Enabled", false))
        {
            logger.LogInformation("Outbox publisher is disabled.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        do
        {
            await PublishBatchAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task PublishBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
        var now = clock.UtcNow;
        var messages = await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(x =>
                x.PublishedAt == null &&
                (x.NextAttemptAt == null || x.NextAttemptAt <= now))
            .OrderBy(x => x.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var outbox in messages)
        {
            try
            {
                var message = JsonSerializer.Deserialize<CourseCompletedEvent>(outbox.Payload)
                    ?? throw new InvalidOperationException("The outbox payload is invalid.");
                await publisher.PublishAsync(message, cancellationToken);

                var tracked = await dbContext.OutboxMessages.FindAsync(
                    [outbox.Id],
                    cancellationToken);
                if (tracked is not null && tracked.PublishedAt is null)
                {
                    tracked.PublishedAt = clock.UtcNow;
                    tracked.LastError = null;
                    tracked.NextAttemptAt = null;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Failed to publish outbox event {EventId}.",
                    outbox.Id);
                dbContext.ChangeTracker.Clear();
                var tracked = await dbContext.OutboxMessages.FindAsync(
                    [outbox.Id],
                    cancellationToken);
                if (tracked is not null)
                {
                    tracked.PublishAttempts++;
                    tracked.LastError = SafeError.From(exception);
                    var delaySeconds = Math.Min(300, 5 * Math.Pow(2, Math.Min(6, tracked.PublishAttempts - 1)));
                    tracked.NextAttemptAt = clock.UtcNow.AddSeconds(delaySeconds);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }
        }
    }

}
