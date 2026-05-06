namespace GestionaGateway.Core.Models;

public enum CreateDocumentInProcessFailureKind
{
    None,
    Configuration,
    Validation,
    NotFound,
    Upstream
}

public sealed record CreateDocumentInProcessDocument(
    string Id,
    string ProcessId,
    string CreationDate,
    string ModificationDate);

public sealed record CreateDocumentInProcessResult(
    bool Success,
    CreateDocumentInProcessFailureKind FailureKind,
    string? ErrorMessage,
    CreateDocumentInProcessDocument? Document);
