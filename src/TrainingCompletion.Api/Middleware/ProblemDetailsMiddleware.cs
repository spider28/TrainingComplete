using Microsoft.AspNetCore.Mvc;
using TrainingCompletion.Application;

namespace TrainingCompletion.Api.Middleware;

public sealed class ProblemDetailsMiddleware(
    RequestDelegate next,
    ILogger<ProblemDetailsMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException exception)
        {
            await WriteProblemAsync(
                context,
                exception.StatusCode,
                exception.ErrorCode,
                exception.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Unhandled API exception.");
            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "unexpected_error",
                "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int status,
        string code,
        string detail)
    {
        var problem = new ProblemDetails
        {
            Type = $"https://training-completion.example/problems/{code}",
            Title = code.Replace('_', ' '),
            Status = status,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = context.TraceIdentifier;
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(
            problem,
            cancellationToken: context.RequestAborted);
    }
}
