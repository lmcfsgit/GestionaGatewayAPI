using System.Text.Json.Serialization;

namespace GestionaGateway.Core.Models;

public sealed record ProcessDocumentSignaturesResponse(
    [property: JsonPropertyName("content")] IReadOnlyList<ProcessDocumentSignature>? Content);

public sealed record ProcessDocumentSignature(
    [property: JsonPropertyName("date")] string? Date,
    [property: JsonPropertyName("signature_state")] string? SignatureState,
    [property: JsonPropertyName("links")] IReadOnlyList<GestionaLink>? Links);

public sealed record ProcessDocumentSignatureResult(
    [property: JsonPropertyName("date_signed")] string? DateSigned,
    [property: JsonPropertyName("signature_state")] string? SignatureState,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("name")] string? Name);

public sealed record GetProcessDocumentSignaturesResult(
    bool Success,
    GetProcessDocumentsFailureKind FailureKind,
    string? ErrorMessage,
    IReadOnlyList<ProcessDocumentSignatureResult>? Signatures,
    int? UpstreamStatusCode);
