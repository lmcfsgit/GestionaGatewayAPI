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
        LogDeprecatedHeader(response, nameof(DownloadDocumentAsync));

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
}
