using System.Text.Encodings.Web;
using System.Text.Json;

namespace GestionaGatewayAPI.Middleware;

/// <summary>
/// Logs successful and error JSON response bodies in an indented format when debug logging is enabled.
/// </summary>
public sealed class ResponseJsonLoggingMiddleware
{
    private static readonly JsonSerializerOptions LogJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ResponseJsonLoggingMiddleware> _logger;

    public ResponseJsonLoggingMiddleware(
        RequestDelegate next,
        ILogger<ResponseJsonLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_logger.IsEnabled(LogLevel.Debug))
        {
            await _next(context);
            return;
        }

        var originalResponseBody = context.Response.Body;
        await using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        try
        {
            await _next(context);

            if (IsJsonContentType(context.Response.ContentType) && responseBuffer.Length > 0)
            {
                responseBuffer.Position = 0;

                try
                {
                    using var jsonDocument = await JsonDocument.ParseAsync(
                        responseBuffer,
                        cancellationToken: context.RequestAborted);
                    var formattedJson = JsonSerializer.Serialize(
                        jsonDocument.RootElement,
                        LogJsonOptions);

                    _logger.LogDebug(
                        "Outgoing JSON response for {HttpMethod} {RequestPath} with status {StatusCode}:{NewLine}{ResponseBody}",
                        context.Request.Method,
                        context.Request.Path,
                        context.Response.StatusCode,
                        Environment.NewLine,
                        formattedJson);
                }
                catch (JsonException exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Could not format JSON response for {HttpMethod} {RequestPath} with status {StatusCode}",
                        context.Request.Method,
                        context.Request.Path,
                        context.Response.StatusCode);
                }
            }
        }
        finally
        {
            responseBuffer.Position = 0;
            context.Response.Body = originalResponseBody;
            await responseBuffer.CopyToAsync(originalResponseBody);
        }
    }

    private static bool IsJsonContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var mediaType = contentType.Split(';', 2)[0].Trim();
        return mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
    }
}
