using System.Text.Json.Serialization;

namespace GestionaGateway.Core.Models;

public sealed record UpdateProcessDocumentRequest(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("metadata_language")] string? MetadataLanguage,
    [property: JsonPropertyName("elaboration_state"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ElaborationState = null,
    [property: JsonPropertyName("metadata_type"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MetadataType = null,
    [property: JsonPropertyName("metadata_subtype"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MetadataSubtype = null);

public sealed record ProcessDocumentMetadata(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("metadata_language")] string? MetadataLanguage,
    [property: JsonPropertyName("elaboration_state")] string? ElaborationState = null,
    [property: JsonPropertyName("metadata_type")] string? MetadataType = null,
    [property: JsonPropertyName("metadata_subtype")] string? MetadataSubtype = null);

public sealed record GetProcessDocumentMetadataResult(
    bool Success,
    GetProcessDocumentsFailureKind FailureKind,
    string? ErrorMessage,
    ProcessDocumentMetadata? Document,
    int? UpstreamStatusCode);
