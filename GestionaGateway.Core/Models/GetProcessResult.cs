namespace GestionaGateway.Core.Models;

/// <summary>
/// Represents the result of resolving a Gestiona file identifier from a process number.
/// </summary>
/// <param name="Success">Indicates whether the workflow completed successfully.</param>
/// <param name="FailureKind">The failure classification when <paramref name="Success"/> is false.</param>
/// <param name="ErrorMessage">The failure message when the workflow fails.</param>
/// <param name="ProcessId">The resolved Gestiona file identifier when available.</param>
/// <param name="ProcessNumber">The process number used for resolution.</param>
/// <param name="UpstreamStatusCode">The upstream Gestiona status code when the failure came from Gestiona.</param>
public sealed record GetProcessResult(
    bool Success,
    GetProcessFailureKind FailureKind,
    string? ErrorMessage,
    string? ProcessId,
    string? ProcessNumber,
    int? UpstreamStatusCode);

/// <summary>
/// Identifies the reason a process lookup workflow failed.
/// </summary>
public enum GetProcessFailureKind
{
    /// <summary>
    /// Indicates no failure occurred.
    /// </summary>
    None,

    /// <summary>
    /// Indicates required configuration is missing or invalid.
    /// </summary>
    Configuration,

    /// <summary>
    /// Indicates the request failed validation.
    /// </summary>
    Validation,

    /// <summary>
    /// Indicates no Gestiona file was found for the process number.
    /// </summary>
    NotFound,

    /// <summary>
    /// Indicates an upstream Gestiona API call failed.
    /// </summary>
    Upstream
}
