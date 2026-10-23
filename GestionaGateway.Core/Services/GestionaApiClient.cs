using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using GestionaGateway.Core.Models;
using Microsoft.Extensions.Logging;

namespace GestionaGateway.Core.Services;

public sealed partial class GestionaApiClient : IGestionaApiClient
{
    private const string FilesFilterContentType = "application/vnd.gestiona.filter.files";
    private const string AssigneesFilterContentType = "application/vnd.gestiona.filter.assignees+json";
    private const string ThirdsFilterContentType = "application/vnd.gestiona.filter.thirds+json";
    private const string FileDocumentContentType = "application/vnd.gestiona.file-document+json; version=4";
    private const string FileFolderContentType = "application/vnd.gestiona.file-folder";
    private const string FileOpeningContentType = "application/vnd.gestiona.file-opening+json; version=1";
    private const string QueueMessageContentType = "application/vnd.gestiona.queues.message";
    private const string QueueConnectorResponseContentType = "application/vnd.gestiona.connector-response+json";

    // These route constants are defined here to ensure consistency across the client methods and to make
    // it easier to update if the API routes change in the future
    private const string UploadsRoute = "uploads";
    private const string FilesRoute = "files";
    private const string FileAssigneeUsersRoute = "files/assignees/users";
    private const string FileAssigneeGroupsRoute = "files/assignees/groups";
    private const string ConnectorsRoute = "connectors";
    private const string AddOnAuthorizationsRoute = "addon/authorizations";
    private const string QueueSubscriptionsRoute = "queues/subscriptions";
    private const string ThirdsRoute = "thirds";
    private const string Catalog2015ProceduresRoute = "catalog-2015/procedures";
    private const string DocumentsAndFoldersRoute = "documents-and-folders";
    private const string SelectableTitlesRoute = "selectable-titles";
    private const string ContentSmallDocumentInstancesRoute = "content/small/documentinstances";

    private static readonly JsonSerializerOptions IgnoreNullJsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // The HttpClientFactory is used to create HttpClient instances for making API calls, 
    // which allows for better management of HTTP connections and resources.
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GestionaApiClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GestionaApiClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The factory used to create HTTP clients for Gestiona API calls.</param>
    /// <param name="logger">The logger used for request tracing and diagnostics.</param>
    public GestionaApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<GestionaApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Normalizes the Gestiona API base URL so relative routes can be combined safely.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL to normalize.</param>
    /// <returns>The normalized base URL ending with a trailing slash.</returns>
    private static string NormalizeBaseUrl(string gestionaApiBaseUrl)
    {
        return gestionaApiBaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? gestionaApiBaseUrl
            : $"{gestionaApiBaseUrl}/";
    }

    /// <summary>
    /// Resolves an absolute or relative Gestiona href into an absolute URI.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API, used when <paramref name="href"/> is relative.</param>
    /// <param name="href">The absolute or relative href to resolve.</param>
    /// <returns>The resolved absolute URI.</returns>
    private static Uri ResolveHrefUri(string gestionaApiBaseUrl, string href)
    {
        return Uri.TryCreate(href, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri
            : new Uri(new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute), href);
    }

    /// <summary>
    /// Deserializes a non-empty JSON response body into the requested model type.
    /// </summary>
    /// <typeparam name="T">The response model type.</typeparam>
    /// <param name="responseBody">The raw response body to deserialize.</param>
    /// <param name="methodName">The calling method name used in warning logs.</param>
    /// <returns>The deserialized response model, or the default value when the body is empty or invalid.</returns>
    private T? DeserializeResponse<T>(string responseBody, string methodName)
    {
        if (string.IsNullOrWhiteSpace(responseBody) ||
            responseBody is "<empty>" or "<no content>")
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(responseBody);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "({Method}) failed to parse Gestiona response body.", methodName);
            return default;
        }
    }

    /// <summary>
    /// Builds the Gestiona documents-and-folders route for a file or child folder/document.
    /// </summary>
    /// <param name="fileId">The Gestiona file identifier.</param>
    /// <param name="folderId">The optional folder or document identifier to append to the route.</param>
    /// <returns>The relative documents-and-folders route.</returns>
    private static string BuildDocumentsAndFoldersRoute(string fileId, string? folderId)
    {
        var route = $"{FilesRoute}/{Uri.EscapeDataString(fileId)}/{DocumentsAndFoldersRoute}";
        return string.IsNullOrWhiteSpace(folderId)
            ? route
            : $"{route}/{Uri.EscapeDataString(folderId)}";
    }

    /// <summary>
    /// Extracts third identifiers from the links returned by the Gestiona process third-party response.
    /// </summary>
    /// <param name="thirdPartyLinks">The upstream third-party links response.</param>
    /// <returns>The third identifiers resolved from links whose relation is <c>third</c>.</returns>
    private static IReadOnlyList<string> ExtractThirdIds(ProcessThirdPartyLinksResponse? thirdPartyLinks)
    {
        if (thirdPartyLinks?.Content is null)
        {
            return [];
        }

        var thirdIds = new List<string>();
        foreach (var contentItem in thirdPartyLinks.Content)
        {
            var thirdLink = contentItem.Links?.FirstOrDefault(link =>
                string.Equals(link.Rel, "third", StringComparison.Ordinal));
            var thirdId = GetLastHrefSegment(thirdLink?.Href);

            if (!string.IsNullOrWhiteSpace(thirdId))
            {
                thirdIds.Add(thirdId);
            }
        }

        return thirdIds;
    }

    /// <summary>
    /// Gets the final path segment from an absolute or relative href.
    /// </summary>
    /// <param name="href">The href whose final path segment should be returned.</param>
    /// <returns>The final href segment, or <see langword="null"/> when the href is empty.</returns>
    private static string? GetLastHrefSegment(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var trimmedHref = href.TrimEnd('/');
        var lastSlashIndex = trimmedHref.LastIndexOf('/');
        return lastSlashIndex < 0
            ? trimmedHref
            : trimmedHref[(lastSlashIndex + 1)..];
    }

    /// <summary>
    /// Resolves a third identifier when the third filter response contains exactly one item.
    /// </summary>
    /// <param name="thirdFilter">The upstream third filter response.</param>
    /// <returns>The single third identifier, or <see langword="null"/> when the response does not contain exactly one item.</returns>
    private static string? ResolveSingleThirdId(ThirdFilterResponse? thirdFilter)
    {
        if (thirdFilter?.Content is null ||
            thirdFilter.Content.Count != 1)
        {
            return null;
        }

        return thirdFilter.Content[0].Id;
    }

    private async Task<GestionaApiCallResult<IReadOnlyList<TItem>>> GetPagedContentAsync<TResponse, TItem>(
        HttpClient httpClient,
        string initialRoute,
        string methodName,
        Func<TResponse, IReadOnlyList<TItem>?> getContent,
        Func<TResponse, IReadOnlyList<GestionaLink>?> getLinks,
        CancellationToken cancellationToken)
    {
        var items = new List<TItem>();
        var visitedUris = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentUri = new Uri(httpClient.BaseAddress!, initialRoute);
        var statusCode = (int)System.Net.HttpStatusCode.OK;

        while (true)
        {
            if (!HasSameOrigin(httpClient.BaseAddress!, currentUri))
            {
                _logger.LogWarning(
                    "({Method}) refused cross-origin pagination link {RequestUri}",
                    methodName,
                    currentUri);
                return new GestionaApiCallResult<IReadOnlyList<TItem>>(statusCode, false, []);
            }

            if (!visitedUris.Add(currentUri.AbsoluteUri))
            {
                _logger.LogWarning(
                    "({Method}) detected repeated pagination link {RequestUri}",
                    methodName,
                    currentUri);
                return new GestionaApiCallResult<IReadOnlyList<TItem>>(statusCode, false, []);
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            statusCode = (int)response.StatusCode;
            LogDeprecatedHeader(response, methodName);
            var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

            _logger.LogDebug(
                "({Method}) paged response from {RequestUri}: StatusCode={StatusCode}, Body={Body}",
                methodName,
                currentUri,
                response.StatusCode,
                responseBody);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "({Method}) failed with status code {StatusCode}, Body={Body}",
                    methodName,
                    response.StatusCode,
                    FormatJsonForLog(responseBody));
                return new GestionaApiCallResult<IReadOnlyList<TItem>>(statusCode, false, []);
            }

            var responseModel = DeserializeResponse<TResponse>(responseBody, methodName);
            if (responseModel is null)
            {
                return new GestionaApiCallResult<IReadOnlyList<TItem>>(statusCode, false, []);
            }

            var pageItems = getContent(responseModel);
            if (pageItems is not null)
            {
                items.AddRange(pageItems);
            }

            var nextHref = getLinks(responseModel)?
                .FirstOrDefault(link =>
                    string.Equals(link.Rel, "next", StringComparison.OrdinalIgnoreCase))?
                .Href;

            if (string.IsNullOrWhiteSpace(nextHref))
            {
                return new GestionaApiCallResult<IReadOnlyList<TItem>>(statusCode, true, items);
            }

            if (!Uri.TryCreate(currentUri, nextHref, out var nextUri))
            {
                _logger.LogWarning(
                    "({Method}) received invalid pagination link {NextHref}",
                    methodName,
                    nextHref);
                return new GestionaApiCallResult<IReadOnlyList<TItem>>(statusCode, false, []);
            }

            currentUri = nextUri;
        }
    }

    private static bool HasSameOrigin(Uri baseUri, Uri candidateUri)
    {
        return string.Equals(baseUri.Scheme, candidateUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(baseUri.Host, candidateUri.Host, StringComparison.OrdinalIgnoreCase)
            && baseUri.Port == candidateUri.Port;
    }

    /// <summary>
    /// Reads the HTTP response body and returns a sentinel string when the response has no readable content.
    /// </summary>
    /// <param name="response">The HTTP response whose body must be read.</param>
    /// <param name="cancellationToken">The token used to cancel the read operation.</param>
    /// <returns>The response body, or a sentinel value when the response content is missing or empty.</returns>
    private static async Task<string> ReadResponseBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content is null)
        {
            return "<no content>";
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(body) ? "<empty>" : body;
    }

    private static string? GetResponseBodyMessage(string responseBody)
    {
        return string.IsNullOrWhiteSpace(responseBody) ||
               responseBody is "<empty>" or "<no content>"
            ? null
            : responseBody;
    }

    private static string? GetResponseBodyDescription(string responseBody)
    {
        var responseMessage = GetResponseBodyMessage(responseBody);
        if (responseMessage is null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseMessage);
            return document.RootElement.TryGetProperty("description", out var descriptionElement)
                ? descriptionElement.GetString() ?? responseMessage
                : responseMessage;
        }
        catch (JsonException)
        {
            return responseMessage;
        }
    }

    /// <summary>
    /// Formats a JSON response body for structured logging, falling back to the original text when parsing fails.
    /// </summary>
    /// <param name="body">The response body to format.</param>
    /// <returns>A pretty-printed JSON string when the input is valid JSON; otherwise, the original body.</returns>
    private static string FormatJsonForLog(string body)
    {
        if (string.IsNullOrWhiteSpace(body) ||
            body is "<empty>" or "<no content>")
        {
            return body;
        }

        try
        {
            using var jsonDocument = JsonDocument.Parse(body);
            return JsonSerializer.Serialize(jsonDocument, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return body;
        }
    }

    /// <summary>
    /// Logs the Gestiona deprecation header when present.
    /// </summary>
    /// <param name="response">The HTTP response that may contain the deprecation header.</param>
    /// <param name="methodName">The calling method name used in the log entry.</param>
    private void LogDeprecatedHeader(HttpResponseMessage response, string methodName)
    {
        if (!response.Headers.TryGetValues("X-Gestiona-Deprecated", out var values))
        {
            return;
        }

        var deprecatedValue = string.Join(", ", values);
        _logger.LogInformation(
            "({Method}) received X-Gestiona-Deprecated: {DeprecatedValue}",
            methodName,
            deprecatedValue);
    }

    /// <summary>
    /// Gets the first value of the specified response header.
    /// </summary>
    /// <param name="headers">The response headers to inspect.</param>
    /// <param name="headerName">The header name to read.</param>
    /// <returns>The first header value when present; otherwise, <see langword="null"/>.</returns>
    private static string? GetHeaderValue(HttpResponseHeaders headers, string headerName)
    {
        return headers.TryGetValues(headerName, out var values)
            ? values.FirstOrDefault()
            : null;
    }

    /// <summary>
    /// Gets the first value of the specified response header and parses it as an invariant-culture integer.
    /// </summary>
    /// <param name="headers">The response headers to inspect.</param>
    /// <param name="headerName">The header name to read.</param>
    /// <returns>The parsed integer value when available and valid; otherwise, <see langword="null"/>.</returns>
    private static long? GetLongHeaderValue(HttpResponseHeaders headers, string headerName)
    {
        var value = GetHeaderValue(headers, headerName);
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue)
            ? parsedValue
            : null;
    }

    /// <summary>
    /// Extracts the file name from the content disposition header, preferring the RFC 5987 file name when available.
    /// </summary>
    /// <param name="contentDisposition">The content disposition header to inspect.</param>
    /// <returns>The resolved file name, or <see langword="null"/> when none is present.</returns>
    private static string? GetFileName(ContentDispositionHeaderValue? contentDisposition)
    {
        return contentDisposition?.FileNameStar?.Trim('"') ??
               contentDisposition?.FileName?.Trim('"');
    }
}
