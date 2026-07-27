using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TrainingCompletion.Functions;
using TrainingCompletion.Infrastructure;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddTrainingInfrastructure(context.Configuration, includeEventPublisher: false);
        services.AddScoped<ConsumerFunctionRunner>();
    })
    .Build();

host.Run();
