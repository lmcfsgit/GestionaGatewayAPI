namespace GestionaGatewayAPI.Models;

/// <summary>
/// Represents the success payload returned by process thirds endpoints.
/// </summary>
/// <param name="ProcessId">The Gestiona file identifier used to retrieve third parties.</param>
/// <param name="Thirds">The semicolon-separated third identifiers.</param>
public sealed record ProcessThirdsResponse(
    string ProcessId,
    string Thirds);

/// <summary>
/// Represents an error payload returned by process thirds endpoints.
/// </summary>
/// <param name="Code">The HTTP status code returned by the gateway.</param>
/// <param name="Name">The HTTP reason phrase.</param>
/// <param name="Kind">The gateway failure classification.</param>
/// <param name="Message">The human-readable error message.</param>
public sealed record ProcessThirdsError(
    int Code,
    string Name,
    string Kind,
    string Message);
