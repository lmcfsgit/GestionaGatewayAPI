namespace GestionaGateway.Core.Models;

/// <summary>
/// Represents the result of retrieving documents and folders for a Gestiona process.
/// </summary>
public sealed record GetProcessDocumentsResult(
    bool Success,
    GetProcessDocumentsFailureKind FailureKind,
    string? ErrorMessage,
    IReadOnlyList<ProcessDocument>? Documents,
    int? UpstreamStatusCode);

/// <summary>
/// Identifies the reason a process documents lookup failed.
/// </summary>
public enum GetProcessDocumentsFailureKind
{
    None,
    Configuration,
    Validation,
    NotFound,
    Upstream
}
