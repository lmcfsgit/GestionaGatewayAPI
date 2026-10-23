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
    /// Gets a third from Gestiona.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="thirdId">The identifier of the third to retrieve.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the third when the request succeeds.</returns>
    public async Task<GestionaApiCallResult<Third?>> GetThirdAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string thirdId,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = $"{ThirdsRoute}/{Uri.EscapeDataString(thirdId)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, route);

        _logger.LogInformation(
            "({Method}) getting Gestiona third {ThirdId} via {RequestUri}",
            nameof(GetThirdAsync),
            thirdId,
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        LogDeprecatedHeader(response, nameof(GetThirdAsync));
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) third response: StatusCode={StatusCode}, Body={Body}",
            nameof(GetThirdAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed for third {ThirdId} with status code {StatusCode}, Body={Body}",
                nameof(GetThirdAsync),
                thirdId,
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<Third?>((int)response.StatusCode, false, null);
        }

        if (string.IsNullOrWhiteSpace(responseBody) ||
            responseBody is "<empty>" or "<no content>")
        {
            return new GestionaApiCallResult<Third?>((int)response.StatusCode, true, null);
        }

        try
        {
            var third = JsonSerializer.Deserialize<Third>(responseBody);
            return new GestionaApiCallResult<Third?>((int)response.StatusCode, true, third);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "({Method}) failed to parse Gestiona third response body.", nameof(GetThirdAsync));
            return new GestionaApiCallResult<Third?>((int)response.StatusCode, false, null);
        }
    }

    /// <summary>
    /// Resolves a Gestiona third identifier from a NIF.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="nif">The NIF used to filter thirds.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the resolved third identifier when found.</returns>
    public async Task<GestionaApiCallResult<string?>> GetThirdIdByNifAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string nif,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var serializedPayload = JsonSerializer.Serialize(new { nif });
        _logger.LogDebug(
            "({Method}) Gestiona request body:{NewLine}{RequestBody}",
            nameof(GetThirdIdByNifAsync),
            Environment.NewLine,
            FormatJsonForLog(serializedPayload));

        using var request = new HttpRequestMessage(HttpMethod.Get, ThirdsRoute)
        {
            Content = new StringContent(
                serializedPayload,
                Encoding.UTF8)
        };
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(ThirdsFilterContentType);

        _logger.LogInformation(
            "({Method}) resolving Gestiona third id by NIF via {RequestUri}",
            nameof(GetThirdIdByNifAsync),
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        LogDeprecatedHeader(response, nameof(GetThirdIdByNifAsync));
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) third filter response: StatusCode={StatusCode}, Body={Body}",
            nameof(GetThirdIdByNifAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed with status code {StatusCode}, Body={Body}",
                nameof(GetThirdIdByNifAsync),
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<string?>((int)response.StatusCode, false, null);
        }

        if (string.IsNullOrWhiteSpace(responseBody) ||
            responseBody is "<empty>" or "<no content>")
        {
            return new GestionaApiCallResult<string?>((int)response.StatusCode, true, null);
        }

        try
        {
            var thirdFilter = JsonSerializer.Deserialize<ThirdFilterResponse>(responseBody);
            var thirdId = ResolveSingleThirdId(thirdFilter);
            return new GestionaApiCallResult<string?>((int)response.StatusCode, true, thirdId);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "({Method}) failed to parse Gestiona third filter response body.", nameof(GetThirdIdByNifAsync));
            return new GestionaApiCallResult<string?>((int)response.StatusCode, false, null);
        }
    }

    /// <summary>
    /// Gets the default address for a third from Gestiona.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="thirdId">The identifier of the third whose default address should be retrieved.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the third default address when the request succeeds.</returns>
    public async Task<GestionaApiCallResult<ThirdDefaultAddress?>> GetThirdDefaultAddressAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string thirdId,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = $"{ThirdsRoute}/{Uri.EscapeDataString(thirdId)}/default-address";
        using var request = new HttpRequestMessage(HttpMethod.Get, route);

        _logger.LogInformation(
            "({Method}) getting Gestiona third default address {ThirdId} via {RequestUri}",
            nameof(GetThirdDefaultAddressAsync),
            thirdId,
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        LogDeprecatedHeader(response, nameof(GetThirdDefaultAddressAsync));
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) third default address response: StatusCode={StatusCode}, Body={Body}",
            nameof(GetThirdDefaultAddressAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed for third {ThirdId} with status code {StatusCode}, Body={Body}",
                nameof(GetThirdDefaultAddressAsync),
                thirdId,
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<ThirdDefaultAddress?>((int)response.StatusCode, false, null);
        }

        if (string.IsNullOrWhiteSpace(responseBody) ||
            responseBody is "<empty>" or "<no content>")
        {
            return new GestionaApiCallResult<ThirdDefaultAddress?>((int)response.StatusCode, true, null);
        }

        try
        {
            var address = JsonSerializer.Deserialize<ThirdDefaultAddress>(responseBody);

            return new GestionaApiCallResult<ThirdDefaultAddress?>((int)response.StatusCode, true, address);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "({Method}) failed to parse Gestiona third default address response body.", nameof(GetThirdDefaultAddressAsync));
            return new GestionaApiCallResult<ThirdDefaultAddress?>((int)response.StatusCode, false, null);
        }
    }
}
