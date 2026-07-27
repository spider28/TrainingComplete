using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using TrainingCompletion.Infrastructure.Consumers;

namespace TrainingCompletion.Functions;

public sealed class CertificateConsumerFunction(
    CertificateHandler handler,
    ConsumerFunctionRunner runner)
{
    [Function(nameof(CertificateConsumerFunction))]
    public Task Run(
        [ServiceBusTrigger(
            "course-completed",
            "certificate",
            Connection = "ServiceBusConnection",
            AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken cancellationToken) =>
        runner.RunAsync(
            "certificate",
            message,
            messageActions,
            handler.HandleAsync,
            cancellationToken);
}

