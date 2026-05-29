namespace GestionaGateway.Core.Models;

/// <summary>
/// Represents the result of retrieving a third from Gestiona.
/// </summary>
/// <param name="Success">Indicates whether the workflow completed successfully.</param>
/// <param name="FailureKind">The failure classification when <paramref name="Success"/> is false.</param>
/// <param name="ErrorMessage">The failure message when the workflow fails.</param>
/// <param name="Third">The retrieved and enriched third when the workflow succeeds.</param>
/// <param name="UpstreamStatusCode">The upstream Gestiona status code when the failure came from Gestiona.</param>
public sealed record GetThirdResult(
    bool Success,
    GetThirdFailureKind FailureKind,
    string? ErrorMessage,
    Third? Third,
    int? UpstreamStatusCode);

/// <summary>
/// Identifies the reason a third lookup workflow failed.
/// </summary>
public enum GetThirdFailureKind
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
    /// Indicates the requested third was not found.
    /// </summary>
    NotFound,

    /// <summary>
    /// Indicates an upstream Gestiona API call failed.
    /// </summary>
    Upstream
}
