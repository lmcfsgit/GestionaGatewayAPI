using GestionaGateway.Core.Models;

namespace GestionaGateway.Core.Services;

/// <summary>
/// Defines third lookup workflows that coordinate Gestiona API operations.
/// </summary>
public interface IGestionaThirdService
{
    /// <summary>
    /// Gets a third from Gestiona and enriches it with its default address.
    /// </summary>
    /// <param name="thirdId">The Gestiona third identifier to retrieve.</param>
    /// <param name="accessTokenOverride">The optional request-provided Gestiona access token. When absent, the configured token is used.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The third lookup result, including failure details or the enriched third data.</returns>
    Task<GetThirdResult> GetThirdAsync(
        string thirdId,
        string? accessTokenOverride,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets a third from Gestiona by NIF and enriches it with its default address.
    /// </summary>
    /// <param name="nif">The NIF used to resolve the Gestiona third identifier.</param>
    /// <param name="accessTokenOverride">The optional request-provided Gestiona access token. When absent, the configured token is used.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The third lookup result, including failure details or the enriched third data.</returns>
    Task<GetThirdResult> GetThirdByNifAsync(
        string nif,
        string? accessTokenOverride,
        CancellationToken cancellationToken);
}
