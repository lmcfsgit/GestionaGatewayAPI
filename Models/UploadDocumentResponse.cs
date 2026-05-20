using System.Text.Json.Serialization;

namespace GestionaGatewayAPI.Models;

public sealed record UploadDocumentResponse(
    string? OperationId,
    bool Success,
    object Result);

public sealed record UploadDocumentResult(
    string Id,
    string ProcessId,
    [property: JsonPropertyName("creation_date")] string CreationDate,
    [property: JsonPropertyName("modification_date")] string ModificationDate);

public sealed record UploadDocumentError(
    int Code,
    string Name,
    string Kind,
    string Message);
