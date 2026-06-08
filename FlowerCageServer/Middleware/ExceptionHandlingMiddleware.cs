using System.Net;
using System.Text.Json;
using FlowerCageServer.Common.Exceptions;
using FlowerCageServer.Common.Models;

namespace FlowerCageServer.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "API exception occurred.");
            await WriteErrorAsync(context, ex.StatusCode, ex.Message, ex.InnerException?.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred.");
            await WriteErrorAsync(context, HttpStatusCode.InternalServerError, "An unexpected server error occurred.", ex.Message);
        }
    }

    private static Task WriteErrorAsync(HttpContext context, HttpStatusCode statusCode, string message, string? detail)
    {
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var payload = new ApiErrorResponse
        {
            StatusCode = (int)statusCode,
            Message = message,
            Detail = detail,
            TimestampUtc = DateTime.UtcNow
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
