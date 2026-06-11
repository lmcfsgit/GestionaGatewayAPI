using GestionaGateway.Core.Configuration;
using GestionaGateway.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestionaGateway.Core.Services;

/// <summary>
/// Provides document download workflows for the Gestiona API.
/// </summary>
public sealed class GestionaDocumentService : IGestionaDocumentService
{
    private readonly GestionaOptions _gestionaOptions;
    private readonly IGestionaApiClient _gestionaApiClient;
    private readonly ILogger<GestionaDocumentService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GestionaDocumentService"/> class.
    /// </summary>
    /// <param name="gestionaOptions">The configured Gestiona options.</param>
    /// <param name="gestionaApiClient">The client used to communicate with the Gestiona API.</param>
    /// <param name="logger">The logger used for operational and diagnostic events.</param>
    public GestionaDocumentService(
        IOptions<GestionaOptions> gestionaOptions,
        IGestionaApiClient gestionaApiClient,
        ILogger<GestionaDocumentService> logger)
    {
        _gestionaOptions = gestionaOptions.Value;
        _gestionaApiClient = gestionaApiClient;
        _logger = logger;
    }

    /// <summary>
    /// Downloads a document from Gestiona.
    /// </summary>
    /// <param name="documentId">The identifier of the document to download.</param>
    /// <param name="accessTokenOverride">The optional request-provided Gestiona access token. When absent, the configured token is used.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The result of the document download workflow.</returns>
    public async Task<DownloadDocumentResult> DownloadDocumentAsync(
        string documentId,
        string? accessTokenOverride,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "({Method}) started. DocumentId={DocumentId}",
            nameof(DownloadDocumentAsync),
            documentId);

        var gestionaApiBaseUrl = _gestionaOptions.GestionaApiBaseUrl;
        var accessToken = ResolveAccessToken(accessTokenOverride);

        if (string.IsNullOrWhiteSpace(gestionaApiBaseUrl))
        {
            _logger.LogWarning("({Method}) failed at step {Step} for document {DocumentId}", nameof(DownloadDocumentAsync), "ValidateConfiguration:GestionaApiBaseUrl", documentId);
            return DownloadFailure(DownloadDocumentFailureKind.Configuration, "Gestiona API base URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _logger.LogWarning("({Method}) failed at step {Step} for document {DocumentId}", nameof(DownloadDocumentAsync), "ValidateConfiguration:AccessToken", documentId);
            return DownloadFailure(DownloadDocumentFailureKind.Configuration, "Gestiona access token is not configured.");
        }

        if (string.IsNullOrWhiteSpace(documentId))
        {
            _logger.LogWarning("({Method}) failed at step {Step}", nameof(DownloadDocumentAsync), "ValidateDocumentId");
            return DownloadFailure(DownloadDocumentFailureKind.Validation, "documentId is required.");
        }

        var downloadResult = await _gestionaApiClient.DownloadDocumentAsync(
            gestionaApiBaseUrl,
            accessToken,
            documentId,
            cancellationToken);

        if (!downloadResult.Success)
        {
            _logger.LogWarning("({Method}) failed at step {Step} for document {DocumentId}", nameof(DownloadDocumentAsync), "DownloadDocumentFromGestiona", documentId);
            var failureKind = downloadResult.StatusCode == 404
                ? DownloadDocumentFailureKind.NotFound
                : DownloadDocumentFailureKind.Upstream;
            return DownloadFailure(
                failureKind,
                $"Failed to download document from Gestiona: {documentId}.",
                downloadResult.StatusCode);
        }

        if (downloadResult.Value is null)
        {
            _logger.LogWarning("({Method}) failed at step {Step} for document {DocumentId}", nameof(DownloadDocumentAsync), "DownloadDocumentFromGestiona", documentId);
            return DownloadFailure(
                DownloadDocumentFailureKind.Upstream,
                $"Failed to download document from Gestiona: {documentId}.",
                downloadResult.StatusCode);
        }

        _logger.LogInformation(
            "({Method}) succeeded. DocumentId={DocumentId}, FileName={FileName}, ContentType={ContentType}, ContentLength={ContentLength}",
            nameof(DownloadDocumentAsync),
            documentId,
            downloadResult.Value.FileName,
            downloadResult.Value.ContentType,
            downloadResult.Value.Content.Length);

        return new DownloadDocumentResult(
            true,
            DownloadDocumentFailureKind.None,
            null,
            downloadResult.Value,
            null);
    }

    /// <summary>
    /// Creates a standardized failure result for the document download workflow.
    /// </summary>
    /// <param name="failureKind">The category of failure.</param>
    /// <param name="errorMessage">The human-readable error message.</param>
    /// <param name="upstreamStatusCode">The optional HTTP status code returned by the upstream API.</param>
    /// <returns>A failed <see cref="DownloadDocumentResult"/> instance.</returns>
    private static DownloadDocumentResult DownloadFailure(
        DownloadDocumentFailureKind failureKind,
        string errorMessage,
        int? upstreamStatusCode = null)
    {
        return new DownloadDocumentResult(false, failureKind, errorMessage, null, upstreamStatusCode);
    }

    private string? ResolveAccessToken(string? accessTokenOverride)
    {
        return string.IsNullOrWhiteSpace(accessTokenOverride)
            ? _gestionaOptions.AccessToken
            : accessTokenOverride;
    }
}
