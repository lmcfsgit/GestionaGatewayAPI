using GestionaGateway.Core.Configuration;
using GestionaGateway.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestionaGateway.Core.Services;

/// <summary>
/// Provides third lookup workflows for the Gestiona API.
/// </summary>
public sealed class GestionaThirdService : IGestionaThirdService
{
    private readonly GestionaOptions _gestionaOptions;
    private readonly IGestionaApiClient _gestionaApiClient;
    private readonly ILogger<GestionaThirdService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GestionaThirdService"/> class.
    /// </summary>
    /// <param name="gestionaOptions">The configured Gestiona options.</param>
    /// <param name="gestionaApiClient">The client used to communicate with the Gestiona API.</param>
    /// <param name="logger">The logger used for operational and diagnostic events.</param>
    public GestionaThirdService(
        IOptions<GestionaOptions> gestionaOptions,
        IGestionaApiClient gestionaApiClient,
        ILogger<GestionaThirdService> logger)
    {
        _gestionaOptions = gestionaOptions.Value;
        _gestionaApiClient = gestionaApiClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets a third from Gestiona and enriches it with its default address.
    /// </summary>
    /// <param name="thirdId">The Gestiona third identifier to retrieve.</param>
    /// <param name="accessTokenOverride">The optional request-provided Gestiona access token. When absent, the configured token is used.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The third lookup result, including failure details or the enriched third data.</returns>
    public async Task<GetThirdResult> GetThirdAsync(
        string thirdId,
        string? accessTokenOverride,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "({Method}) started. ThirdId={ThirdId}",
            nameof(GetThirdAsync),
            thirdId);

        var gestionaApiBaseUrl = _gestionaOptions.GestionaApiBaseUrl;
        var accessToken = ResolveAccessToken(accessTokenOverride);

        if (string.IsNullOrWhiteSpace(gestionaApiBaseUrl))
        {
            _logger.LogWarning("({Method}) failed at step {Step} for third {ThirdId}", nameof(GetThirdAsync), "ValidateConfiguration:GestionaApiBaseUrl", thirdId);
            return Failure(GetThirdFailureKind.Configuration, "Gestiona API base URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _logger.LogWarning("({Method}) failed at step {Step} for third {ThirdId}", nameof(GetThirdAsync), "ValidateConfiguration:AccessToken", thirdId);
            return Failure(GetThirdFailureKind.Configuration, "Gestiona access token is not configured.");
        }

        if (string.IsNullOrWhiteSpace(thirdId))
        {
            _logger.LogWarning("({Method}) failed at step {Step}", nameof(GetThirdAsync), "ValidateThirdId");
            return Failure(GetThirdFailureKind.Validation, "thirdId is required.");
        }

        var thirdResult = await _gestionaApiClient.GetThirdAsync(
            gestionaApiBaseUrl,
            accessToken,
            thirdId,
            cancellationToken);

        if (!thirdResult.Success)
        {
            _logger.LogWarning("({Method}) failed at step {Step} for third {ThirdId}", nameof(GetThirdAsync), "GetThirdFromGestiona", thirdId);
            var failureKind = thirdResult.StatusCode == 404
                ? GetThirdFailureKind.NotFound
                : GetThirdFailureKind.Upstream;
            return Failure(
                failureKind,
                $"Failed to get third from Gestiona: {thirdId}.",
                GetUpstreamErrorStatusCode(thirdResult.StatusCode));
        }

        if (thirdResult.Value is null)
        {
            _logger.LogWarning("({Method}) failed at step {Step} for third {ThirdId}", nameof(GetThirdAsync), "GetThirdFromGestiona", thirdId);
            var failureKind = thirdResult.StatusCode is 204 or 404
                ? GetThirdFailureKind.NotFound
                : GetThirdFailureKind.Upstream;
            return Failure(
                failureKind,
                $"Failed to get third from Gestiona: {thirdId}.",
                GetUpstreamErrorStatusCode(thirdResult.StatusCode));
        }

        var addressResult = await _gestionaApiClient.GetThirdDefaultAddressAsync(
            gestionaApiBaseUrl,
            accessToken,
            thirdId,
            cancellationToken);

        if (!addressResult.Success)
        {
            _logger.LogWarning("({Method}) failed at step {Step} for third {ThirdId}", nameof(GetThirdAsync), "GetThirdDefaultAddressFromGestiona", thirdId);
            return Failure(
                GetThirdFailureKind.Upstream,
                $"Failed to get third default address from Gestiona: {thirdId}.",
                GetUpstreamErrorStatusCode(addressResult.StatusCode));
        }

        var third = MergeAddress(thirdResult.Value, addressResult.Value);

        _logger.LogInformation(
            "({Method}) succeeded. ThirdId={ThirdId}",
            nameof(GetThirdAsync),
            thirdId);

        return new GetThirdResult(
            true,
            GetThirdFailureKind.None,
            null,
            third,
            null);
    }

    /// <summary>
    /// Gets a third from Gestiona by NIF and enriches it with its default address.
    /// </summary>
    /// <param name="nif">The NIF used to resolve the Gestiona third identifier.</param>
    /// <param name="accessTokenOverride">The optional request-provided Gestiona access token. When absent, the configured token is used.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The third lookup result, including failure details or the enriched third data.</returns>
    public async Task<GetThirdResult> GetThirdByNifAsync(
        string nif,
        string? accessTokenOverride,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "({Method}) started. Nif={Nif}",
            nameof(GetThirdByNifAsync),
            nif);

        var gestionaApiBaseUrl = _gestionaOptions.GestionaApiBaseUrl;
        var accessToken = ResolveAccessToken(accessTokenOverride);

        if (string.IsNullOrWhiteSpace(gestionaApiBaseUrl))
        {
            _logger.LogWarning("({Method}) failed at step {Step} for nif {Nif}", nameof(GetThirdByNifAsync), "ValidateConfiguration:GestionaApiBaseUrl", nif);
            return Failure(GetThirdFailureKind.Configuration, "Gestiona API base URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _logger.LogWarning("({Method}) failed at step {Step} for nif {Nif}", nameof(GetThirdByNifAsync), "ValidateConfiguration:AccessToken", nif);
            return Failure(GetThirdFailureKind.Configuration, "Gestiona access token is not configured.");
        }

        if (string.IsNullOrWhiteSpace(nif))
        {
            _logger.LogWarning("({Method}) failed at step {Step}", nameof(GetThirdByNifAsync), "ValidateNif");
            return Failure(GetThirdFailureKind.Validation, "nif is required.");
        }

        var thirdIdResult = await _gestionaApiClient.GetThirdIdByNifAsync(
            gestionaApiBaseUrl,
            accessToken,
            nif,
            cancellationToken);

        if (!thirdIdResult.Success)
        {
            _logger.LogWarning("({Method}) failed at step {Step} for nif {Nif}", nameof(GetThirdByNifAsync), "GetThirdIdByNifFromGestiona", nif);
            var failureKind = thirdIdResult.StatusCode == 404
                ? GetThirdFailureKind.NotFound
                : GetThirdFailureKind.Upstream;
            return Failure(
                failureKind,
                $"Failed to get third id from Gestiona by nif: {nif}.",
                GetUpstreamErrorStatusCode(thirdIdResult.StatusCode));
        }

        if (string.IsNullOrWhiteSpace(thirdIdResult.Value))
        {
            _logger.LogWarning("({Method}) failed at step {Step} for nif {Nif}", nameof(GetThirdByNifAsync), "ResolveSingleThirdIdFromGestiona", nif);
            return Failure(
                GetThirdFailureKind.NotFound,
                $"No single Gestiona third was found for nif: {nif}.");
        }

        return await GetThirdAsync(
            thirdIdResult.Value,
            accessTokenOverride,
            cancellationToken);
    }

    private static Third MergeAddress(
        Third third,
        ThirdDefaultAddress? address)
    {
        if (address is null)
        {
            return third;
        }

        return third with
        {
            Address = address.Address,
            Number = address.Number,
            ZipCode = address.ZipCode,
            Province = address.Province,
            Country = address.Country,
            TypeOfRoad = address.TypeOfRoad
        };
    }

    private static GetThirdResult Failure(
        GetThirdFailureKind failureKind,
        string errorMessage,
        int? upstreamStatusCode = null)
    {
        return new GetThirdResult(false, failureKind, errorMessage, null, upstreamStatusCode);
    }

    private static int? GetUpstreamErrorStatusCode(int statusCode)
    {
        return statusCode >= 400
            ? statusCode
            : null;
    }

    private string? ResolveAccessToken(string? accessTokenOverride)
    {
        return string.IsNullOrWhiteSpace(accessTokenOverride)
            ? _gestionaOptions.AccessToken
            : accessTokenOverride;
    }
}
