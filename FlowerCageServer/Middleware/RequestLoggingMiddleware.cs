using System.Diagnostics;
using System.Text;

namespace FlowerCageServer.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var request = context.Request;

        // Log request info
        _logger.LogInformation(
            "[REQ] {Method} {Path}{Query} | Content-Type: {ContentType} | Content-Length: {ContentLength} | Remote: {RemoteIp}",
            request.Method,
            request.Path,
            request.QueryString,
            request.ContentType ?? "-",
            request.ContentLength?.ToString() ?? "-",
            context.Connection.RemoteIpAddress);

        // Capture request body for POST/PUT/PATCH
        string? requestBody = null;
        if (request.ContentLength > 0 &&
            (request.Method == "POST" || request.Method == "PUT" || request.Method == "PATCH"))
        {
            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            requestBody = await reader.ReadToEndAsync();
            request.Body.Position = 0;

            // Truncate very large bodies
            if (requestBody.Length > 2000)
            {
                requestBody = string.Concat(requestBody.AsSpan(0, 2000), "... (truncated)");
            }

            _logger.LogDebug("[REQ BODY] {Body}", requestBody);
        }

        // Capture response
        var originalResponseBody = context.Response.Body;
        using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            // Read response body
            responseBuffer.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(responseBuffer).ReadToEndAsync();
            responseBuffer.Seek(0, SeekOrigin.Begin);

            // Copy back to original stream
            await responseBuffer.CopyToAsync(originalResponseBody);
            context.Response.Body = originalResponseBody;

            var statusCode = context.Response.StatusCode;
            var logLevel = statusCode >= 500 ? LogLevel.Error :
                           statusCode >= 400 ? LogLevel.Warning :
                           LogLevel.Information;

            _logger.Log(logLevel,
                "[RES] {Method} {Path} => {StatusCode} | {ElapsedMs}ms | Response-Length: {ResponseLength}",
                request.Method,
                request.Path,
                statusCode,
                stopwatch.ElapsedMilliseconds,
                responseBody.Length);

            // Log response body at Debug level for non-2xx or always for errors
            if (statusCode >= 400 && responseBody.Length > 0)
            {
                var truncatedResponse = responseBody.Length > 2000
                    ? string.Concat(responseBody.AsSpan(0, 2000), "... (truncated)")
                    : responseBody;
                _logger.LogWarning("[RES BODY] {Body}", truncatedResponse);
            }
            else if (_logger.IsEnabled(LogLevel.Debug) && responseBody.Length > 0)
            {
                var truncatedResponse = responseBody.Length > 2000
                    ? string.Concat(responseBody.AsSpan(0, 2000), "... (truncated)")
                    : responseBody;
                _logger.LogDebug("[RES BODY] {Body}", truncatedResponse);
            }
        }
    }
}
