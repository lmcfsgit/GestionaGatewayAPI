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
    public async Task<GestionaApiCallResult<string?>> CreateAddOnAuthorizationAsync(
        string gestionaApiBaseUrl,
        string addonToken,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Addon-Token", addonToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, AddOnAuthorizationsRoute)
        {
            Content = new ByteArrayContent([])
        };

        _logger.LogInformation(
            "({Method}) creating Gestiona add-on authorization via {RequestUri}",
            nameof(CreateAddOnAuthorizationAsync),
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);

        LogDeprecatedHeader(response, nameof(CreateAddOnAuthorizationAsync));
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) add-on authorization response: StatusCode={StatusCode}, Body={Body}",
            nameof(CreateAddOnAuthorizationAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed with status code {StatusCode}, Body={Body}",
                nameof(CreateAddOnAuthorizationAsync),
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<string?>((int)response.StatusCode, false, null);
        }

        var location = response.Headers.Location?.ToString();
        _logger.LogInformation(
            "({Method}) created Gestiona add-on authorization at {Location}",
            nameof(CreateAddOnAuthorizationAsync),
            location);

        return new GestionaApiCallResult<string?>((int)response.StatusCode, true, location);
    }

    public async Task<GestionaApiCallResult<GestionaAddOnAuthorizationStatus?>> GetAddOnAuthorizationAsync(
        string gestionaApiBaseUrl,
        string addonToken,
        string authId,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Addon-Token", addonToken);

        var route = $"{AddOnAuthorizationsRoute}/{Uri.EscapeDataString(authId)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        LogDeprecatedHeader(response, nameof(GetAddOnAuthorizationAsync));
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) add-on authorization status response: StatusCode={StatusCode}, Body={Body}",
            nameof(GetAddOnAuthorizationAsync),
            response.StatusCode,
            responseBody);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return new GestionaApiCallResult<GestionaAddOnAuthorizationStatus?>(
                (int)response.StatusCode,
                true,
                new GestionaAddOnAuthorizationStatus(response.Headers.Location?.ToString(), null));
        }

        if (!response.IsSuccessStatusCode)
        {
            return new GestionaApiCallResult<GestionaAddOnAuthorizationStatus?>((int)response.StatusCode, false, null);
        }

        var authorization = DeserializeResponse<GestionaAddOnAuthorization>(
            responseBody,
            nameof(GetAddOnAuthorizationAsync));
        return new GestionaApiCallResult<GestionaAddOnAuthorizationStatus?>(
            (int)response.StatusCode,
            authorization is not null,
            new GestionaAddOnAuthorizationStatus(null, authorization));
    }
}
