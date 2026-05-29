using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Globalization;
using GestionaGateway.Core.Models;
using Microsoft.Extensions.Logging;

namespace GestionaGateway.Core.Services;

public sealed class GestionaApiClient : IGestionaApiClient
{
    private const string FilesFilterContentType = "application/vnd.gestiona.filter.files";
    private const string ThirdsFilterContentType = "application/vnd.gestiona.filter.thirds+json";
    private const string FileDocumentContentType = "application/vnd.gestiona.file-document+json; version=4";
    private const string FileFolderContentType = "application/vnd.gestiona.file-folder";

    // These route constants are defined here to ensure consistency across the client methods and to make
    // it easier to update if the API routes change in the future
    private const string UploadsRoute = "uploads";
    private const string FilesRoute = "files";
    private const string ThirdsRoute = "thirds";
    private const string DocumentsAndFoldersRoute = "documents-and-folders";
    private const string ContentSmallDocumentInstancesRoute = "content/small/documentinstances";

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
    /// Creates a temporary upload space in Gestiona and returns the location where document content must be uploaded.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the upload location when the operation succeeds.</returns>
    public async Task<GestionaApiCallResult<string?>> CreateUploadSpaceAsync(
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
        LogDeprecatedFileDocumentVersionHeader(response, nameof(CreateUploadSpaceAsync));
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);
        _logger.LogDebug(
            "({Method}) upload space response: StatusCode={StatusCode}, Body={Body}",
            nameof(CreateUploadSpaceAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed with status code {StatusCode}, Body={Body}",
                nameof(CreateUploadSpaceAsync),
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<string?>((int)response.StatusCode, false, null);
        }

        var location = response.Headers.Location?.ToString();
        _logger.LogInformation(
            "({Method}) created Gestiona upload space at {Location}",
            nameof(CreateUploadSpaceAsync),
            location);

        return new GestionaApiCallResult<string?>((int)response.StatusCode, true, location);
    }

    /// <summary>
    /// Uploads binary document content to a previously created Gestiona upload location.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API, used when the upload location is relative.</param>
    /// <param name="uploadLocation">The absolute or relative upload location returned by Gestiona.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="content">The binary document content to upload.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result that indicates whether the upload completed successfully.</returns>
    public async Task<GestionaApiCallResult> UploadDocumentContentAsync(
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
                "({Method}) failed with status code {StatusCode}, Body={Body}",
                nameof(UploadDocumentContentAsync),
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult((int)response.StatusCode, false);
        }

        return new GestionaApiCallResult((int)response.StatusCode, true);
    }

    /// <summary>
    /// Resolves the self link of the Gestiona file associated with the provided process code.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="processId">The process code used to search for the Gestiona file.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the file self href when it is found.</returns>
    public async Task<GestionaApiCallResult<string?>> GetFileSelfHrefAsync(
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
            return new GestionaApiCallResult<string?>((int)response.StatusCode, false, null);
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("content", out var contentElement) ||
                contentElement.ValueKind != JsonValueKind.Array)
            {
                return new GestionaApiCallResult<string?>((int)response.StatusCode, true, null);
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
                    return new GestionaApiCallResult<string?>((int)response.StatusCode, true, href);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "({Method}) failed to parse Gestiona files response body.", nameof(GetFileSelfHrefAsync));
            return new GestionaApiCallResult<string?>((int)response.StatusCode, false, null);
        }

        return new GestionaApiCallResult<string?>((int)response.StatusCode, true, null);
    }

    /// <summary>
    /// Resolves the Gestiona file identifier associated with the provided process code.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="processId">The process code used to search for the Gestiona file.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the Gestiona file identifier when it is found.</returns>
    public async Task<GestionaApiCallResult<string?>> GetFileIdFromProcessCode(
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

        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            _logger.LogInformation(
                "({Method}) no Gestiona file was found for process code {ProcessId}",
                nameof(GetFileIdFromProcessCode),
                processId);
            return new GestionaApiCallResult<string?>((int)response.StatusCode, false, null);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("({Method}) Gestiona files request failed: StatusCode={StatusCode}, Body={Body}",
            nameof(GetFileIdFromProcessCode), response.StatusCode, FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<string?>((int)response.StatusCode, false, null);
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("content", out var contentElement) ||
                contentElement.ValueKind != JsonValueKind.Array)
            {
                return new GestionaApiCallResult<string?>((int)response.StatusCode, true, null);
            }

            using var contentEnumerator = contentElement.EnumerateArray();
            if (!contentEnumerator.MoveNext())
            {
                return new GestionaApiCallResult<string?>((int)response.StatusCode, true, null);
            }

            var contentItem = contentEnumerator.Current;
            if (!contentItem.TryGetProperty("id", out var idElement))
            {
                return new GestionaApiCallResult<string?>((int)response.StatusCode, true, null);
            }

            var id = idElement.GetString();
            _logger.LogInformation(
                "({Method}) resolved Gestiona file id: {FileId}",
                nameof(GetFileIdFromProcessCode),
                id);
            return new GestionaApiCallResult<string?>((int)response.StatusCode, true, id);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "({Method}) failed to parse Gestiona files response body for file id extraction.", nameof(GetFileIdFromProcessCode));
            return new GestionaApiCallResult<string?>((int)response.StatusCode, false, null);
        }

    }

    /// <summary>
    /// Creates a digital document in the specified Gestiona file using previously uploaded content.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="fileId">The Gestiona file identifier that will receive the new document.</param>
    /// <param name="request">The document creation payload, including metadata and the uploaded content link.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the created document payload when available.</returns>
    public async Task<GestionaApiCallResult<CreateDocumentAndFolderResponse?>> CreateDocumentAndFolderAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string fileId,
        string? folderId,
        CreateDocumentInFileRequest request,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = BuildDocumentsAndFoldersRoute(fileId, folderId);
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


        // The response from this endpoint may contain useful information in the body even in error scenarios 
        // (e.g. when the file ID is not found), so we read and log the body regardless of the status code
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

        LogDeprecatedFileDocumentVersionHeader(response, nameof(CreateDocumentAndFolderAsync));

        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);
        _logger.LogDebug(
            "(({Method})) documents-and-folders response: StatusCode={StatusCode}, Body={Body}",
            nameof(CreateDocumentAndFolderAsync),
            response.StatusCode,
            responseBody);

        CreateDocumentAndFolderResponse? responseModel = null;
        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            responseModel = JsonSerializer.Deserialize<CreateDocumentAndFolderResponse>(responseBody);
        }

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
                "({Method}) failed for file {FileId} with status code {StatusCode}, Body={Body}",
                nameof(CreateDocumentAndFolderAsync),
                fileId,
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<CreateDocumentAndFolderResponse?>((int)response.StatusCode, false, responseModel);
        }

        return new GestionaApiCallResult<CreateDocumentAndFolderResponse?>((int)response.StatusCode, true, responseModel);
    }

    /// <summary>
    /// Downloads the content and metadata for a Gestiona document.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="documentId">The identifier of the document to download.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the downloaded document when the request succeeds.</returns>
    public async Task<GestionaApiCallResult<DownloadedDocument?>> DownloadDocumentAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string documentId,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = $"{ContentSmallDocumentInstancesRoute}/{Uri.EscapeDataString(documentId)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, route);

        _logger.LogInformation(
            "({Method}) downloading Gestiona document {DocumentId} via {RequestUri}",
            nameof(DownloadDocumentAsync),
            documentId,
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await ReadResponseBodyAsync(response, cancellationToken);
            _logger.LogWarning(
                "({Method}) failed for document {DocumentId} with status code {StatusCode}, Body={Body}",
                nameof(DownloadDocumentAsync),
                documentId,
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<DownloadedDocument?>((int)response.StatusCode, false, null);
        }

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var downloadedDocument = new DownloadedDocument(
            documentId,
            GetFileName(response.Content.Headers.ContentDisposition),
            response.Content.Headers.ContentType?.MediaType,
            GetLongHeaderValue(response.Headers, "X-Gestiona-Storage-Size"),
            GetHeaderValue(response.Headers, "X-Gestiona-Storage-Extension"),
            GetHeaderValue(response.Headers, "X-Gestiona-Storage-MIME-Type"),
            GetHeaderValue(response.Headers, "X-Gestiona-Storage-MD5"),
            GetHeaderValue(response.Headers, "X-Gestiona-Storage-SHA1"),
            GetHeaderValue(response.Headers, "X-Gestiona-Storage-SHA512"),
            content);

        _logger.LogInformation(
            "({Method}) downloaded Gestiona document {DocumentId}. ContentType={ContentType}, FileName={FileName}, ContentLength={ContentLength}",
            nameof(DownloadDocumentAsync),
            documentId,
            downloadedDocument.ContentType,
            downloadedDocument.FileName,
            downloadedDocument.Content.Length);

        return new GestionaApiCallResult<DownloadedDocument?>((int)response.StatusCode, true, downloadedDocument);
    }

    /// <summary>
    /// Gets the third identifiers associated with a Gestiona file.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="processId">The Gestiona file identifier whose third parties should be retrieved.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the third identifiers extracted from the third-party links.</returns>
    public async Task<GestionaApiCallResult<IReadOnlyList<string>>> GetProcessThirdIdsAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string processId,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = $"{FilesRoute}/{Uri.EscapeDataString(processId)}/thirdparties";
        using var request = new HttpRequestMessage(HttpMethod.Get, route);

        _logger.LogInformation(
            "({Method}) getting Gestiona third parties for process {ProcessId} via {RequestUri}",
            nameof(GetProcessThirdIdsAsync),
            processId,
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) process third parties response: StatusCode={StatusCode}, Body={Body}",
            nameof(GetProcessThirdIdsAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed for process {ProcessId} with status code {StatusCode}, Body={Body}",
                nameof(GetProcessThirdIdsAsync),
                processId,
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<IReadOnlyList<string>>((int)response.StatusCode, false, []);
        }

        if (string.IsNullOrWhiteSpace(responseBody) ||
            responseBody is "<empty>" or "<no content>")
        {
            return new GestionaApiCallResult<IReadOnlyList<string>>((int)response.StatusCode, true, []);
        }

        try
        {
            var thirdPartyLinks = JsonSerializer.Deserialize<ProcessThirdPartyLinksResponse>(responseBody);
            var thirdIds = ExtractThirdIds(thirdPartyLinks);
            return new GestionaApiCallResult<IReadOnlyList<string>>((int)response.StatusCode, true, thirdIds);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "({Method}) failed to parse Gestiona process third parties response body.", nameof(GetProcessThirdIdsAsync));
            return new GestionaApiCallResult<IReadOnlyList<string>>((int)response.StatusCode, false, []);
        }
    }

    /// <summary>
    /// Gets a third from Gestiona.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="thirdId">The identifier of the third to retrieve.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the third when the request succeeds.</returns>
    public async Task<GestionaApiCallResult<Third?>> GetThirdAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string thirdId,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = $"{ThirdsRoute}/{Uri.EscapeDataString(thirdId)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, route);

        _logger.LogInformation(
            "({Method}) getting Gestiona third {ThirdId} via {RequestUri}",
            nameof(GetThirdAsync),
            thirdId,
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) third response: StatusCode={StatusCode}, Body={Body}",
            nameof(GetThirdAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed for third {ThirdId} with status code {StatusCode}, Body={Body}",
                nameof(GetThirdAsync),
                thirdId,
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<Third?>((int)response.StatusCode, false, null);
        }

        if (string.IsNullOrWhiteSpace(responseBody) ||
            responseBody is "<empty>" or "<no content>")
        {
            return new GestionaApiCallResult<Third?>((int)response.StatusCode, true, null);
        }

        try
        {
            var third = JsonSerializer.Deserialize<Third>(responseBody);
            return new GestionaApiCallResult<Third?>((int)response.StatusCode, true, third);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "({Method}) failed to parse Gestiona third response body.", nameof(GetThirdAsync));
            return new GestionaApiCallResult<Third?>((int)response.StatusCode, false, null);
        }
    }

    /// <summary>
    /// Resolves a Gestiona third identifier from a NIF.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="nif">The NIF used to filter thirds.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the resolved third identifier when found.</returns>
    public async Task<GestionaApiCallResult<string?>> GetThirdIdByNifAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string nif,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, ThirdsRoute)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { nif }),
                Encoding.UTF8)
        };
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(ThirdsFilterContentType);

        _logger.LogInformation(
            "({Method}) resolving Gestiona third id by NIF via {RequestUri}",
            nameof(GetThirdIdByNifAsync),
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) third filter response: StatusCode={StatusCode}, Body={Body}",
            nameof(GetThirdIdByNifAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed with status code {StatusCode}, Body={Body}",
                nameof(GetThirdIdByNifAsync),
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<string?>((int)response.StatusCode, false, null);
        }

        if (string.IsNullOrWhiteSpace(responseBody) ||
            responseBody is "<empty>" or "<no content>")
        {
            return new GestionaApiCallResult<string?>((int)response.StatusCode, true, null);
        }

        try
        {
            var thirdFilter = JsonSerializer.Deserialize<ThirdFilterResponse>(responseBody);
            var thirdId = ResolveSingleThirdId(thirdFilter);
            return new GestionaApiCallResult<string?>((int)response.StatusCode, true, thirdId);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "({Method}) failed to parse Gestiona third filter response body.", nameof(GetThirdIdByNifAsync));
            return new GestionaApiCallResult<string?>((int)response.StatusCode, false, null);
        }
    }

    /// <summary>
    /// Gets the default address for a third from Gestiona.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="thirdId">The identifier of the third whose default address should be retrieved.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the third default address when the request succeeds.</returns>
    public async Task<GestionaApiCallResult<ThirdDefaultAddress?>> GetThirdDefaultAddressAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string thirdId,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = $"{ThirdsRoute}/{Uri.EscapeDataString(thirdId)}/default-address";
        using var request = new HttpRequestMessage(HttpMethod.Get, route);

        _logger.LogInformation(
            "({Method}) getting Gestiona third default address {ThirdId} via {RequestUri}",
            nameof(GetThirdDefaultAddressAsync),
            thirdId,
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) third default address response: StatusCode={StatusCode}, Body={Body}",
            nameof(GetThirdDefaultAddressAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed for third {ThirdId} with status code {StatusCode}, Body={Body}",
                nameof(GetThirdDefaultAddressAsync),
                thirdId,
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<ThirdDefaultAddress?>((int)response.StatusCode, false, null);
        }

        if (string.IsNullOrWhiteSpace(responseBody) ||
            responseBody is "<empty>" or "<no content>")
        {
            return new GestionaApiCallResult<ThirdDefaultAddress?>((int)response.StatusCode, true, null);
        }

        try
        {
            var address = JsonSerializer.Deserialize<ThirdDefaultAddress>(responseBody);
            return new GestionaApiCallResult<ThirdDefaultAddress?>((int)response.StatusCode, true, address);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "({Method}) failed to parse Gestiona third default address response body.", nameof(GetThirdDefaultAddressAsync));
            return new GestionaApiCallResult<ThirdDefaultAddress?>((int)response.StatusCode, false, null);
        }
    }

    /// <summary>
    /// Creates an external URL document in the specified Gestiona file.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="fileId">The Gestiona file identifier that will receive the new document.</param>
    /// <param name="request">The document creation payload, including metadata and the external URL.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the created document payload when available.</returns>
    public async Task<GestionaApiCallResult<CreateDocumentAndFolderResponse?>> CreateDocumentUrlAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string fileId,
        string? folderId,
        CreateDocumentInFileRequest request,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = BuildDocumentsAndFoldersRoute(fileId, folderId);
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
        LogDeprecatedFileDocumentVersionHeader(response, nameof(CreateDocumentUrlAsync));
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
                "({Method}) failed for file {FileId} with status code {StatusCode}, Body={Body}",
                nameof(CreateDocumentUrlAsync),
                fileId,
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<CreateDocumentAndFolderResponse?>((int)response.StatusCode, false, responseModel);
        }

        return new GestionaApiCallResult<CreateDocumentAndFolderResponse?>((int)response.StatusCode, true, responseModel);
    }

    /// <summary>
    /// Creates a folder in the specified Gestiona file.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="fileId">The Gestiona file identifier that will receive the new folder.</param>
    /// <param name="request">The folder creation payload, including the folder name and line.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the created folder payload when available.</returns>
    public async Task<GestionaApiCallResult<CreateDocumentAndFolderResponse?>> CreateFolderAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string fileId,
        string? folderId,
        CreateDocumentInFileRequest request,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = BuildDocumentsAndFoldersRoute(fileId, folderId);
        var payload = new
        {
            name = request.Name,
            line = request.Line
        };
        var serializedPayload = JsonSerializer.Serialize(payload);

        _logger.LogDebug(
            "({Method}) documents-and-folders (folder) request: Route={Route}, Payload={Payload}",
            nameof(CreateFolderAsync),
            route,
            serializedPayload);

        var requestContent = new StringContent(serializedPayload, Encoding.UTF8);
        requestContent.Headers.ContentType = MediaTypeHeaderValue.Parse(FileFolderContentType);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = requestContent
        };

        _logger.LogInformation(
            "({Method}) creating Gestiona folder in file {FileId} via {RequestUri}",
            nameof(CreateFolderAsync),
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
            "({Method}) documents-and-folders (folder) response: StatusCode={StatusCode}, Body={Body}",
            nameof(CreateFolderAsync),
            response.StatusCode,
            responseBody);

        if (responseModel is not null)
        {
            _logger.LogDebug(
                "({Method}) documents-and-folders (folder) response model:{NewLine}{ResponseModel}",
                nameof(CreateFolderAsync),
                Environment.NewLine,
                JsonSerializer.Serialize(responseModel, new JsonSerializerOptions { WriteIndented = true }));
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed for file {FileId} with status code {StatusCode}, Body={Body}",
                nameof(CreateFolderAsync),
                fileId,
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<CreateDocumentAndFolderResponse?>((int)response.StatusCode, false, responseModel);
        }

        return new GestionaApiCallResult<CreateDocumentAndFolderResponse?>((int)response.StatusCode, true, responseModel);
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

    private static string BuildDocumentsAndFoldersRoute(string fileId, string? folderId)
    {
        var route = $"{FilesRoute}/{Uri.EscapeDataString(fileId)}/{DocumentsAndFoldersRoute}";
        return string.IsNullOrWhiteSpace(folderId)
            ? route
            : $"{route}/{Uri.EscapeDataString(folderId)}";
    }

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

    private static string? ResolveSingleThirdId(ThirdFilterResponse? thirdFilter)
    {
        if (thirdFilter?.Content is null ||
            thirdFilter.Content.Count != 1)
        {
            return null;
        }

        return thirdFilter.Content[0].Id;
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
    /// Logs the Gestiona deprecation header emitted for the file-document media type when present.
    /// </summary>
    /// <param name="response">The HTTP response that may contain the deprecation header.</param>
    /// <param name="methodName">The calling method name used in the log entry.</param>
    private void LogDeprecatedFileDocumentVersionHeader(HttpResponseMessage response, string methodName)
    {
        if (!response.Headers.TryGetValues("X-Gestiona-Deprecated", out var values))
        {
            return;
        }

        var deprecatedValue = string.Join(", ", values);
        _logger.LogInformation(
            "({Method}) received X-Gestiona-Deprecated for content type {ContentType}: {DeprecatedValue}",
            methodName,
            FileDocumentContentType,
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
