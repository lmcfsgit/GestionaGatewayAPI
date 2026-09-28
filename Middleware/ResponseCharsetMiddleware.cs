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

    /// <summary>
    /// Initializes a new instance of the <see cref="ResponseCharsetMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the request pipeline.</param>
    /// <param name="logger">The logger used to report charset conversion problems.</param>
    public ResponseCharsetMiddleware(
        RequestDelegate next,
        ILogger<ResponseCharsetMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Executes the middleware and encodes a non-empty JSON response using the charset
    /// selected from the request's Accept-Charset header.
    /// </summary>
    /// <param name="context">The context for the current HTTP request and response.</param>
    /// <returns>A task that represents execution of the middleware.</returns>
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

    /// <summary>
    /// Resolves the most preferred supported charset from the request's Accept-Charset header.
    /// </summary>
    /// <param name="context">The context containing the request headers.</param>
    /// <returns>
    /// The selected encoding, or <see langword="null"/> when no specific supported charset
    /// is requested and the response should remain UTF-8.
    /// </returns>
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

    /// <summary>
    /// Determines whether a content type represents JSON or a JSON-based media type.
    /// </summary>
    /// <param name="contentType">The response content type to examine.</param>
    /// <returns><see langword="true"/> when the content type represents JSON; otherwise, <see langword="false"/>.</returns>
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
