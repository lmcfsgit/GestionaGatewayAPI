using System.Text.Json.Serialization;

namespace GestionaGateway.Core.Models;

/// <summary>
/// Represents the gateway request body used to create a document or folder in a process.
/// </summary>
public sealed class UploadDocumentRequest
{
    /// <summary>Gets the elaboration state, defaulting to EE01 when omitted.</summary>
    [JsonPropertyName("elaboration_state")]
    public string? ElaborationState { get; init; } = "EE01";

    /// <summary>Gets the metadata type, defaulting to TD99 when omitted.</summary>
    [JsonPropertyName("metadata_type")]
    public string? MetadataType { get; init; } = "TD99";

    /// <summary>Gets the optional metadata subtype.</summary>
    [JsonPropertyName("metadata_subtype")]
    public string? MetadataSubtype { get; init; }

    /// <summary>
    /// Gets the optional operation identifier echoed in the gateway response envelope.
    /// </summary>
    public string? OperationId { get; init; }

    /// <summary>
    /// Gets the document or folder name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the local file name used for digital uploads when inline content is not provided.
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    /// Gets the source type, such as DIGITAL, EXTERNAL_URL, or FOLDER.
    /// </summary>
    public string? DocumentSourceType { get; init; }

    /// <summary>
    /// Gets the external URL used when creating an external URL document.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Gets the base64-encoded document content used for digital uploads.
    /// </summary>
    public string? Content { get; init; }
}
