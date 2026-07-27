using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using TrainingCompletion.Infrastructure.Consumers;

namespace TrainingCompletion.Functions;

public sealed class NotificationConsumerFunction(
    NotificationHandler handler,
    ConsumerFunctionRunner runner)
{
    [Function(nameof(NotificationConsumerFunction))]
    public Task Run(
        [ServiceBusTrigger(
            "course-completed",
            "notification",
            Connection = "ServiceBusConnection",
            AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken cancellationToken) =>
        runner.RunAsync(
            "notification",
            message,
            messageActions,
            handler.HandleAsync,
            cancellationToken);
}

