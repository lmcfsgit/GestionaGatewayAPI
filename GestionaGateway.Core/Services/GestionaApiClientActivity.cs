using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using GestionaGateway.Core.Models;
using Microsoft.Extensions.Logging;

namespace GestionaGateway.Core.Services;

public sealed partial class GestionaApiClient
{
    /// <summary>
    /// Gets the activities available in the Gestiona catalog.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the available activities.</returns>
    public async Task<GestionaApiCallResult<IReadOnlyList<Activity>>> GetActivitiesAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        _logger.LogInformation(
            "({Method}) getting Gestiona activities via {RequestUri}",
            nameof(GetActivitiesAsync),
            new Uri(httpClient.BaseAddress, Catalog2015ProceduresRoute));

        return await GetPagedContentAsync<ActivitiesResponse, Activity>(
            httpClient,
            Catalog2015ProceduresRoute,
            nameof(GetActivitiesAsync),
            response => response.Content,
            response => response.Links,
            cancellationToken);
    }

    /// <summary>
    /// Gets the external procedures available for the specified Gestiona catalog activity.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="activityId">The Gestiona catalog activity identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the available external procedures.</returns>
    public async Task<GestionaApiCallResult<IReadOnlyList<ExternalProcedure>>> GetExternalProceduresAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string activityId,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = $"{Catalog2015ProceduresRoute}/{Uri.EscapeDataString(activityId)}/external-procedures";
        _logger.LogInformation(
            "({Method}) getting Gestiona external procedures for activity {ActivityId} via {RequestUri}",
            nameof(GetExternalProceduresAsync),
            activityId,
            new Uri(httpClient.BaseAddress, route));

        return await GetPagedContentAsync<ExternalProceduresResponse, ExternalProcedure>(
            httpClient,
            route,
            nameof(GetExternalProceduresAsync),
            response => response.Content,
            response => response.Links,
            cancellationToken);
    }
}
