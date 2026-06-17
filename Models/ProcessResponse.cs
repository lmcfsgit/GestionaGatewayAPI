using System.Text.Json.Serialization;

namespace GestionaGatewayAPI.Models;

/// <summary>
/// Represents the success payload returned by process lookup endpoints.
/// </summary>
/// <param name="Id">The resolved Gestiona file identifier.</param>
/// <param name="ProcessNumber">The process number used for resolution.</param>
public sealed record ProcessResponse(
    [property: JsonPropertyName("Id")]
    string Id,
    [property: JsonPropertyName("processNumber")]
    string ProcessNumber);

/// <summary>
/// Represents an error payload returned by process lookup endpoints.
/// </summary>
/// <param name="Code">The HTTP status code returned by the gateway.</param>
/// <param name="Name">The HTTP reason phrase.</param>
/// <param name="Kind">The gateway failure classification.</param>
/// <param name="Message">The human-readable error message.</param>
public sealed record ProcessError(
    int Code,
    string Name,
    string Kind,
    string Message);
