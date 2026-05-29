using System.Text.Json.Serialization;

namespace GestionaGatewayAPI.Models;

/// <summary>
/// Represents the success payload returned by upload endpoints.
/// </summary>
/// <param name="Id">The created Gestiona document or folder identifier.</param>
/// <param name="ProcessId">The Gestiona file identifier associated with the created entity.</param>
/// <param name="CreationDate">The formatted creation date.</param>
/// <param name="ModificationDate">The formatted modification date.</param>
public sealed record UploadDocumentResult(
    string Id,
    string ProcessId,
    [property: JsonPropertyName("creation_date")] string CreationDate,
    [property: JsonPropertyName("modification_date")] string ModificationDate);

/// <summary>
/// Represents an error payload returned by upload and download endpoints.
/// </summary>
/// <param name="Code">The HTTP status code returned by the gateway.</param>
/// <param name="Name">The HTTP reason phrase.</param>
/// <param name="Kind">The gateway failure classification.</param>
/// <param name="Message">The human-readable error message.</param>
public sealed record UploadDocumentError(
    int Code,
    string Name,
    string Kind,
    string Message);
