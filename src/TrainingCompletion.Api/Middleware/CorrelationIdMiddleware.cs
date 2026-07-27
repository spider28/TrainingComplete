namespace TrainingCompletion.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var suppliedValue = context.Request.Headers.TryGetValue(HeaderName, out var supplied)
            ? supplied.ToString().Trim()
            : string.Empty;
        var correlationId = suppliedValue.Length is > 0 and <= 128
            ? suppliedValue
            : Guid.CreateVersion7().ToString();
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (context.RequestServices
                   .GetRequiredService<ILogger<CorrelationIdMiddleware>>()
                   .BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }
}
