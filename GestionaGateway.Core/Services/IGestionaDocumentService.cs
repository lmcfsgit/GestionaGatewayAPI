using GestionaGateway.Core.Models;

namespace GestionaGateway.Core.Services;

/// <summary>
/// Defines document retrieval workflows that coordinate Gestiona API operations.
/// </summary>
public interface IGestionaDocumentService
{
    /// <summary>
    /// Downloads a document from Gestiona.
    /// </summary>
    /// <param name="documentId">The Gestiona document identifier to download.</param>
    /// <param name="accessTokenOverride">The optional request-provided Gestiona access token. When absent, the configured token is used.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The download result, including failure details or the downloaded document content and metadata.</returns>
    Task<DownloadDocumentResult> DownloadDocumentAsync(
        string documentId,
        string? accessTokenOverride,
        CancellationToken cancellationToken);
}
