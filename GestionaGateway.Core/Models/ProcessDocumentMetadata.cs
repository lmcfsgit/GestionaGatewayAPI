using System.Text.Json.Serialization;

namespace GestionaGateway.Core.Models;

public sealed record UpdateProcessDocumentRequest(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("metadata_language")] string? MetadataLanguage);

public sealed record ProcessDocumentMetadata(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("metadata_language")] string? MetadataLanguage);

public sealed record GetProcessDocumentMetadataResult(
    bool Success,
    GetProcessDocumentsFailureKind FailureKind,
    string? ErrorMessage,
    ProcessDocumentMetadata? Document,
    int? UpstreamStatusCode);
