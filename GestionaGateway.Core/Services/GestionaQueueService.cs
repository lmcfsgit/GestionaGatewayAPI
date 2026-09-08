using GestionaGateway.Core.Configuration;
using GestionaGateway.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestionaGateway.Core.Services;

/// <summary>
/// Provides gateway-level queue connector operations backed by the Gestiona API.
/// </summary>
public sealed class GestionaQueueService : IGestionaQueueService
{
    private readonly GestionaOptions _gestionaOptions;
    private readonly IGestionaApiClient _gestionaApiClient;
    private readonly ILogger<GestionaQueueService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GestionaQueueService"/> class.
    /// </summary>
    /// <param name="gestionaOptions">The Gestiona API configuration options.</param>
    /// <param name="gestionaApiClient">The client used to call the upstream Gestiona API.</param>
    /// <param name="logger">The logger used for operation tracing and diagnostics.</param>
    public GestionaQueueService(
        IOptions<GestionaOptions> gestionaOptions,
        IGestionaApiClient gestionaApiClient,
        ILogger<GestionaQueueService> logger)
    {
        _gestionaOptions = gestionaOptions.Value;
        _gestionaApiClient = gestionaApiClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets queued messages for the specified connector, subscribing to the queue first when needed.
    /// </summary>
    /// <param name="connectorName">The connector queue name.</param>
    /// <param name="accessTokenOverride">An optional access token override. When null, the configured Gestiona access token is used.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The service result containing mapped queue messages or a gateway failure description.</returns>
    public async Task<GetQueueConnectorMessagesResult> GetConnectorMessagesAsync(
        string connectorName,
        string? accessTokenOverride,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "({Method}) started. ConnectorName={ConnectorName}",
            nameof(GetConnectorMessagesAsync),
            connectorName);

        if (string.IsNullOrWhiteSpace(connectorName))
        {
            return MessagesFailure(QueueFailureKind.Validation, "connectorName is required.");
        }

        var gestionaApiBaseUrl = _gestionaOptions.GestionaApiBaseUrl;
        var accessToken = GestionaAccessTokenResolver.Resolve(
            _gestionaOptions,
            accessTokenOverride,
            _logger);

        if (string.IsNullOrWhiteSpace(gestionaApiBaseUrl))
        {
            return MessagesFailure(QueueFailureKind.Configuration, "Gestiona API base URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return MessagesFailure(QueueFailureKind.Configuration, "Gestiona access token is not configured.");
        }

        // Validate the connector before touching subscriptions or queue messages.
        var connectorsResult = await _gestionaApiClient.GetQueueConnectorsAsync(
            gestionaApiBaseUrl,
            accessToken,
            cancellationToken);

        if (!connectorsResult.Success)
        {
            return MessagesFailure(
                QueueFailureKind.Upstream,
                "Failed to get connectors from Gestiona.",
                GetUpstreamErrorStatusCode(connectorsResult.StatusCode));
        }

        var connectorExists = connectorsResult.Value?.Any(item =>
            string.Equals(item.Code, connectorName, StringComparison.Ordinal)) == true;
        if (!connectorExists)
        {
            return MessagesFailure(
                QueueFailureKind.NotFound,
                $"No Gestiona connector was found for connector name: {connectorName}.");
        }

        // Gestiona requires an active connector subscription before queue messages can be pulled.
        var subscriptionsResult = await _gestionaApiClient.GetQueueSubscriptionsAsync(
            gestionaApiBaseUrl,
            accessToken,
            cancellationToken);

        if (!subscriptionsResult.Success)
        {
            return MessagesFailure(
                QueueFailureKind.Upstream,
                "Failed to get queue subscriptions from Gestiona.",
                GetUpstreamErrorStatusCode(subscriptionsResult.StatusCode));
        }

        var expectedName = $"connectors#{connectorName}";
        var subscription = subscriptionsResult.Value?.FirstOrDefault(item =>
            string.Equals(item.Name, expectedName, StringComparison.Ordinal));

        // If this gateway consumer is not subscribed yet, subscribe before polling the queue.
        if (subscription is null)
        {
            var subscribeResult = await _gestionaApiClient.SubscribeQueueConnectorAsync(
                gestionaApiBaseUrl,
                accessToken,
                connectorName,
                cancellationToken);

            if (!subscribeResult.Success)
            {
                return MessagesFailure(
                    QueueFailureKind.Upstream,
                    subscribeResult.Value ?? $"Failed to subscribe Gestiona queue connector: {connectorName}.",
                    GetUpstreamErrorStatusCode(subscribeResult.StatusCode));
            }
        }

        // Poll up to the client-defined maximum and map each Gestiona message to the gateway shape.
        var messagesResult = await _gestionaApiClient.GetQueueConnectorMessagesAsync(
            gestionaApiBaseUrl,
            accessToken,
            connectorName,
            cancellationToken);

        if (!messagesResult.Success)
        {
            return MessagesFailure(
                QueueFailureKind.Upstream,
                $"Failed to get queue connector messages for connector: {connectorName}.",
                GetUpstreamErrorStatusCode(messagesResult.StatusCode));
        }

        var messages = messagesResult.Value?
            .Select(message => new QueueConnectorMessageResult(
                message.Payload?.Target,
                FormatQueueMessageTimestamp(message.Entry)))
            .ToArray() ?? [];

        return new GetQueueConnectorMessagesResult(
            true,
            QueueFailureKind.None,
            null,
            messages,
            messagesResult.StatusCode);
    }

    /// <summary>
    /// Gets a specific queued message for the specified connector, subscribing to the queue first when needed.
    /// </summary>
    /// <param name="connectorName">The connector queue name.</param>
    /// <param name="messageId">The queued message identifier.</param>
    /// <param name="accessTokenOverride">An optional access token override. When null, the configured Gestiona access token is used.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The service result containing the mapped queue message or a gateway failure description.</returns>
    public async Task<GetQueueConnectorMessageResult> GetConnectorMessageAsync(
        string connectorName,
        string messageId,
        string? accessTokenOverride,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "({Method}) started. ConnectorName={ConnectorName}",
            nameof(GetConnectorMessageAsync),
            connectorName);

        if (string.IsNullOrWhiteSpace(connectorName))
        {
            return Failure(QueueFailureKind.Validation, "connectorName is required.");
        }

        if (string.IsNullOrWhiteSpace(messageId))
        {
            return Failure(QueueFailureKind.Validation, "messageId is required.");
        }

        var gestionaApiBaseUrl = _gestionaOptions.GestionaApiBaseUrl;
        var accessToken = GestionaAccessTokenResolver.Resolve(
            _gestionaOptions,
            accessTokenOverride,
            _logger);

        if (string.IsNullOrWhiteSpace(gestionaApiBaseUrl))
        {
            return Failure(QueueFailureKind.Configuration, "Gestiona API base URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Failure(QueueFailureKind.Configuration, "Gestiona access token is not configured.");
        }

        var connectorsResult = await _gestionaApiClient.GetQueueConnectorsAsync(
            gestionaApiBaseUrl,
            accessToken,
            cancellationToken);

        if (!connectorsResult.Success)
        {
            return Failure(
                QueueFailureKind.Upstream,
                "Failed to get connectors from Gestiona.",
                GetUpstreamErrorStatusCode(connectorsResult.StatusCode));
        }

        var connectorExists = connectorsResult.Value?.Any(item =>
            string.Equals(item.Code, connectorName, StringComparison.Ordinal)) == true;
        if (!connectorExists)
        {
            return Failure(
                QueueFailureKind.NotFound,
                $"No Gestiona connector was found for connector name: {connectorName}.");
        }

        var subscriptionsResult = await _gestionaApiClient.GetQueueSubscriptionsAsync(
            gestionaApiBaseUrl,
            accessToken,
            cancellationToken);

        if (!subscriptionsResult.Success)
        {
            return Failure(
                QueueFailureKind.Upstream,
                "Failed to get queue subscriptions from Gestiona.",
                GetUpstreamErrorStatusCode(subscriptionsResult.StatusCode));
        }

        var expectedName = $"connectors#{connectorName}";
        var subscription = subscriptionsResult.Value?.FirstOrDefault(item =>
            string.Equals(item.Name, expectedName, StringComparison.Ordinal));

        if (subscription is not null)
        {
            return await GetMessageAsync(
                gestionaApiBaseUrl,
                accessToken,
                connectorName,
                messageId,
                subscriptionsResult.StatusCode,
                cancellationToken);
        }

        var subscribeResult = await _gestionaApiClient.SubscribeQueueConnectorAsync(
            gestionaApiBaseUrl,
            accessToken,
            connectorName,
            cancellationToken);

        if (!subscribeResult.Success)
        {
            return Failure(
                QueueFailureKind.Upstream,
                subscribeResult.Value ?? $"Failed to subscribe Gestiona queue connector: {connectorName}.",
                GetUpstreamErrorStatusCode(subscribeResult.StatusCode));
        }

        return await GetMessageAsync(
            gestionaApiBaseUrl,
            accessToken,
            connectorName,
            messageId,
            subscribeResult.StatusCode,
            cancellationToken);
    }

    /// <summary>
    /// Gets a specific queued message from Gestiona after connector and subscription checks have completed.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent on upstream requests.</param>
    /// <param name="connectorName">The connector queue name.</param>
    /// <param name="messageId">The queued message identifier.</param>
    /// <param name="subscriptionStatusCode">The status code returned by the subscription check or create operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The service result containing the mapped queue message or a gateway failure description.</returns>
    private async Task<GetQueueConnectorMessageResult> GetMessageAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string connectorName,
        string messageId,
        int subscriptionStatusCode,
        CancellationToken cancellationToken)
    {
        var messageResult = await _gestionaApiClient.GetQueueConnectorMessageAsync(
            gestionaApiBaseUrl,
            accessToken,
            connectorName,
            messageId,
            cancellationToken);

        if (!messageResult.Success || messageResult.Value is null)
        {
            var failureKind = messageResult.StatusCode == 404
                ? QueueFailureKind.NotFound
                : QueueFailureKind.Upstream;
            return Failure(
                failureKind,
                $"Failed to get queue connector message: {messageId}.",
                GetUpstreamErrorStatusCode(messageResult.StatusCode));
        }

        return new GetQueueConnectorMessageResult(
            true,
            QueueFailureKind.None,
            null,
            new QueueConnectorMessageResult(
                messageResult.Value.Payload?.Target,
                FormatQueueMessageTimestamp(messageResult.Value.Entry)),
            messageResult.StatusCode >= 200 ? messageResult.StatusCode : subscriptionStatusCode);
    }

    /// <summary>
    /// Sends a response for a specific queued connector message, subscribing to the queue first when needed.
    /// </summary>
    /// <param name="connectorName">The connector queue name.</param>
    /// <param name="messageId">The queued message identifier.</param>
    /// <param name="request">The connector response payload sent to Gestiona.</param>
    /// <param name="accessTokenOverride">An optional access token override. When null, the configured Gestiona access token is used.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The service result containing the sent response summary or a gateway failure description.</returns>
    public async Task<SendQueueConnectorResponseResult> SendConnectorResponseAsync(
        string connectorName,
        string messageId,
        QueueConnectorResponseRequest request,
        string? accessTokenOverride,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "({Method}) started. ConnectorName={ConnectorName}, MessageId={MessageId}",
            nameof(SendConnectorResponseAsync),
            connectorName,
            messageId);

        if (string.IsNullOrWhiteSpace(connectorName))
        {
            return SendFailure(QueueFailureKind.Validation, "connectorName is required.");
        }

        if (string.IsNullOrWhiteSpace(messageId))
        {
            return SendFailure(QueueFailureKind.Validation, "messageId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ResultSuccess))
        {
            return SendFailure(QueueFailureKind.Validation, "result_success is required.");
        }

        var gestionaApiBaseUrl = _gestionaOptions.GestionaApiBaseUrl;
        var accessToken = GestionaAccessTokenResolver.Resolve(
            _gestionaOptions,
            accessTokenOverride,
            _logger);

        if (string.IsNullOrWhiteSpace(gestionaApiBaseUrl))
        {
            return SendFailure(QueueFailureKind.Configuration, "Gestiona API base URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return SendFailure(QueueFailureKind.Configuration, "Gestiona access token is not configured.");
        }

        var connectorsResult = await _gestionaApiClient.GetQueueConnectorsAsync(
            gestionaApiBaseUrl,
            accessToken,
            cancellationToken);

        if (!connectorsResult.Success)
        {
            return SendFailure(
                QueueFailureKind.Upstream,
                "Failed to get connectors from Gestiona.",
                GetUpstreamErrorStatusCode(connectorsResult.StatusCode));
        }

        var connectorExists = connectorsResult.Value?.Any(item =>
            string.Equals(item.Code, connectorName, StringComparison.Ordinal)) == true;
        if (!connectorExists)
        {
            return SendFailure(
                QueueFailureKind.NotFound,
                $"No Gestiona connector was found for connector name: {connectorName}.");
        }

        var subscriptionsResult = await _gestionaApiClient.GetQueueSubscriptionsAsync(
            gestionaApiBaseUrl,
            accessToken,
            cancellationToken);

        if (!subscriptionsResult.Success)
        {
            return SendFailure(
                QueueFailureKind.Upstream,
                "Failed to get queue subscriptions from Gestiona.",
                GetUpstreamErrorStatusCode(subscriptionsResult.StatusCode));
        }

        var expectedName = $"connectors#{connectorName}";
        var subscription = subscriptionsResult.Value?.FirstOrDefault(item =>
            string.Equals(item.Name, expectedName, StringComparison.Ordinal));

        if (subscription is null)
        {
            var subscribeResult = await _gestionaApiClient.SubscribeQueueConnectorAsync(
                gestionaApiBaseUrl,
                accessToken,
                connectorName,
                cancellationToken);

            if (!subscribeResult.Success)
            {
                return SendFailure(
                    QueueFailureKind.Upstream,
                    subscribeResult.Value ?? $"Failed to subscribe Gestiona queue connector: {connectorName}.",
                    GetUpstreamErrorStatusCode(subscribeResult.StatusCode));
            }
        }

        var responseResult = await _gestionaApiClient.SendQueueConnectorResponseAsync(
            gestionaApiBaseUrl,
            accessToken,
            connectorName,
            messageId,
            request,
            cancellationToken);

        if (!responseResult.Success)
        {
            return SendFailure(
                responseResult.StatusCode == 404 ? QueueFailureKind.NotFound : QueueFailureKind.Upstream,
                responseResult.Value ?? $"Failed to send queue connector response for message: {messageId}.",
                GetUpstreamErrorStatusCode(responseResult.StatusCode));
        }

        return new SendQueueConnectorResponseResult(
            true,
            QueueFailureKind.None,
            null,
            new QueueConnectorResponseResult(connectorName, messageId),
            responseResult.StatusCode);
    }

    /// <summary>
    /// Formats a queue message Unix timestamp using the shared gateway date-time helper.
    /// </summary>
    /// <param name="unixTimestamp">The Unix timestamp value returned by Gestiona.</param>
    /// <returns>The formatted local date-time string, or the original value when it is blank.</returns>
    private static string? FormatQueueMessageTimestamp(string? unixTimestamp)
    {
        return string.IsNullOrWhiteSpace(unixTimestamp)
            ? unixTimestamp
            : DateTimeHelpers.FormatUnixTimestamp(unixTimestamp);
    }

    /// <summary>
    /// Creates a failed single-message result.
    /// </summary>
    /// <param name="failureKind">The gateway failure classification.</param>
    /// <param name="errorMessage">The human-readable error message.</param>
    /// <param name="upstreamStatusCode">The optional upstream HTTP status code.</param>
    /// <returns>A failed single-message service result.</returns>
    private static GetQueueConnectorMessageResult Failure(
        QueueFailureKind failureKind,
        string errorMessage,
        int? upstreamStatusCode = null)
    {
        return new GetQueueConnectorMessageResult(false, failureKind, errorMessage, null, upstreamStatusCode);
    }

    /// <summary>
    /// Creates a failed multi-message result.
    /// </summary>
    /// <param name="failureKind">The gateway failure classification.</param>
    /// <param name="errorMessage">The human-readable error message.</param>
    /// <param name="upstreamStatusCode">The optional upstream HTTP status code.</param>
    /// <returns>A failed multi-message service result.</returns>
    private static GetQueueConnectorMessagesResult MessagesFailure(
        QueueFailureKind failureKind,
        string errorMessage,
        int? upstreamStatusCode = null)
    {
        return new GetQueueConnectorMessagesResult(false, failureKind, errorMessage, [], upstreamStatusCode);
    }

    /// <summary>
    /// Creates a failed connector-response result.
    /// </summary>
    /// <param name="failureKind">The gateway failure classification.</param>
    /// <param name="errorMessage">The human-readable error message.</param>
    /// <param name="upstreamStatusCode">The optional upstream HTTP status code.</param>
    /// <returns>A failed connector-response service result.</returns>
    private static SendQueueConnectorResponseResult SendFailure(
        QueueFailureKind failureKind,
        string errorMessage,
        int? upstreamStatusCode = null)
    {
        return new SendQueueConnectorResponseResult(false, failureKind, errorMessage, null, upstreamStatusCode);
    }

    /// <summary>
    /// Returns the upstream status code only when it represents an error.
    /// </summary>
    /// <param name="statusCode">The status code returned by Gestiona.</param>
    /// <returns>The upstream error status code, or null when the status code is not an error.</returns>
    private static int? GetUpstreamErrorStatusCode(int statusCode)
    {
        return statusCode >= 400
            ? statusCode
            : null;
    }
}
