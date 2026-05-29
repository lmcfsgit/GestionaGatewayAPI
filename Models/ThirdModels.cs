namespace GestionaGatewayAPI.Models;

/// <summary>
/// Represents an error payload returned by third lookup endpoints.
/// </summary>
/// <param name="Code">The HTTP status code returned by the gateway.</param>
/// <param name="Name">The HTTP reason phrase.</param>
/// <param name="Kind">The gateway failure classification.</param>
/// <param name="Message">The human-readable error message.</param>
public sealed record ThirdError(
    int Code,
    string Name,
    string Kind,
    string Message);
