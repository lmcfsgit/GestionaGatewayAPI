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
    /// Gets the active Gestiona queue subscriptions.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the active queue subscriptions.</returns>
    public async Task<GestionaApiCallResult<IReadOnlyList<QueueSubscription>>> GetQueueSubscriptionsAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, QueueSubscriptionsRoute);

        _logger.LogInformation(
            "({Method}) getting Gestiona queue subscriptions via {RequestUri}",
            nameof(GetQueueSubscriptionsAsync),
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        LogDeprecatedHeader(response, nameof(GetQueueSubscriptionsAsync));
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) queue subscriptions response: StatusCode={StatusCode}, Body={Body}",
            nameof(GetQueueSubscriptionsAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed with status code {StatusCode}, Body={Body}",
                nameof(GetQueueSubscriptionsAsync),
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<IReadOnlyList<QueueSubscription>>((int)response.StatusCode, false, []);
        }

        var responseModel = DeserializeResponse<QueueSubscriptionsResponse>(
            responseBody,
            nameof(GetQueueSubscriptionsAsync));

        return new GestionaApiCallResult<IReadOnlyList<QueueSubscription>>(
            (int)response.StatusCode,
            true,
            responseModel?.Content ?? []);
    }

    /// <summary>
    /// Gets the Gestiona queue connectors.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the available queue connectors.</returns>
    public async Task<GestionaApiCallResult<IReadOnlyList<QueueConnector>>> GetQueueConnectorsAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, ConnectorsRoute);

        _logger.LogInformation(
            "({Method}) getting Gestiona connectors via {RequestUri}",
            nameof(GetQueueConnectorsAsync),
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        LogDeprecatedHeader(response, nameof(GetQueueConnectorsAsync));
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) connectors response: StatusCode={StatusCode}, Body={Body}",
            nameof(GetQueueConnectorsAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed with status code {StatusCode}, Body={Body}",
                nameof(GetQueueConnectorsAsync),
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<IReadOnlyList<QueueConnector>>((int)response.StatusCode, false, []);
        }

        var responseModel = DeserializeResponse<QueueConnectorsResponse>(
            responseBody,
            nameof(GetQueueConnectorsAsync));

        return new GestionaApiCallResult<IReadOnlyList<QueueConnector>>(
            (int)response.StatusCode,
            true,
            responseModel?.Content ?? []);
    }

    /// <summary>
    /// Subscribes the current consumer to the specified Gestiona queue connector.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="connectorName">The queue connector name to subscribe to.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing an upstream response message when one is available.</returns>
    public async Task<GestionaApiCallResult<string?>> SubscribeQueueConnectorAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string connectorName,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = $"queues/connectors/{Uri.EscapeDataString(connectorName)}/subscription";
        using var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = new ByteArrayContent([])
        };

        _logger.LogInformation(
            "({Method}) subscribing Gestiona queue connector {ConnectorName} via {RequestUri}",
            nameof(SubscribeQueueConnectorAsync),
            connectorName,
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        LogDeprecatedHeader(response, nameof(SubscribeQueueConnectorAsync));
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) queue connector subscription response: StatusCode={StatusCode}, Body={Body}",
            nameof(SubscribeQueueConnectorAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed with status code {StatusCode}, Body={Body}",
                nameof(SubscribeQueueConnectorAsync),
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<string?>((int)response.StatusCode, false, GetResponseBodyDescription(responseBody));
        }

        return new GestionaApiCallResult<string?>((int)response.StatusCode, true, GetResponseBodyMessage(responseBody));
    }

    /// <summary>
    /// Gets a specific Gestiona queue connector message.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="connectorName">The queue connector name.</param>
    /// <param name="messageId">The queue message identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the queue connector message when found.</returns>
    public async Task<GestionaApiCallResult<QueueConnectorMessage?>> GetQueueConnectorMessageAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string connectorName,
        string messageId,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = $"queues/connectors/{Uri.EscapeDataString(connectorName)}/{Uri.EscapeDataString(messageId)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse(QueueMessageContentType));

        _logger.LogInformation(
            "({Method}) getting Gestiona queue connector message {MessageId} for connector {ConnectorName} via {RequestUri}",
            nameof(GetQueueConnectorMessageAsync),
            messageId,
            connectorName,
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        LogDeprecatedHeader(response, nameof(GetQueueConnectorMessageAsync));
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) queue connector message response: StatusCode={StatusCode}, Body={Body}",
            nameof(GetQueueConnectorMessageAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed with status code {StatusCode}, Body={Body}",
                nameof(GetQueueConnectorMessageAsync),
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<QueueConnectorMessage?>((int)response.StatusCode, false, null);
        }

        var responseModel = DeserializeResponse<QueueConnectorMessage>(
            responseBody,
            nameof(GetQueueConnectorMessageAsync));

        return new GestionaApiCallResult<QueueConnectorMessage?>(
            (int)response.StatusCode,
            true,
            responseModel);
    }

    /// <summary>
    /// Gets queued messages for the specified Gestiona queue connector.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="connectorName">The queue connector name.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the queued connector messages.</returns>
    public async Task<GestionaApiCallResult<IReadOnlyList<QueueConnectorMessage>>> GetQueueConnectorMessagesAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string connectorName,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = $"queues/connectors/{Uri.EscapeDataString(connectorName)}?max-messages=100&hold-seconds=1";
        using var request = new HttpRequestMessage(HttpMethod.Post, route);

        _logger.LogInformation(
            "({Method}) getting Gestiona queue connector messages for connector {ConnectorName} via {RequestUri}",
            nameof(GetQueueConnectorMessagesAsync),
            connectorName,
            new Uri(httpClient.BaseAddress, request.RequestUri!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        LogDeprecatedHeader(response, nameof(GetQueueConnectorMessagesAsync));
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) queue connector messages response: StatusCode={StatusCode}, Body={Body}",
            nameof(GetQueueConnectorMessagesAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed with status code {StatusCode}, Body={Body}",
                nameof(GetQueueConnectorMessagesAsync),
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<IReadOnlyList<QueueConnectorMessage>>((int)response.StatusCode, false, []);
        }

        var responseModel = DeserializeResponse<QueueConnectorMessagesResponse>(
            responseBody,
            nameof(GetQueueConnectorMessagesAsync));

        return new GestionaApiCallResult<IReadOnlyList<QueueConnectorMessage>>(
            (int)response.StatusCode,
            true,
            responseModel?.Content ?? []);
    }

    /// <summary>
    /// Sends a response for a Gestiona queue connector message.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on the request headers.</param>
    /// <param name="connectorName">The queue connector name.</param>
    /// <param name="messageId">The queue message identifier.</param>
    /// <param name="request">The connector response payload.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing an upstream response message when one is available.</returns>
    public async Task<GestionaApiCallResult<string?>> SendQueueConnectorResponseAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string connectorName,
        string messageId,
        QueueConnectorResponseRequest request,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(NormalizeBaseUrl(gestionaApiBaseUrl), UriKind.Absolute);
        httpClient.DefaultRequestHeaders.Add("X-Gestiona-Access-Token", accessToken);

        var route = $"queues/connectors/{Uri.EscapeDataString(connectorName)}/{Uri.EscapeDataString(messageId)}";
        var serializedPayload = JsonSerializer.Serialize(
            request,
            IgnoreNullJsonSerializerOptions);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = new StringContent(serializedPayload, Encoding.UTF8)
        };
        httpRequest.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(QueueConnectorResponseContentType);

        _logger.LogInformation(
            "({Method}) sending Gestiona queue connector response for message {MessageId}, connector {ConnectorName} via {RequestUri}",
            nameof(SendQueueConnectorResponseAsync),
            messageId,
            connectorName,
            new Uri(httpClient.BaseAddress, httpRequest.RequestUri!));

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

        LogDeprecatedHeader(response, nameof(SendQueueConnectorResponseAsync));
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        _logger.LogDebug(
            "({Method}) queue connector response result: StatusCode={StatusCode}, Body={Body}",
            nameof(SendQueueConnectorResponseAsync),
            response.StatusCode,
            responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "({Method}) failed with status code {StatusCode}, Body={Body}",
                nameof(SendQueueConnectorResponseAsync),
                response.StatusCode,
                FormatJsonForLog(responseBody));
            return new GestionaApiCallResult<string?>((int)response.StatusCode, false, GetResponseBodyDescription(responseBody));
        }

        return new GestionaApiCallResult<string?>((int)response.StatusCode, true, GetResponseBodyMessage(responseBody));
    }
}
