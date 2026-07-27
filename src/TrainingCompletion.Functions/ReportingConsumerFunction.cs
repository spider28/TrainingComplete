using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using TrainingCompletion.Infrastructure.Consumers;

namespace TrainingCompletion.Functions;

public sealed class ReportingConsumerFunction(
    ReportingHandler handler,
    ConsumerFunctionRunner runner)
{
    [Function(nameof(ReportingConsumerFunction))]
    public Task Run(
        [ServiceBusTrigger(
            "course-completed",
            "reporting",
            Connection = "ServiceBusConnection",
            AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken cancellationToken) =>
        runner.RunAsync(
            "reporting",
            message,
            messageActions,
            handler.HandleAsync,
            cancellationToken);
}

