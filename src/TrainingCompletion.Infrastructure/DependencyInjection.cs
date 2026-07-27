using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrainingCompletion.Application;
using TrainingCompletion.Infrastructure.Certificates;
using TrainingCompletion.Infrastructure.Consumers;
using TrainingCompletion.Infrastructure.Messaging;
using TrainingCompletion.Infrastructure.Persistence;
using TrainingCompletion.Infrastructure.Services;

namespace TrainingCompletion.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTrainingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool includeEventPublisher = true)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? configuration["PostgresConnection"]
            ?? "Host=localhost;Port=5432;Database=training_dev;Username=postgres;Password=postgres";

        services.AddDbContext<TrainingDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(TrainingDbContext).Assembly.FullName)));
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<CourseService>();
        services.AddScoped<CompletionService>();
        services.AddScoped<DiagnosticsService>();
        services.AddScoped<CertificateDownloadService>();
        services.AddScoped<CertificateHandler>();
        services.AddScoped<ReportingHandler>();
        services.AddScoped<NotificationHandler>();
        services.AddScoped<ConsumerFailureService>();
        services.AddSingleton<ICertificateDocumentGenerator, QuestPdfCertificateGenerator>();
        services.AddSingleton<ICertificateStore, AzureBlobCertificateStore>();
        if (includeEventPublisher)
        {
            services.AddSingleton<IEventPublisher, ServiceBusEventPublisher>();
        }

        return services;
    }
}
