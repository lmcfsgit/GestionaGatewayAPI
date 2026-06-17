using GestionaGateway.Core.Models;

namespace GestionaGateway.Core.Services;

/// <summary>
/// Defines process-scoped workflows that coordinate Gestiona API operations.
/// </summary>
public interface IGestionaProcessService
{
    /// <summary>
    /// Creates a document or folder in a Gestiona process file.
    /// </summary>
    /// <param name="request">The upload request containing document metadata and either content, a file name, or an external URL.</param>
    /// <param name="processId">The process identifier or Gestiona file identifier associated with the target file.</param>
    /// <param name="folderId">The optional Gestiona folder identifier where the document or folder should be created.</param>
    /// <param name="resolveFileIdFromProcessCode">Indicates whether <paramref name="processId"/> must be resolved from a process number to a Gestiona file identifier before creating the document.</param>
    /// <param name="documentsFolder">The base folder used when the request references a local file by name.</param>
    /// <param name="accessTokenOverride">The optional request-provided Gestiona access token. When absent, the configured token is used.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The document creation result, including failure details or the created Gestiona entity information.</returns>
    Task<CreateDocumentInProcessResult> CreateDocumentInProcessAsync(
        UploadDocumentRequest request,
        string processId,
        string? folderId,
        bool resolveFileIdFromProcessCode,
        string documentsFolder,
        string? accessTokenOverride,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the Gestiona file identifier associated with a process number.
    /// </summary>
    /// <param name="processNumber">The process number used to search for the Gestiona file.</param>
    /// <param name="accessTokenOverride">The optional request-provided Gestiona access token. When absent, the configured token is used.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The process lookup result, including the resolved Gestiona file identifier on success.</returns>
    Task<GetProcessResult> GetProcessAsync(
        string processNumber,
        string? accessTokenOverride,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the third identifiers associated with a Gestiona process file.
    /// </summary>
    /// <param name="processId">The process identifier or Gestiona file identifier used to locate the third parties.</param>
    /// <param name="resolveFileIdFromProcessCode">Indicates whether <paramref name="processId"/> must first be resolved from a process number to a Gestiona file identifier.</param>
    /// <param name="accessTokenOverride">The optional request-provided Gestiona access token. When absent, the configured token is used.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The process thirds result, including the resolved Gestiona file identifier and semicolon-separated third identifiers on success.</returns>
    Task<GetProcessThirdsResult> GetProcessThirdsAsync(
        string processId,
        bool resolveFileIdFromProcessCode,
        string? accessTokenOverride,
        CancellationToken cancellationToken);
}
