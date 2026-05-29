namespace GestionaGateway.Core.Models;

/// <summary>
/// Identifies the reason a process document creation workflow failed.
/// </summary>
public enum CreateDocumentInProcessFailureKind
{
    /// <summary>
    /// Indicates no failure occurred.
    /// </summary>
    None,

    /// <summary>
    /// Indicates required configuration is missing or invalid.
    /// </summary>
    Configuration,

    /// <summary>
    /// Indicates the request failed validation.
    /// </summary>
    Validation,

    /// <summary>
    /// Indicates the requested resource was not found.
    /// </summary>
    NotFound,

    /// <summary>
    /// Indicates an upstream Gestiona API call failed.
    /// </summary>
    Upstream
}

/// <summary>
/// Represents the created Gestiona entity returned by a process document creation workflow.
/// </summary>
/// <param name="Id">The created document or folder identifier.</param>
/// <param name="ProcessId">The Gestiona file identifier associated with the created entity.</param>
/// <param name="CreationDate">The formatted creation date.</param>
/// <param name="ModificationDate">The formatted modification date.</param>
public sealed record CreateDocumentInProcessDocument(
    string Id,
    string ProcessId,
    string CreationDate,
    string ModificationDate);

/// <summary>
/// Represents the result of creating a document or folder in a Gestiona process file.
/// </summary>
/// <param name="Success">Indicates whether the workflow completed successfully.</param>
/// <param name="FailureKind">The failure classification when <paramref name="Success"/> is false.</param>
/// <param name="ErrorMessage">The failure message when the workflow fails.</param>
/// <param name="Document">The created document or folder information when the workflow succeeds.</param>
/// <param name="UpstreamStatusCode">The upstream Gestiona status code when the failure came from Gestiona.</param>
public sealed record CreateDocumentInProcessResult(
    bool Success,
    CreateDocumentInProcessFailureKind FailureKind,
    string? ErrorMessage,
    CreateDocumentInProcessDocument? Document,
    int? UpstreamStatusCode);
