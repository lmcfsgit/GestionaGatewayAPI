using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GestionaGatewayAPI.Models;

namespace GestionaGatewayAPI.Services;

public sealed class GestionaApiClient : IGestionaApiClient
{
    private const string FilesFilterContentType = "application/vnd.gestiona.filter.files";
    private const string FileDocumentContentType = "application/vnd.gestiona.file-document+json; version=3";
    private const string UploadsRoute = "uploads";
    private const string FilesRoute = "files";
    private const string DocumentsAndFoldersRoute = "documents-and-folders";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GestionaApiClient> _logger;

    public GestionaApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<GestionaApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

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

        _logger.LogInformation("Sending Gestiona upload request to {RequestUri}", new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);
        _logger.LogDebug(
            "Gestiona upload space response: StatusCode={StatusCode}, Body={Body}",
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var location = response.Headers.Location?.ToString();
        _logger.LogInformation("Gestiona upload space created at {Location}", location);

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

        _logger.LogInformation("Uploading document content to {RequestUri}", uploadUri);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);
        _logger.LogDebug(
            "Gestiona document upload response: StatusCode={StatusCode}, Body={Body}",
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Gestiona document upload failed with status code {StatusCode}", response.StatusCode);
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

        _logger.LogInformation("Sending Gestiona files request to {RequestUri}", new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);
        _logger.LogDebug(
            "Gestiona files response: StatusCode={StatusCode}, Body={Body}",
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
                    _logger.LogInformation("Gestiona file self href: {Href}", href);
                    return href;
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Gestiona files response body.");
            return null;
        }

        return null;
    }

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

        _logger.LogInformation("Sending Gestiona files request to {RequestUri}", new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);
        _logger.LogDebug(
            "Gestiona files response for file id extraction: StatusCode={StatusCode}, Body={Body}",
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
                _logger.LogInformation("Gestiona file id: {FileId}", id);
                return id;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Gestiona files response body for file id extraction.");
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
            "Gestiona documents-and-folders request: Route={Route}, Payload={Payload}",
            route,
            serializedPayload);

        var requestContent = new StringContent(serializedPayload, Encoding.UTF8);
        requestContent.Headers.ContentType = MediaTypeHeaderValue.Parse(FileDocumentContentType);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = requestContent
        };

        _logger.LogInformation(
            "Creating Gestiona document in file {FileId} via {RequestUri}",
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
            "Gestiona documents-and-folders response: StatusCode={StatusCode}, Body={Body}",
            response.StatusCode,
            responseBody);
        if (responseModel is not null)
        {
            _logger.LogInformation(
                "Gestiona documents-and-folders response model:{NewLine}{ResponseModel}",
                Environment.NewLine,
                JsonSerializer.Serialize(responseModel, new JsonSerializerOptions { WriteIndented = true }));
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Gestiona document creation failed for file {FileId} with status code {StatusCode}",
                fileId,
                response.StatusCode);
            return null;
        }

        return responseModel;
    }

    private static string NormalizeBaseUrl(string gestionaApiBaseUrl)
    {
        return gestionaApiBaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? gestionaApiBaseUrl
            : $"{gestionaApiBaseUrl}/";
    }

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
