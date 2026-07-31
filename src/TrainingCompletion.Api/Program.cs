using System.Text.Json.Serialization;
using TrainingCompletion.Api.Middleware;
using TrainingCompletion.Infrastructure;
using TrainingCompletion.Infrastructure.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddTrainingInfrastructure(builder.Configuration);
builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? ["http://localhost:5173"];
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ProblemDetailsMiddleware>();
app.UseCors();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => Results.Ok(new
{
    service = "Training Completion API",
    status = "running",
    health = "/health",
    openApi = "/openapi/v1.json"
}));
app.MapHealthChecks("/health");
app.MapControllers();
app.Run();

public partial class Program;
