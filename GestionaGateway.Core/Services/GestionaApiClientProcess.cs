using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using GestionaGateway.Core.Models;
using Microsoft.Extensions.Logging;

namespace GestionaGateway.Core.Services;

public sealed partial class GestionaApiClient
{
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

        LogDeprecatedHeader(response, nameof(CreateUploadSpaceAsync));
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
        LogDeprecatedHeader(response, nameof(UploadDocumentContentAsync));
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

        var serializedPayload = JsonSerializer.Serialize(new { exact_code = processId });
        _logger.LogDebug(
            "({Method}) Gestiona request body:{NewLine}{RequestBody}",
            nameof(GetFileSelfHrefAsync),
            Environment.NewLine,
            FormatJsonForLog(serializedPayload));

        using var request = new HttpRequestMessage(HttpMethod.Get, FilesRoute)
        {
            Content = new StringContent(
                serializedPayload,
                Encoding.UTF8,
                FilesFilterContentType)
        };

        _logger.LogInformation(
            "({Method}) sending Gestiona files request to {RequestUri}",
            nameof(GetFileSelfHrefAsync),
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        LogDeprecatedHeader(response, nameof(GetFileSelfHrefAsync));
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

        var serializedPayload = JsonSerializer.Serialize(new { exact_code = processId });
        _logger.LogDebug(
            "({Method}) Gestiona request body:{NewLine}{RequestBody}",
            nameof(GetFileIdFromProcessCode),
            Environment.NewLine,
            FormatJsonForLog(serializedPayload));

        using var request = new HttpRequestMessage(HttpMethod.Get, FilesRoute)
        {
            Content = new StringContent(
                serializedPayload,
                Encoding.UTF8,
                FilesFilterContentType)
        };

        _logger.LogInformation(
            "({Method}) sending Gestiona files request to {RequestUri}",
            nameof(GetFileIdFromProcessCode),
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        LogDeprecatedHeader(response, nameof(GetFileIdFromProcessCode));
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
    /// Creates a Gestiona process file for the specified catalog activity and external procedure.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="activityId">The Gestiona catalog activity identifier.</param>
    /// <param name="procedureId">The Gestiona external procedure identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the create-file response when available.</returns>
    public async Task<GestionaApiCallResult<CreateProcessFileResponse?>> CreateProcessFileAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string activityId,
        string procedureId,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = $"{Catalog2015ProceduresRoute}/{Uri.EscapeDataString(activityId)}/external-procedures/{Uri.EscapeDataString(procedureId)}/create-file";
        using var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = new ByteArrayContent([])
        };

        _logger.LogInformation(
            "({Method}) creating Gestiona process file via {RequestUri}",
            nameof(CreateProcessFileAsync),
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);

        LogDeprecatedHeader(response, nameof(CreateProcessFileAsync));
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) create-file response: StatusCode={StatusCode}, Body={Body}",
            nameof(CreateProcessFileAsync),
            response.StatusCode,
            responseBody);

        var responseModel = DeserializeResponse<CreateProcessFileResponse>(
            responseBody,
            nameof(CreateProcessFileAsync));

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed with status code {StatusCode}, Body={Body}",
                nameof(CreateProcessFileAsync),
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<CreateProcessFileResponse?>((int)response.StatusCode, false, responseModel);
        }

        return new GestionaApiCallResult<CreateProcessFileResponse?>((int)response.StatusCode, true, responseModel);
    }

    /// <summary>
    /// Gets the selectable titles available for opening a Gestiona process file.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="processId">The Gestiona process file identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the selectable-title response when the request succeeds.</returns>
    public async Task<GestionaApiCallResult<SelectableTitlesResponse?>> GetSelectableTitlesAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string processId,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = $"{FilesRoute}/{Uri.EscapeDataString(processId)}/{SelectableTitlesRoute}";
        using var request = new HttpRequestMessage(HttpMethod.Get, route);

        _logger.LogInformation(
            "({Method}) getting Gestiona selectable titles for process {ProcessId} via {RequestUri}",
            nameof(GetSelectableTitlesAsync),
            processId,
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        LogDeprecatedHeader(response, nameof(GetSelectableTitlesAsync));
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) selectable-titles response: StatusCode={StatusCode}, Body={Body}",
            nameof(GetSelectableTitlesAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed with status code {StatusCode}, Body={Body}",
                nameof(GetSelectableTitlesAsync),
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<SelectableTitlesResponse?>((int)response.StatusCode, false, null);
        }

        var responseModel = DeserializeResponse<SelectableTitlesResponse>(
            responseBody,
            nameof(GetSelectableTitlesAsync));

        return new GestionaApiCallResult<SelectableTitlesResponse?>((int)response.StatusCode, true, responseModel);
    }

    /// <summary>
    /// Opens a previously created Gestiona process file using the file-open link returned by Gestiona.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API, used when the file-open link is relative.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="fileOpenHref">The absolute or relative file-open link returned by the create-file response.</param>
    /// <param name="request">The file-opening payload containing entry date, title, optional selectable title, assignee user, and group.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the opened process file response when the request succeeds.</returns>
    public async Task<GestionaApiCallResult<OpenProcessFileResponse?>> OpenProcessFileAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string fileOpenHref,
        OpenProcessFileRequest request,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var fileOpenUri = ResolveHrefUri(gestionaApiBaseUrl, fileOpenHref);
        var payload = new Dictionary<string, object?>
        {
            ["entry_date"] = request.EntryDate,
            ["free_title"] = request.FreeTitle,
            ["initial_assignation"] = new[]
            {
                new
                {
                    rel = "user",
                    href = request.UserHref
                }
            },
            ["links"] = new[]
            {
                new
                {
                    rel = "management-unit-group",
                    href = request.GroupHref
                }
            }
        };
        if (!string.IsNullOrWhiteSpace(request.SelectableTitle))
        {
            payload["selectable_title"] = request.SelectableTitle;
        }

        var serializedPayload = JsonSerializer.Serialize(payload);
        var requestContent = new StringContent(serializedPayload, Encoding.UTF8);
        requestContent.Headers.ContentType = MediaTypeHeaderValue.Parse(FileOpeningContentType);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, fileOpenUri)
        {
            Content = requestContent
        };

        _logger.LogInformation(
            "({Method}) opening Gestiona process file via {RequestUri}",
            nameof(OpenProcessFileAsync),
            fileOpenUri);
        _logger.LogDebug(
            "({Method}) Gestiona request body:{NewLine}{RequestBody}",
            nameof(OpenProcessFileAsync),
            Environment.NewLine,
            FormatJsonForLog(serializedPayload));

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

        LogDeprecatedHeader(response, nameof(OpenProcessFileAsync));
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) file-open response: StatusCode={StatusCode}, Body={Body}",
            nameof(OpenProcessFileAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed with status code {StatusCode}, Body={Body}",
                nameof(OpenProcessFileAsync),
                response.StatusCode,
                FormatJsonForLog(responseBody));
            var errorResponse = DeserializeResponse<OpenProcessFileResponse>(
                responseBody,
                nameof(OpenProcessFileAsync));
            return new GestionaApiCallResult<OpenProcessFileResponse?>((int)response.StatusCode, false, errorResponse);
        }

        var responseModel = DeserializeResponse<OpenProcessFileResponse>(
            responseBody,
            nameof(OpenProcessFileAsync));

        return new GestionaApiCallResult<OpenProcessFileResponse?>((int)response.StatusCode, true, responseModel);
    }

    public async Task<GestionaApiCallResult> RelateFilesAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string fileId,
        RelatedFilesRequest request,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = $"{FilesRoute}/{Uri.EscapeDataString(fileId)}/related-files";
        var serializedPayload = JsonSerializer.Serialize(request);
        var requestContent = new StringContent(serializedPayload, Encoding.UTF8);
        requestContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/vnd.gestiona.links+json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = requestContent
        };

        _logger.LogInformation(
            "({Method}) relating Gestiona file {FileId} via {RequestUri}",
            nameof(RelateFilesAsync),
            fileId,
            new Uri(httpClient.BaseAddress, httpRequest.RequestUri!));
        _logger.LogDebug(
            "({Method}) Gestiona request body:{NewLine}{RequestBody}",
            nameof(RelateFilesAsync),
            Environment.NewLine,
            FormatJsonForLog(serializedPayload));

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        LogDeprecatedHeader(response, nameof(RelateFilesAsync));
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) related-files response: StatusCode={StatusCode}, Body={Body}",
            nameof(RelateFilesAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed with status code {StatusCode}, Body={Body}",
                nameof(RelateFilesAsync),
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult((int)response.StatusCode, false);
        }

        return new GestionaApiCallResult((int)response.StatusCode, true);
    }

    public async Task<GestionaApiCallResult> DeleteRelatedFileAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string fileId,
        string relatedFileId,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = $"{FilesRoute}/{Uri.EscapeDataString(fileId)}/related-files/{Uri.EscapeDataString(relatedFileId)}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Delete, route);

        _logger.LogInformation(
            "({Method}) deleting Gestiona related file {RelatedFileId} from file {FileId} via {RequestUri}",
            nameof(DeleteRelatedFileAsync),
            relatedFileId,
            fileId,
            new Uri(httpClient.BaseAddress, httpRequest.RequestUri!));

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        LogDeprecatedHeader(response, nameof(DeleteRelatedFileAsync));
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) delete related-files response: StatusCode={StatusCode}, Body={Body}",
            nameof(DeleteRelatedFileAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed with status code {StatusCode}, Body={Body}",
                nameof(DeleteRelatedFileAsync),
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult((int)response.StatusCode, false);
        }

        return new GestionaApiCallResult((int)response.StatusCode, true);
    }

    public async Task<GestionaApiCallResult<IReadOnlyList<RelatedFile>>> GetRelatedFilesAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string fileId,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = $"{FilesRoute}/{Uri.EscapeDataString(fileId)}/related-files";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, route);

        _logger.LogInformation(
            "({Method}) getting Gestiona related files for file {FileId} via {RequestUri}",
            nameof(GetRelatedFilesAsync),
            fileId,
            new Uri(httpClient.BaseAddress, httpRequest.RequestUri!));

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        LogDeprecatedHeader(response, nameof(GetRelatedFilesAsync));
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) get related-files response: StatusCode={StatusCode}, Body={Body}",
            nameof(GetRelatedFilesAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed with status code {StatusCode}, Body={Body}",
                nameof(GetRelatedFilesAsync),
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<IReadOnlyList<RelatedFile>>((int)response.StatusCode, false, []);
        }

        var responseModel = DeserializeResponse<RelatedFilesResponse>(
            responseBody,
            nameof(GetRelatedFilesAsync));

        return new GestionaApiCallResult<IReadOnlyList<RelatedFile>>(
            (int)response.StatusCode,
            true,
            responseModel?.Content ?? []);
    }

    /// <summary>
    /// Creates a digital document in the specified Gestiona file using previously uploaded content.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="fileId">The Gestiona file identifier that will receive the new document.</param>
    /// <param name="folderId">The optional Gestiona folder identifier that will receive the new document.</param>
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
            elaboration_state = request.ElaborationState,
            metadata_type = request.MetadataType,
            // trashed = request.Trashed,
            // version = request.Version,
            links = new[]
            {
                new
                {
                    rel = "content",
                    href = request.ContentHref
                }
            }
        };
        var payloadNode = JsonSerializer.SerializeToNode(payload)!;
        if (request.MetadataSubtype is not null)
        {
            payloadNode["metadata_subtype"] = request.MetadataSubtype;
        }
        var serializedPayload = payloadNode.ToJsonString();

        _logger.LogDebug(
            "(({Method})) documents-and-folders request: Route={Route}, Payload={Payload}",
            nameof(CreateDocumentAndFolderAsync),
            route,
            serializedPayload);
        _logger.LogDebug(
            "({Method}) Gestiona request body:{NewLine}{RequestBody}",
            nameof(CreateDocumentAndFolderAsync),
            Environment.NewLine,
            FormatJsonForLog(serializedPayload));

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

        LogDeprecatedHeader(response, nameof(CreateDocumentAndFolderAsync));

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
        LogDeprecatedHeader(response, nameof(GetProcessThirdIdsAsync));
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
    /// Gets the documents and folders associated with a Gestiona file.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="processId">The Gestiona file identifier whose documents and folders should be retrieved.</param>
    /// <param name="documentId">The optional document or folder identifier whose child items should be retrieved.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the mapped document and folder items.</returns>
    public async Task<GestionaApiCallResult<IReadOnlyList<ProcessDocument>>> GetProcessDocumentsAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string processId,
        string? documentId,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = BuildDocumentsAndFoldersRoute(processId, documentId);
        using var request = new HttpRequestMessage(HttpMethod.Get, route);

        _logger.LogInformation(
            "({Method}) getting Gestiona documents and folders for process {ProcessId} via {RequestUri}",
            nameof(GetProcessDocumentsAsync),
            processId,
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        LogDeprecatedHeader(response, nameof(GetProcessDocumentsAsync));
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) process documents response: StatusCode={StatusCode}, Body={Body}",
            nameof(GetProcessDocumentsAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed for process {ProcessId} with status code {StatusCode}, Body={Body}",
                nameof(GetProcessDocumentsAsync),
                processId,
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<IReadOnlyList<ProcessDocument>>((int)response.StatusCode, false, []);
        }

        if (string.IsNullOrWhiteSpace(responseBody) ||
            responseBody is "<empty>" or "<no content>")
        {
            return new GestionaApiCallResult<IReadOnlyList<ProcessDocument>>((int)response.StatusCode, true, []);
        }

        try
        {
            var upstream = JsonSerializer.Deserialize<ProcessDocumentsAndFoldersResponse>(responseBody);
            var documents = upstream?.Content?
                .Select(item => new ProcessDocument(
                    item.Type ?? string.Empty,
                    item.Rel ?? string.Empty,
                    GetLastHrefSegment(item.Href) ?? string.Empty))
                .ToArray() ?? [];

            return new GestionaApiCallResult<IReadOnlyList<ProcessDocument>>((int)response.StatusCode, true, documents);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "({Method}) failed to parse Gestiona process documents response body.", nameof(GetProcessDocumentsAsync));
            return new GestionaApiCallResult<IReadOnlyList<ProcessDocument>>((int)response.StatusCode, false, []);
        }
    }

    /// <summary>
    /// Gets all procedure-data definitions associated with a Gestiona process file.
    /// </summary>
    public async Task<GestionaApiCallResult<IReadOnlyList<ProcessProcedureDataDefinition>>> GetProcessProcedureDataAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string processId,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = $"{FilesRoute}/{Uri.EscapeDataString(processId)}/procedure-data";
        _logger.LogInformation(
            "({Method}) getting Gestiona procedure data for process {ProcessId} via {RequestUri}",
            nameof(GetProcessProcedureDataAsync),
            processId,
            new Uri(httpClient.BaseAddress, route));

        return await GetPagedContentAsync<ProcessProcedureDataResponse, ProcessProcedureDataDefinition>(
            httpClient,
            route,
            nameof(GetProcessProcedureDataAsync),
            response => response.Content,
            response => response.Links,
            cancellationToken);
    }

    /// <summary>
    /// Gets one procedure-data value associated with a Gestiona process file.
    /// </summary>
    public async Task<GestionaApiCallResult<ProcessProcedureDataDefinition?>> GetProcessProcedureDataValueAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string processId,
        string referenceId,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = $"{FilesRoute}/{Uri.EscapeDataString(processId)}/procedure-data/{Uri.EscapeDataString(referenceId)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, route);

        _logger.LogInformation(
            "({Method}) getting Gestiona procedure data {ReferenceId} for process {ProcessId} via {RequestUri}",
            nameof(GetProcessProcedureDataValueAsync),
            referenceId,
            processId,
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        LogDeprecatedHeader(response, nameof(GetProcessProcedureDataValueAsync));
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) procedure-data value response: StatusCode={StatusCode}, Body={Body}",
            nameof(GetProcessProcedureDataValueAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            return new GestionaApiCallResult<ProcessProcedureDataDefinition?>((int)response.StatusCode, false, null);
        }

        var value = DeserializeResponse<ProcessProcedureDataDefinition>(
            responseBody,
            nameof(GetProcessProcedureDataValueAsync));
        return new GestionaApiCallResult<ProcessProcedureDataDefinition?>((int)response.StatusCode, value is not null, value);
    }

    /// <summary>
    /// Gets signatures associated with a document in a Gestiona file.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="processId">The Gestiona file identifier that contains the document.</param>
    /// <param name="documentId">The Gestiona document identifier whose signatures should be retrieved.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing upstream signature items.</returns>
    public async Task<GestionaApiCallResult<IReadOnlyList<ProcessDocumentSignature>>> GetProcessDocumentSignaturesAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string processId,
        string documentId,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = $"{FilesRoute}/{Uri.EscapeDataString(processId)}/documents/{Uri.EscapeDataString(documentId)}/signatures";
        using var request = new HttpRequestMessage(HttpMethod.Get, route);

        _logger.LogInformation(
            "({Method}) getting Gestiona document signatures for process {ProcessId}, document {DocumentId} via {RequestUri}",
            nameof(GetProcessDocumentSignaturesAsync),
            processId,
            documentId,
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        LogDeprecatedHeader(response, nameof(GetProcessDocumentSignaturesAsync));
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) process document signatures response: StatusCode={StatusCode}, Body={Body}",
            nameof(GetProcessDocumentSignaturesAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed for process {ProcessId}, document {DocumentId} with status code {StatusCode}, Body={Body}",
                nameof(GetProcessDocumentSignaturesAsync),
                processId,
                documentId,
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<IReadOnlyList<ProcessDocumentSignature>>((int)response.StatusCode, false, []);
        }

        var responseModel = DeserializeResponse<ProcessDocumentSignaturesResponse>(
            responseBody,
            nameof(GetProcessDocumentSignaturesAsync));

        return new GestionaApiCallResult<IReadOnlyList<ProcessDocumentSignature>>(
            (int)response.StatusCode,
            true,
            responseModel?.Content ?? []);
    }

    /// <summary>
    /// Gets a Gestiona user by following an absolute or relative user href.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="userHref">The absolute or relative Gestiona user href.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the user when found.</returns>
    public async Task<GestionaApiCallResult<ProcessAssigneeUser?>> GetUserByHrefAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string userHref,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var userUri = ResolveHrefUri(gestionaApiBaseUrl, userHref);
        using var request = new HttpRequestMessage(HttpMethod.Get, userUri);

        _logger.LogInformation(
            "({Method}) getting Gestiona user via {RequestUri}",
            nameof(GetUserByHrefAsync),
            userUri);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        LogDeprecatedHeader(response, nameof(GetUserByHrefAsync));
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) Gestiona user response: StatusCode={StatusCode}, Body={Body}",
            nameof(GetUserByHrefAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed with status code {StatusCode}, Body={Body}",
                nameof(GetUserByHrefAsync),
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<ProcessAssigneeUser?>((int)response.StatusCode, false, null);
        }

        var user = DeserializeResponse<ProcessAssigneeUser>(
            responseBody,
            nameof(GetUserByHrefAsync));

        return new GestionaApiCallResult<ProcessAssigneeUser?>((int)response.StatusCode, true, user);
    }

    /// <summary>
    /// Gets the first Gestiona assignee user matching the provided username filter.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="request">The assignee user filter payload.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the first matching assignee user when available.</returns>
    public async Task<GestionaApiCallResult<ProcessAssigneeUser?>> GetProcessAssigneeUserAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        GetProcessAssigneeUserRequest request,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var serializedPayload = JsonSerializer.Serialize(request);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, FileAssigneeUsersRoute)
        {
            Content = new StringContent(serializedPayload, Encoding.UTF8, AssigneesFilterContentType)
        };

        _logger.LogInformation(
            "({Method}) getting Gestiona assignee user via {RequestUri}",
            nameof(GetProcessAssigneeUserAsync),
            new Uri(httpClient.BaseAddress, httpRequest.RequestUri!));

        _logger.LogDebug(
            "({Method}) Gestiona request body:{NewLine}{RequestBody}",
            nameof(GetProcessAssigneeUserAsync),
            Environment.NewLine,
            FormatJsonForLog(serializedPayload));

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        LogDeprecatedHeader(response, nameof(GetProcessAssigneeUserAsync));
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) assignee users response: StatusCode={StatusCode}, Body={Body}",
            nameof(GetProcessAssigneeUserAsync),
            response.StatusCode,
            responseBody);

        if (response.IsSuccessStatusCode)
        {
            var responseModel = DeserializeResponse<ProcessAssigneeUsersResponse>(
                responseBody,
                nameof(GetProcessAssigneeUserAsync));

            return new GestionaApiCallResult<ProcessAssigneeUser?>(
                (int)response.StatusCode,
                true,
                responseModel?.Content?.FirstOrDefault());
        }

        _logger.LogWarning(
            "({Method}) failed with status code {StatusCode}, Body={Body}",
            nameof(GetProcessAssigneeUserAsync),
            response.StatusCode,
            FormatJsonForLog(responseBody));
        return new GestionaApiCallResult<ProcessAssigneeUser?>((int)response.StatusCode, false, null);
    }

    /// <summary>
    /// Gets the Gestiona assignee groups available for process assignment.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the available assignee groups.</returns>
    public async Task<GestionaApiCallResult<IReadOnlyList<ProcessAssigneeGroup>>> GetProcessAssigneeGroupsAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, FileAssigneeGroupsRoute);

        _logger.LogInformation(
            "({Method}) getting Gestiona assignee groups via {RequestUri}",
            nameof(GetProcessAssigneeGroupsAsync),
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        LogDeprecatedHeader(response, nameof(GetProcessAssigneeGroupsAsync));
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) assignee groups response: StatusCode={StatusCode}, Body={Body}",
            nameof(GetProcessAssigneeGroupsAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed with status code {StatusCode}, Body={Body}",
                nameof(GetProcessAssigneeGroupsAsync),
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<IReadOnlyList<ProcessAssigneeGroup>>((int)response.StatusCode, false, []);
        }

        var responseModel = DeserializeResponse<ProcessAssigneeGroupsResponse>(
            responseBody,
            nameof(GetProcessAssigneeGroupsAsync));

        return new GestionaApiCallResult<IReadOnlyList<ProcessAssigneeGroup>>(
            (int)response.StatusCode,
            true,
            responseModel?.Content ?? []);
    }

    /// <summary>
    /// Creates an external URL document in the specified Gestiona file.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="fileId">The Gestiona file identifier that will receive the new document.</param>
    /// <param name="folderId">The optional Gestiona folder identifier that will receive the new document.</param>
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
            elaboration_state = request.ElaborationState,
            metadata_type = request.MetadataType,
            // trashed = request.Trashed,
            // version = request.Version,
            external_url = request.ExternalUrl

        };
        var payloadNode = JsonSerializer.SerializeToNode(payload)!;
        if (request.MetadataSubtype is not null)
        {
            payloadNode["metadata_subtype"] = request.MetadataSubtype;
        }
        var serializedPayload = payloadNode.ToJsonString();

        _logger.LogDebug(
            "({Method}) documents-and-folders request: Route={Route}, Payload={Payload}",
            nameof(CreateDocumentUrlAsync),
            route,
            serializedPayload);
        _logger.LogDebug(
            "({Method}) Gestiona request body:{NewLine}{RequestBody}",
            nameof(CreateDocumentUrlAsync),
            Environment.NewLine,
            FormatJsonForLog(serializedPayload));

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
        LogDeprecatedHeader(response, nameof(CreateDocumentUrlAsync));
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
            _logger.LogDebug(
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
    /// <param name="folderId">The optional parent Gestiona folder identifier that will receive the new folder.</param>
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
        _logger.LogDebug(
            "({Method}) Gestiona request body:{NewLine}{RequestBody}",
            nameof(CreateFolderAsync),
            Environment.NewLine,
            FormatJsonForLog(serializedPayload));

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
        LogDeprecatedHeader(response, nameof(CreateFolderAsync));
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
}
