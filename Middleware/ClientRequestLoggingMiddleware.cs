using Microsoft.AspNetCore.Http;

namespace GestionaGatewayAPI.Middleware;

/// <summary>
/// Logs the client IP address, HTTP method, and request path for each incoming request.
/// </summary>
public sealed class ClientRequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ClientRequestLoggingMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientRequestLoggingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger used to write request details.</param>
    public ClientRequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<ClientRequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Logs request metadata and then invokes the next middleware in the pipeline.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task that represents the asynchronous middleware operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var clientAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var httpMethod = context.Request.Method;
        var requestPath = context.Request.Path.HasValue
            ? context.Request.Path.Value!
            : "/";

        _logger.LogInformation(
            "Incoming request from {ClientAddress}: {HttpMethod} {RequestPath}",
            clientAddress,
            httpMethod,
            requestPath);

        await _next(context);
    }
}
