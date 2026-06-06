using System.Net;
using System.Text.Json;
using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.API.Middlewares;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ISystemLogService systemLogService)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred during request processing.");

            // Log to the persistent SystemLogs table
            await systemLogService.LogAsync(
                LogSeverity.Error,
                "API",
                $"Unhandled Exception: {ex.Message}",
                ex.ToString(),
                context.Request.Path
            );

            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response = new
        {
            message = "An internal server error occurred. Please check the system logs for details.",
            error = exception.Message // In a real production app, you might hide this. Keeping it for UAT debugging.
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
