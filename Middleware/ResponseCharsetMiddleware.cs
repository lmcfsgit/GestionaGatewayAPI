using System.Text;

namespace GestionaGatewayAPI.Middleware;

/// <summary>
/// Encodes JSON responses using the charset requested through Accept-Charset.
/// Responses remain UTF-8 when no supported response charset is requested.
/// </summary>
public sealed class ResponseCharsetMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ResponseCharsetMiddleware> _logger;

    public ResponseCharsetMiddleware(
        RequestDelegate next,
        ILogger<ResponseCharsetMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var responseEncoding = ResolveEncoding(context);

        if (responseEncoding is null || responseEncoding.CodePage == Encoding.UTF8.CodePage)
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

            responseBuffer.Position = 0;
            if (!IsJsonContentType(context.Response.ContentType) || responseBuffer.Length == 0)
            {
                await responseBuffer.CopyToAsync(originalResponseBody);
                return;
            }

            using var reader = new StreamReader(
                responseBuffer,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                leaveOpen: true);
            var responseText = await reader.ReadToEndAsync(context.RequestAborted);

            try
            {
                var encodedResponse = responseEncoding.GetBytes(responseText);
                var mediaType = context.Response.ContentType!.Split(';', 2)[0].Trim();
                context.Response.ContentType = $"{mediaType}; charset={responseEncoding.WebName}";
                context.Response.ContentLength = encodedResponse.Length;
                await originalResponseBody.WriteAsync(encodedResponse, context.RequestAborted);
            }
            catch (EncoderFallbackException exception)
            {
                _logger.LogWarning(
                    exception,
                    "JSON response for {HttpMethod} {RequestPath} contains characters that cannot be encoded as {Charset}; UTF-8 was used instead",
                    context.Request.Method,
                    context.Request.Path,
                    responseEncoding.WebName);

                responseBuffer.Position = 0;
                await responseBuffer.CopyToAsync(originalResponseBody);
            }
        }
        finally
        {
            context.Response.Body = originalResponseBody;
        }
    }

    private Encoding? ResolveEncoding(HttpContext context)
    {
        var acceptedCharsets = context.Request.GetTypedHeaders().AcceptCharset;
        if (acceptedCharsets is null || acceptedCharsets.Count == 0)
        {
            return null;
        }

        foreach (var acceptedCharset in acceptedCharsets
            .Where(value => (value.Quality ?? 1) > 0)
            .OrderByDescending(value => value.Quality ?? 1))
        {
            var normalizedCharset = acceptedCharset.Value.Value?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(normalizedCharset) || normalizedCharset == "*")
            {
                return null;
            }

            try
            {
                return Encoding.GetEncoding(
                    normalizedCharset,
                    EncoderFallback.ExceptionFallback,
                    DecoderFallback.ExceptionFallback);
            }
            catch (ArgumentException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Request for {HttpMethod} {RequestPath} requested unsupported response charset {Charset}",
                    context.Request.Method,
                    context.Request.Path,
                    normalizedCharset);
            }
        }

        return null;
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
