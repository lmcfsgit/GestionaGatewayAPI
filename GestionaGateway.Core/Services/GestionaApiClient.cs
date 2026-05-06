using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GestionaGateway.Core.Models;
using Microsoft.Extensions.Logging;

namespace GestionaGateway.Core.Services;

public sealed class GestionaApiClient : IGestionaApiClient
{
    private const string FilesFilterContentType = "application/vnd.gestiona.filter.files";
    private const string FileDocumentContentType = "application/vnd.gestiona.file-document+json; version=3";

    // These route constants are defined here to ensure consistency across the client methods and to make
    // it easier to update if the API routes change in the future
    private const string UploadsRoute = "uploads";
    private const string FilesRoute = "files";
    private const string DocumentsAndFoldersRoute = "documents-and-folders";

    // The HttpClientFactory is used to create HttpClient instances for making API calls, 
    // which allows for better management of HTTP connections and resources.
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GestionaApiClient> _logger;

    public GestionaApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<GestionaApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // This method creates an upload space in Gestiona and returns the location URL 
    // where the document content can be uploaded.
    public async Task<string?> CreateUploadSpaceAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, UploadsRoute)
        {
            Content = new ByteArrayContent([])
        };
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(FileDocumentContentType);

        _logger.LogInformation(
            "({Method}) sending Gestiona upload request to {RequestUri}",
            nameof(CreateUploadSpaceAsync),
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);
        _logger.LogDebug(
            "({Method}) upload space response: StatusCode={StatusCode}, Body={Body}",
            nameof(CreateUploadSpaceAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var location = response.Headers.Location?.ToString();
        _logger.LogInformation(
            "({Method}) created Gestiona upload space at {Location}",
            nameof(CreateUploadSpaceAsync),
            location);

        return location;
    }

    public async Task<bool> UploadDocumentContentAsync(
        string gestionaApiBaseUrl,
        string uploadLocation,
        string accessToken,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var uploadUri = Uri.TryCreate(uploadLocation, UriKind.Absolute, out var absoluteUploadUri)
            ? absoluteUploadUri
            : new Uri(new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute), uploadLocation);

        using var request = new HttpRequestMessage(HttpMethod.Put, uploadUri)
        {
            Content = new ByteArrayContent(content)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        _logger.LogInformation(
            "({Method}) uploading document content to {RequestUri}",
            nameof(UploadDocumentContentAsync),
            uploadUri);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);
        _logger.LogDebug(
            "({Method}) document upload response: StatusCode={StatusCode}, Body={Body}",
            nameof(UploadDocumentContentAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed with status code {StatusCode}",
                nameof(UploadDocumentContentAsync),
                response.StatusCode);
            return false;
        }

        return true;
    }

    public async Task<string?> GetFileSelfHrefAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string processId,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, FilesRoute)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { exact_code = processId }),
                Encoding.UTF8,
                FilesFilterContentType)
        };

        _logger.LogInformation(
            "({Method}) sending Gestiona files request to {RequestUri}",
            nameof(GetFileSelfHrefAsync),
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);
        _logger.LogDebug(
            "({Method}) files response: StatusCode={StatusCode}, Body={Body}",
            nameof(GetFileSelfHrefAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("content", out var contentElement) ||
                contentElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var contentItem in contentElement.EnumerateArray())
            {
                if (!contentItem.TryGetProperty("links", out var linksElement) ||
                    linksElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var link in linksElement.EnumerateArray())
                {
                    if (!link.TryGetProperty("rel", out var relElement) ||
                        !string.Equals(relElement.GetString(), "self", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!link.TryGetProperty("href", out var hrefElement))
                    {
                        continue;
                    }

                    var href = hrefElement.GetString();
                    _logger.LogInformation(
                        "({Method}) resolved Gestiona file self href: {Href}",
                        nameof(GetFileSelfHrefAsync),
                        href);
                    return href;
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "({Method}) failed to parse Gestiona files response body.", nameof(GetFileSelfHrefAsync));
            return null;
        }

        return null;
    }

    // This method is needed because some operations (like creating documents) require the file ID, not just the self href
    public async Task<string?> GetFileIdFromProcessCode(
        string gestionaApiBaseUrl,
        string accessToken,
        string processId,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, FilesRoute)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { exact_code = processId }),
                Encoding.UTF8,
                FilesFilterContentType)
        };

        _logger.LogInformation(
            "({Method}) sending Gestiona files request to {RequestUri}",
            nameof(GetFileIdFromProcessCode),
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);
        _logger.LogDebug(
            "({Method}) files response for file id extraction: StatusCode={StatusCode}, Body={Body}",
            nameof(GetFileIdFromProcessCode),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("content", out var contentElement) ||
                contentElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var contentItem in contentElement.EnumerateArray())
            {
                if (!contentItem.TryGetProperty("id", out var idElement))
                {
                    continue;
                }

                var id = idElement.GetString();
                _logger.LogInformation(
                    "({Method}) resolved Gestiona file id: {FileId}",
                    nameof(GetFileIdFromProcessCode),
                    id);
                return id;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "({Method}) failed to parse Gestiona files response body for file id extraction.", nameof(GetFileIdFromProcessCode));
            return null;
        }

        return null;
    }

    public async Task<CreateDocumentAndFolderResponse?> CreateDocumentAndFolderAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string fileId,
        CreateDocumentInFileRequest request,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = $"{FilesRoute}/{Uri.EscapeDataString(fileId)}/{DocumentsAndFoldersRoute}";
        var payload = new
        {
            name = request.Name,
            type = request.Type,
            metadata_language = request.MetadataLanguage,
            trashed = request.Trashed,
            version = request.Version,
            links = new[]
            {
                new
                {
                    rel = "content",
                    href = request.ContentHref
                }
            }
        };
        var serializedPayload = JsonSerializer.Serialize(payload);

        _logger.LogDebug(
            "(({Method})) documents-and-folders request: Route={Route}, Payload={Payload}",
            nameof(CreateDocumentAndFolderAsync),
            route,
            serializedPayload);

        var requestContent = new StringContent(serializedPayload, Encoding.UTF8);
        requestContent.Headers.ContentType = MediaTypeHeaderValue.Parse(FileDocumentContentType);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = requestContent
        };

        _logger.LogInformation(
            "({Method}) creating Gestiona document in file {FileId} via {RequestUri}",
            nameof(CreateDocumentAndFolderAsync),
            fileId,
            new Uri(httpClient.BaseAddress, httpRequest.RequestUri!));

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);
        CreateDocumentAndFolderResponse? responseModel = null;
        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            responseModel = JsonSerializer.Deserialize<CreateDocumentAndFolderResponse>(responseBody);
        }

        _logger.LogDebug(
            "(({Method})) documents-and-folders response: StatusCode={StatusCode}, Body={Body}",
            nameof(CreateDocumentAndFolderAsync),
            response.StatusCode,
            responseBody);

        if (responseModel is not null)
        {
            _logger.LogDebug(
                "(({Method})) documents-and-folders response model:{NewLine}{ResponseModel}",
                nameof(CreateDocumentAndFolderAsync),
                Environment.NewLine,
                JsonSerializer.Serialize(responseModel, new JsonSerializerOptions { WriteIndented = true }));
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed for file {FileId} with status code {StatusCode}",
                nameof(CreateDocumentAndFolderAsync),
                fileId,
                response.StatusCode);
            return null;
        }

        return responseModel;
    }

    public async Task<CreateDocumentAndFolderResponse?> CreateDocumentUrlAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string fileId,
        CreateDocumentInFileRequest request,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = $"{FilesRoute}/{Uri.EscapeDataString(fileId)}/{DocumentsAndFoldersRoute}";
        var payload = new
        {
            name = request.Name,
            type = request.Type,
            metadata_language = request.MetadataLanguage,
            trashed = request.Trashed,
            version = request.Version,
            external_url = request.ExternalUrl

        };
        var serializedPayload = JsonSerializer.Serialize(payload);

        _logger.LogDebug(
            "({Method}) documents-and-folders request: Route={Route}, Payload={Payload}",
            nameof(CreateDocumentUrlAsync),
            route,
            serializedPayload);

        var requestContent = new StringContent(serializedPayload, Encoding.UTF8);
        requestContent.Headers.ContentType = MediaTypeHeaderValue.Parse(FileDocumentContentType);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = requestContent
        };

        _logger.LogInformation(
            "({Method}) creating Gestiona document in file {FileId} via {RequestUri}",
            nameof(CreateDocumentUrlAsync),
            fileId,
            new Uri(httpClient.BaseAddress, httpRequest.RequestUri!));

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);
        CreateDocumentAndFolderResponse? responseModel = null;
        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            responseModel = JsonSerializer.Deserialize<CreateDocumentAndFolderResponse>(responseBody);
        }

        _logger.LogDebug(
            "({Method}) documents-and-folders (url) response: StatusCode={StatusCode}, Body={Body}",
            nameof(CreateDocumentUrlAsync),
            response.StatusCode,
            responseBody);
        if (responseModel is not null)
        {
            _logger.LogInformation(
                "({Method}) documents-and-folders (url) response model:{NewLine}{ResponseModel}",
                nameof(CreateDocumentUrlAsync),
                Environment.NewLine,
                JsonSerializer.Serialize(responseModel, new JsonSerializerOptions { WriteIndented = true }));
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed for file {FileId} with status code {StatusCode}",
                nameof(CreateDocumentUrlAsync),
                fileId,
                response.StatusCode);
            return null;
        }

        return responseModel;
    }

    // This method is needed to ensure the base URL always ends with a slash, as expected by the API client code
    // (without this, some operations that combine the base URL with relative paths may produce incorrect URLs)
    private static string NormalizeBaseUrl(string gestionaApiBaseUrl)
    {
        return gestionaApiBaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? gestionaApiBaseUrl
            : $"{gestionaApiBaseUrl}/";
    }

    // This method is needed to ensure that response bodies are read and logged correctly, 
    // even in error scenarios where the body may contain useful information about the failure
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
}
