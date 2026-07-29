using System.Text.Json.Serialization;

namespace GestionaGateway.Core.Models;

/// <summary>
/// Represents a document or folder returned for a Gestiona process file.
/// </summary>
public sealed record ProcessDocument(
    [property: JsonPropertyName("type")]
    string Type,
    [property: JsonPropertyName("name")]
    string Name,
    [property: JsonPropertyName("id")]
    string Id);

/// <summary>
/// Represents the upstream Gestiona documents-and-folders response.
/// </summary>
public sealed record ProcessDocumentsAndFoldersResponse(
    [property: JsonPropertyName("content")]
    IReadOnlyList<ProcessDocumentLink>? Content);

/// <summary>
/// Represents an entry in the upstream documents-and-folders content array.
/// </summary>
public sealed record ProcessDocumentLink(
    [property: JsonPropertyName("type")]
    string? Type,
    [property: JsonPropertyName("rel")]
    string? Rel,
    [property: JsonPropertyName("href")]
    string? Href);
