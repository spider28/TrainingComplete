using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TrainingCompletion.Application;
using TrainingCompletion.Infrastructure.Consumers;

namespace TrainingCompletion.Functions;

public sealed class ConsumerFunctionRunner(
    ConsumerFailureService failureService,
    ILogger<ConsumerFunctionRunner> logger)
{
    public async Task RunAsync(
        string consumerName,
        ServiceBusReceivedMessage receivedMessage,
        ServiceBusMessageActions messageActions,
        Func<CourseCompletedEvent, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        CourseCompletedEvent? message = null;
        try
        {
            message = receivedMessage.Body.ToObjectFromJson<CourseCompletedEvent>()
                ?? throw new InvalidOperationException("Message body is empty.");
            using var scope = logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = message.CorrelationId,
                ["EventId"] = message.EventId,
                ["CompletionId"] = message.CompletionId,
                ["Consumer"] = consumerName
            });
            await handler(message, cancellationToken);
            await messageActions.CompleteMessageAsync(receivedMessage, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "{Consumer} failed on Service Bus delivery {DeliveryCount}.",
                consumerName,
                receivedMessage.DeliveryCount);

            var terminal = receivedMessage.DeliveryCount >= 5;
            if (message is not null)
            {
                await failureService.RecordAsync(
                    message,
                    consumerName,
                    receivedMessage.DeliveryCount,
                    exception,
                    terminal,
                    cancellationToken);
            }

            if (terminal)
            {
                await messageActions.DeadLetterMessageAsync(
                    receivedMessage,
                    cancellationToken: cancellationToken);
            }
            else
            {
                await messageActions.AbandonMessageAsync(
                    receivedMessage,
                    cancellationToken: cancellationToken);
            }
        }
    }
}

