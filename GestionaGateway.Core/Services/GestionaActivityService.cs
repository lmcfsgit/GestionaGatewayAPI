using GestionaGateway.Core.Configuration;
using GestionaGateway.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestionaGateway.Core.Services;

public sealed class GestionaActivityService : IGestionaActivityService
{
    private readonly GestionaOptions _gestionaOptions;
    private readonly IGestionaApiClient _gestionaApiClient;
    private readonly ILogger<GestionaActivityService> _logger;

    public GestionaActivityService(
        IOptions<GestionaOptions> gestionaOptions,
        IGestionaApiClient gestionaApiClient,
        ILogger<GestionaActivityService> logger)
    {
        _gestionaOptions = gestionaOptions.Value;
        _gestionaApiClient = gestionaApiClient;
        _logger = logger;
    }

    public async Task<GetActivitiesResult> GetActivitiesAsync(
        string? accessTokenOverride,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("({Method}) started.", nameof(GetActivitiesAsync));

        var gestionaApiBaseUrl = _gestionaOptions.GestionaApiBaseUrl;
        var accessToken = GestionaAccessTokenResolver.Resolve(
            _gestionaOptions,
            accessTokenOverride,
            _logger);

        if (string.IsNullOrWhiteSpace(gestionaApiBaseUrl))
        {
            return Failure(
                GetActivitiesFailureKind.Configuration,
                "Gestiona API base URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Failure(
                GetActivitiesFailureKind.Configuration,
                "Gestiona access token is not configured.");
        }

        var activitiesResult = await _gestionaApiClient.GetActivitiesAsync(
            gestionaApiBaseUrl,
            accessToken,
            cancellationToken);

        if (!activitiesResult.Success)
        {
            return Failure(
                GetActivitiesFailureKind.Upstream,
                "Failed to get activities from Gestiona.",
                GetUpstreamErrorStatusCode(activitiesResult.StatusCode));
        }

        return new GetActivitiesResult(
            true,
            GetActivitiesFailureKind.None,
            null,
            activitiesResult.Value ?? [],
            null);
    }

    private static GetActivitiesResult Failure(
        GetActivitiesFailureKind failureKind,
        string errorMessage,
        int? upstreamStatusCode = null)
    {
        return new GetActivitiesResult(false, failureKind, errorMessage, null, upstreamStatusCode);
    }

    private static int? GetUpstreamErrorStatusCode(int statusCode)
    {
        return statusCode >= 400
            ? statusCode
            : null;
    }

    public async Task<GetProceduresResult> GetProceduresAsync(
        string activityId,
        string? accessTokenOverride,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("({Method}) started.", nameof(GetProceduresAsync));

        if (string.IsNullOrWhiteSpace(activityId))
        {
            return ProceduresFailure(
                GetActivitiesFailureKind.Upstream,
                "Activity id is required.");
        }

        var gestionaApiBaseUrl = _gestionaOptions.GestionaApiBaseUrl;
        var accessToken = GestionaAccessTokenResolver.Resolve(
            _gestionaOptions,
            accessTokenOverride,
            _logger);

        if (string.IsNullOrWhiteSpace(gestionaApiBaseUrl))
        {
            return ProceduresFailure(
                GetActivitiesFailureKind.Configuration,
                "Gestiona API base URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return ProceduresFailure(
                GetActivitiesFailureKind.Configuration,
                "Gestiona access token is not configured.");
        }

        var proceduresResult = await _gestionaApiClient.GetExternalProceduresAsync(
            gestionaApiBaseUrl,
            accessToken,
            activityId,
            cancellationToken);

        if (!proceduresResult.Success)
        {
            return ProceduresFailure(
                GetActivitiesFailureKind.Upstream,
                "Failed to get procedures from Gestiona.",
                GetUpstreamErrorStatusCode(proceduresResult.StatusCode));
        }

        var procedures = proceduresResult.Value?
            .Select(procedure => new Procedure(procedure.Id, procedure.Title, activityId))
            .ToArray() ?? [];

        return new GetProceduresResult(
            true,
            GetActivitiesFailureKind.None,
            null,
            procedures,
            null);
    }

    private static GetProceduresResult ProceduresFailure(
        GetActivitiesFailureKind failureKind,
        string errorMessage,
        int? upstreamStatusCode = null)
    {
        return new GetProceduresResult(false, failureKind, errorMessage, null, upstreamStatusCode);
    }
}
