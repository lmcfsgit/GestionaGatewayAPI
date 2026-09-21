using GestionaGateway.Core.Configuration;
using GestionaGateway.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestionaGateway.Core.Services;

public sealed class GestionaAddOnService : IGestionaAddOnService
{
    private readonly GestionaOptions _gestionaOptions;
    private readonly IGestionaApiClient _gestionaApiClient;
    private readonly ILogger<GestionaAddOnService> _logger;

    public GestionaAddOnService(
        IOptions<GestionaOptions> gestionaOptions,
        IGestionaApiClient gestionaApiClient,
        ILogger<GestionaAddOnService> logger)
    {
        _gestionaOptions = gestionaOptions.Value;
        _gestionaApiClient = gestionaApiClient;
        _logger = logger;
    }

    public async Task<CreateAddOnAuthorizationResult> CreateAuthorizationAsync(
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("({Method}) started.", nameof(CreateAuthorizationAsync));

        var gestionaApiBaseUrl = _gestionaOptions.GestionaApiBaseUrl;
        var addonToken = _gestionaOptions.AddonToken;

        if (string.IsNullOrWhiteSpace(gestionaApiBaseUrl))
        {
            return Failure(
                CreateAddOnAuthorizationFailureKind.Configuration,
                "Gestiona API base URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(addonToken))
        {
            return Failure(
                CreateAddOnAuthorizationFailureKind.Configuration,
                "Gestiona add-on token is not configured.");
        }

        var authorizationResult = await _gestionaApiClient.CreateAddOnAuthorizationAsync(
            gestionaApiBaseUrl,
            addonToken,
            cancellationToken);

        if (!authorizationResult.Success)
        {
            return Failure(
                CreateAddOnAuthorizationFailureKind.Upstream,
                "Failed to create add-on authorization in Gestiona.",
                GetUpstreamErrorStatusCode(authorizationResult.StatusCode));
        }

        var authId = GetLastLocationSegment(authorizationResult.Value);
        if (string.IsNullOrWhiteSpace(authId))
        {
            return Failure(
                CreateAddOnAuthorizationFailureKind.Upstream,
                "Gestiona add-on authorization response did not include a valid Location header.",
                GetUpstreamErrorStatusCode(authorizationResult.StatusCode));
        }

        return new CreateAddOnAuthorizationResult(
            true,
            CreateAddOnAuthorizationFailureKind.None,
            null,
            new AddOnAuthorization(authId),
            null);
    }

    public async Task<GetAddOnAuthorizationResult> GetAuthorizationAsync(
        string authId,
        CancellationToken cancellationToken)
    {
        var gestionaApiBaseUrl = _gestionaOptions.GestionaApiBaseUrl;
        var addonToken = _gestionaOptions.AddonToken;

        if (string.IsNullOrWhiteSpace(gestionaApiBaseUrl) || string.IsNullOrWhiteSpace(addonToken))
        {
            return GetFailure(
                CreateAddOnAuthorizationFailureKind.Configuration,
                "Gestiona API base URL or add-on token is not configured.");
        }

        var result = await _gestionaApiClient.GetAddOnAuthorizationAsync(
            gestionaApiBaseUrl,
            addonToken,
            authId,
            cancellationToken);

        if (!result.Success || result.Value is null)
        {
            return GetFailure(
                CreateAddOnAuthorizationFailureKind.Upstream,
                "Failed to get add-on authorization from Gestiona.",
                GetUpstreamErrorStatusCode(result.StatusCode));
        }

        if (result.StatusCode == (int)System.Net.HttpStatusCode.Unauthorized)
        {
            if (string.IsNullOrWhiteSpace(result.Value.AuthorizeUrl))
            {
                return GetFailure(
                    CreateAddOnAuthorizationFailureKind.Upstream,
                    "Pending Gestiona authorization response did not include a Location header.",
                    (int)System.Net.HttpStatusCode.BadGateway);
            }

            return GetSuccess(new AddOnAuthorizationStatus(false, result.Value.AuthorizeUrl, null));
        }

        var authorization = result.Value.Authorization;
        if (string.IsNullOrWhiteSpace(authorization?.UserId) ||
            string.IsNullOrWhiteSpace(authorization.AccessToken))
        {
            return GetFailure(
                CreateAddOnAuthorizationFailureKind.Upstream,
                "Gestiona authorization response did not include user_id and access_token.",
                (int)System.Net.HttpStatusCode.BadGateway);
        }

        return GetSuccess(new AddOnAuthorizationStatus(
            true,
            null,
            new AddOnAuthorizedInfo(authorization.UserId, authorization.AccessToken)));
    }

    private static GetAddOnAuthorizationResult GetSuccess(AddOnAuthorizationStatus status) =>
        new(true, CreateAddOnAuthorizationFailureKind.None, null, status, null);

    private static GetAddOnAuthorizationResult GetFailure(
        CreateAddOnAuthorizationFailureKind failureKind,
        string errorMessage,
        int? upstreamStatusCode = null) =>
        new(false, failureKind, errorMessage, null, upstreamStatusCode);

    private static CreateAddOnAuthorizationResult Failure(
        CreateAddOnAuthorizationFailureKind failureKind,
        string errorMessage,
        int? upstreamStatusCode = null)
    {
        return new CreateAddOnAuthorizationResult(false, failureKind, errorMessage, null, upstreamStatusCode);
    }

    private static int? GetUpstreamErrorStatusCode(int statusCode)
    {
        return statusCode >= 400
            ? statusCode
            : null;
    }

    private static string? GetLastLocationSegment(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return null;
        }

        return location
            .TrimEnd('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault();
    }
}
