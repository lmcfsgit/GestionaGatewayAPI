namespace GestionaGateway.Core.Models;

/// <summary>
/// Represents the result of a Gestiona API call that does not return a value.
/// </summary>
/// <param name="StatusCode">The HTTP status code returned by Gestiona.</param>
/// <param name="Success">Indicates whether the upstream call completed successfully.</param>
public sealed record GestionaApiCallResult(
    int StatusCode,
    bool Success);

/// <summary>
/// Represents the result of a Gestiona API call that returns a value.
/// </summary>
/// <typeparam name="T">The value type returned by the upstream call.</typeparam>
/// <param name="StatusCode">The HTTP status code returned by Gestiona.</param>
/// <param name="Success">Indicates whether the upstream call completed successfully.</param>
/// <param name="Value">The value returned by the upstream call when available.</param>
public sealed record GestionaApiCallResult<T>(
    int StatusCode,
    bool Success,
    T? Value);
