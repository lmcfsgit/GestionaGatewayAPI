namespace GestionaGateway.Core.Models;

/// <summary>
/// Identifies the reason a document download workflow failed.
/// </summary>
public enum DownloadDocumentFailureKind
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
    /// Indicates the requested document was not found.
    /// </summary>
    NotFound,

    /// <summary>
    /// Indicates an upstream Gestiona API call failed.
    /// </summary>
    Upstream
}

/// <summary>
/// Represents the result of a document download workflow.
/// </summary>
/// <param name="Success">Indicates whether the workflow completed successfully.</param>
/// <param name="FailureKind">The failure classification when <paramref name="Success"/> is false.</param>
/// <param name="ErrorMessage">The failure message when the workflow fails.</param>
/// <param name="Document">The downloaded document content and metadata when the workflow succeeds.</param>
/// <param name="UpstreamStatusCode">The upstream Gestiona status code when the failure came from Gestiona.</param>
public sealed record DownloadDocumentResult(
    bool Success,
    DownloadDocumentFailureKind FailureKind,
    string? ErrorMessage,
    DownloadedDocument? Document,
    int? UpstreamStatusCode);
