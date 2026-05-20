namespace GestionaGateway.Core.Models;

public enum DownloadDocumentFailureKind
{
    None,
    Configuration,
    Validation,
    NotFound,
    Upstream
}

public sealed record DownloadDocumentResult(
    bool Success,
    DownloadDocumentFailureKind FailureKind,
    string? ErrorMessage,
    DownloadedDocument? Document,
    int? UpstreamStatusCode);
