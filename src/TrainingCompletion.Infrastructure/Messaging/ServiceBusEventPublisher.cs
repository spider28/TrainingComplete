using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using TrainingCompletion.Application;

namespace TrainingCompletion.Infrastructure.Messaging;

public sealed class ServiceBusEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly ServiceBusClient client;
    private readonly ServiceBusSender sender;

    public ServiceBusEventPublisher(IConfiguration configuration)
    {
        var connectionString = configuration["ServiceBus:ConnectionString"];
        client = !string.IsNullOrWhiteSpace(connectionString)
            ? new ServiceBusClient(connectionString)
            : new ServiceBusClient(
                configuration["ServiceBus:FullyQualifiedNamespace"]
                    ?? throw new InvalidOperationException(
                        "Configure ServiceBus:ConnectionString or ServiceBus:FullyQualifiedNamespace."),
                new DefaultAzureCredential());
        sender = client.CreateSender(configuration["ServiceBus:TopicName"] ?? "course-completed");
    }

    public Task PublishAsync(CourseCompletedEvent message, CancellationToken cancellationToken)
    {
        var serviceBusMessage = new ServiceBusMessage(BinaryData.FromObjectAsJson(message))
        {
            MessageId = message.EventId.ToString(),
            CorrelationId = message.CorrelationId,
            Subject = nameof(CourseCompletedEvent),
            ContentType = "application/json"
        };
        serviceBusMessage.ApplicationProperties["eventVersion"] = 1;
        return sender.SendMessageAsync(serviceBusMessage, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await sender.DisposeAsync();
        await client.DisposeAsync();
    }
}

