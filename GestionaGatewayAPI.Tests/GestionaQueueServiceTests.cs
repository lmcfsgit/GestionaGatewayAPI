using GestionaGateway.Core;
using GestionaGateway.Core.Configuration;
using GestionaGateway.Core.Models;
using GestionaGateway.Core.Services;
using GestionaGatewayAPI.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GestionaGatewayAPI.Tests;

public sealed class GestionaQueueServiceTests
{
    [Fact]
    public async Task GetConnectorMessagesAsync_WhenSubscriptionExists_ReturnsMessages()
    {
        string? requestedConnectorName = null;
        var apiClient = new TestGestionaApiClient
        {
            GetQueueConnectorsAsyncHandler = (baseUrl, token, cancellationToken) =>
                Task.FromResult(new GestionaApiCallResult<IReadOnlyList<QueueConnector>>(
                    200,
                    true,
                    [new QueueConnector("sigma-medidata-file-docs", "sigma-medidata-file-docs")])),
            GetQueueSubscriptionsAsyncHandler = (baseUrl, token, cancellationToken) =>
                Task.FromResult(new GestionaApiCallResult<IReadOnlyList<QueueSubscription>>(
                    200,
                    true,
                    [new QueueSubscription("connectors#sigma-medidata-file-docs")])),
            GetQueueConnectorMessagesAsyncHandler = (baseUrl, token, connectorName, cancellationToken) =>
            {
                requestedConnectorName = connectorName;
                return Task.FromResult(new GestionaApiCallResult<IReadOnlyList<QueueConnectorMessage>>(
                    200,
                    true,
                    [
                        new QueueConnectorMessage(
                            new QueueConnectorMessagePayload("e796e8b5-4f8a-4acd-9459-ee57acf8e383"),
                            "1788775851",
                            "1788776011")
                    ]));
            }
        };
        var service = CreateService(apiClient);

        var result = await service.GetConnectorMessagesAsync(
            "sigma-medidata-file-docs",
            accessTokenOverride: null,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("sigma-medidata-file-docs", requestedConnectorName);
        var message = Assert.Single(result.Messages);
        Assert.Equal("e796e8b5-4f8a-4acd-9459-ee57acf8e383", message.MessageId);
        Assert.Equal(DateTimeHelpers.FormatUnixTimestamp("1788775851"), message.DateSigned);
    }

    [Fact]
    public async Task GetConnectorMessageAsync_WhenSubscriptionExists_ReturnsMessage()
    {
        string? requestedMessageId = null;
        var apiClient = new TestGestionaApiClient
        {
            GetQueueConnectorsAsyncHandler = (baseUrl, token, cancellationToken) =>
                Task.FromResult(new GestionaApiCallResult<IReadOnlyList<QueueConnector>>(
                    200,
                    true,
                    [new QueueConnector("sigma-medidata-file-docs", "sigma-medidata-file-docs")])),
            GetQueueSubscriptionsAsyncHandler = (baseUrl, token, cancellationToken) =>
                Task.FromResult(new GestionaApiCallResult<IReadOnlyList<QueueSubscription>>(
                    200,
                    true,
                    [
                        new QueueSubscription("connectors#other"),
                        new QueueSubscription("connectors#sigma-medidata-file-docs")
                    ])),
            GetQueueConnectorMessageAsyncHandler = (baseUrl, token, connectorName, messageId, cancellationToken) =>
            {
                requestedMessageId = messageId;
                return Task.FromResult(new GestionaApiCallResult<QueueConnectorMessage?>(
                    200,
                    true,
                    new QueueConnectorMessage(
                        new QueueConnectorMessagePayload("9376d53a-176e-4716-beaa-bd4486561bbd"),
                        "1788444429",
                        "1788515019")));
            }
        };
        var service = CreateService(apiClient);

        var result = await service.GetConnectorMessageAsync(
            "sigma-medidata-file-docs",
            "message-1",
            accessTokenOverride: null,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("message-1", requestedMessageId);
        Assert.Equal("9376d53a-176e-4716-beaa-bd4486561bbd", result.Message!.MessageId);
        Assert.Equal(DateTimeHelpers.FormatUnixTimestamp("1788444429"), result.Message.DateSigned);
    }

    [Fact]
    public async Task GetConnectorMessageAsync_WhenSubscriptionIsMissing_SubscribesAndReturnsMessage()
    {
        string? subscribedConnectorName = null;
        string? requestedMessageId = null;
        var apiClient = new TestGestionaApiClient
        {
            GetQueueConnectorsAsyncHandler = (baseUrl, token, cancellationToken) =>
                Task.FromResult(new GestionaApiCallResult<IReadOnlyList<QueueConnector>>(
                    200,
                    true,
                    [new QueueConnector("sigma-medidata-file-docs", "sigma-medidata-file-docs")])),
            GetQueueSubscriptionsAsyncHandler = (baseUrl, token, cancellationToken) =>
                Task.FromResult(new GestionaApiCallResult<IReadOnlyList<QueueSubscription>>(
                    200,
                    true,
                    [new QueueSubscription("connectors#other")])),
            SubscribeQueueConnectorAsyncHandler = (baseUrl, token, connectorName, cancellationToken) =>
            {
                subscribedConnectorName = connectorName;
                return Task.FromResult(new GestionaApiCallResult<string?>(201, true, null));
            },
            GetQueueConnectorMessageAsyncHandler = (baseUrl, token, connectorName, messageId, cancellationToken) =>
            {
                requestedMessageId = messageId;
                return Task.FromResult(new GestionaApiCallResult<QueueConnectorMessage?>(
                    200,
                    true,
                    new QueueConnectorMessage(
                        new QueueConnectorMessagePayload("9376d53a-176e-4716-beaa-bd4486561bbd"),
                        "1788444429",
                        "1788515019")));
            }
        };
        var service = CreateService(apiClient);

        var result = await service.GetConnectorMessageAsync(
            "sigma-medidata-file-docs",
            "message-1",
            accessTokenOverride: null,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("sigma-medidata-file-docs", subscribedConnectorName);
        Assert.Equal("message-1", requestedMessageId);
        Assert.Equal("9376d53a-176e-4716-beaa-bd4486561bbd", result.Message!.MessageId);
        Assert.Equal(DateTimeHelpers.FormatUnixTimestamp("1788444429"), result.Message.DateSigned);
    }

    [Fact]
    public async Task GetConnectorMessageAsync_WhenConnectorIsMissing_ReturnsNotFoundWithoutCheckingSubscriptions()
    {
        var subscriptionWasRequested = false;
        var apiClient = new TestGestionaApiClient
        {
            GetQueueConnectorsAsyncHandler = (baseUrl, token, cancellationToken) =>
                Task.FromResult(new GestionaApiCallResult<IReadOnlyList<QueueConnector>>(
                    200,
                    true,
                    [new QueueConnector("other", "Other")])),
            GetQueueSubscriptionsAsyncHandler = (baseUrl, token, cancellationToken) =>
            {
                subscriptionWasRequested = true;
                return Task.FromResult(new GestionaApiCallResult<IReadOnlyList<QueueSubscription>>(
                    200,
                    true,
                    []));
            },
            SubscribeQueueConnectorAsyncHandler = (baseUrl, token, connectorName, cancellationToken) =>
            {
                throw new InvalidOperationException("Subscribe should not be called when connector is missing.");
            }
        };
        var service = CreateService(apiClient);

        var result = await service.GetConnectorMessageAsync(
            "sigma-medidata-file-docs",
            "message-1",
            accessTokenOverride: null,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(QueueFailureKind.NotFound, result.FailureKind);
        Assert.Contains("No Gestiona connector", result.ErrorMessage);
        Assert.False(subscriptionWasRequested);
    }

    [Fact]
    public async Task GetConnectorMessageAsync_WhenSubscribeFails_UsesUpstreamDescriptionAsErrorMessage()
    {
        var apiClient = new TestGestionaApiClient
        {
            GetQueueConnectorsAsyncHandler = (baseUrl, token, cancellationToken) =>
                Task.FromResult(new GestionaApiCallResult<IReadOnlyList<QueueConnector>>(
                    200,
                    true,
                    [new QueueConnector("sigma-medidata-file-docs", "sigma-medidata-file-docs")])),
            GetQueueSubscriptionsAsyncHandler = (baseUrl, token, cancellationToken) =>
                Task.FromResult(new GestionaApiCallResult<IReadOnlyList<QueueSubscription>>(
                    200,
                    true,
                    [])),
            SubscribeQueueConnectorAsyncHandler = (baseUrl, token, connectorName, cancellationToken) =>
                Task.FromResult(new GestionaApiCallResult<string?>(
                    412,
                    false,
                    "already exists other consumer subscribed to queue"))
        };
        var service = CreateService(apiClient);

        var result = await service.GetConnectorMessageAsync(
            "sigma-medidata-file-docs",
            "message-1",
            accessTokenOverride: null,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(QueueFailureKind.Upstream, result.FailureKind);
        Assert.Equal("already exists other consumer subscribed to queue", result.ErrorMessage);
        Assert.Equal(412, result.UpstreamStatusCode);
    }

    private static GestionaQueueService CreateService(TestGestionaApiClient apiClient)
    {
        return new GestionaQueueService(
            Options.Create(new GestionaOptions
            {
                GestionaApiBaseUrl = "https://gestiona.example/rest",
                AccessToken = "token"
            }),
            apiClient,
            NullLogger<GestionaQueueService>.Instance);
    }
}
