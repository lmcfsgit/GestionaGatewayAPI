using GestionaGateway.Core.Models;
using GestionaGateway.Core.Services;
using GestionaGatewayAPI.Controllers;
using GestionaGatewayAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestionaGatewayAPI.Tests;

public sealed class QueuesControllerTests
{
    [Fact]
    public async Task GetConnectorMessages_WhenServiceSucceeds_ReturnsGatewayResponseWithMessages()
    {
        string? receivedAccessTokenOverride = "not-called";
        var controller = new QueuesController(
            new TestGestionaQueueService
            {
                GetConnectorMessagesAsyncHandler = (connectorName, accessTokenOverride, cancellationToken) =>
                {
                    receivedAccessTokenOverride = accessTokenOverride;
                    return Task.FromResult(new GetQueueConnectorMessagesResult(
                        true,
                        QueueFailureKind.None,
                        null,
                        [
                            new QueueConnectorMessageResult(
                                "e796e8b5-4f8a-4acd-9459-ee57acf8e383",
                                "2026-09-07 11:30:51")
                        ],
                        200));
                }
            },
            NullLogger<QueuesController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.Request.Headers["X-Gestiona-Access-Token"] = "header-token";

        var response = await controller.GetConnectorMessages(
            "sigma-medidata-file-docs",
            "operation-1",
            CancellationToken.None);

        var objectResult = Assert.IsType<OkObjectResult>(response.Result);
        var gatewayResponse = Assert.IsType<GatewayResponse>(objectResult.Value);
        Assert.Equal("operation-1", gatewayResponse.OperationId);
        Assert.True(gatewayResponse.Success);
        var result = Assert.IsType<QueueConnectorMessageResponse[]>(gatewayResponse.Result);
        var message = Assert.Single(result);
        Assert.Equal("e796e8b5-4f8a-4acd-9459-ee57acf8e383", message.MessageId);
        Assert.Equal("2026-09-07 11:30:51", message.DateSigned);
        Assert.Null(receivedAccessTokenOverride);
    }

    [Fact]
    public async Task GetConnectorMessage_WhenSubscriptionExists_ReturnsGatewayResponseWithMessage()
    {
        var controller = new QueuesController(
            new TestGestionaQueueService
            {
                GetConnectorMessageAsyncHandler = (connectorName, messageId, accessTokenOverride, cancellationToken) =>
                    Task.FromResult(new GetQueueConnectorMessageResult(
                        true,
                        QueueFailureKind.None,
                        null,
                        new QueueConnectorMessageResult(
                            "9376d53a-176e-4716-beaa-bd4486561bbd",
                            "2026-09-03 10:07:09"),
                        null))
            },
            NullLogger<QueuesController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var response = await controller.GetConnectorMessage(
            "sigma-medidata-file-docs",
            "message-1",
            "operation-1",
            CancellationToken.None);

        var objectResult = Assert.IsType<OkObjectResult>(response.Result);
        var gatewayResponse = Assert.IsType<GatewayResponse>(objectResult.Value);
        Assert.Equal("operation-1", gatewayResponse.OperationId);
        Assert.True(gatewayResponse.Success);
        var result = Assert.IsType<QueueConnectorMessageResponse>(gatewayResponse.Result);
        Assert.Equal("9376d53a-176e-4716-beaa-bd4486561bbd", result.MessageId);
        Assert.Equal("2026-09-03 10:07:09", result.DateSigned);
    }

    [Fact]
    public async Task GetConnectorMessage_WhenSubscriptionDoesNotExistButSubscribeSucceeds_ReturnsGatewayResponseWithMessage()
    {
        var controller = new QueuesController(
            new TestGestionaQueueService
            {
                GetConnectorMessageAsyncHandler = (connectorName, messageId, accessTokenOverride, cancellationToken) =>
                    Task.FromResult(new GetQueueConnectorMessageResult(
                        true,
                        QueueFailureKind.None,
                        null,
                        new QueueConnectorMessageResult(
                            "9376d53a-176e-4716-beaa-bd4486561bbd",
                            "2026-09-03 10:07:09"),
                        201))
            },
            NullLogger<QueuesController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var response = await controller.GetConnectorMessage(
            "sigma-medidata-file-docs",
            "message-1",
            "operation-1",
            CancellationToken.None);

        var objectResult = Assert.IsType<OkObjectResult>(response.Result);
        var gatewayResponse = Assert.IsType<GatewayResponse>(objectResult.Value);
        Assert.Equal("operation-1", gatewayResponse.OperationId);
        Assert.True(gatewayResponse.Success);
        var result = Assert.IsType<QueueConnectorMessageResponse>(gatewayResponse.Result);
        Assert.Equal("9376d53a-176e-4716-beaa-bd4486561bbd", result.MessageId);
        Assert.Equal("2026-09-03 10:07:09", result.DateSigned);
    }

    [Fact]
    public async Task GetConnectorMessage_IgnoresRequestAccessTokenHeader()
    {
        string? receivedAccessTokenOverride = "not-called";
        var controller = new QueuesController(
            new TestGestionaQueueService
            {
                GetConnectorMessageAsyncHandler = (connectorName, messageId, accessTokenOverride, cancellationToken) =>
                {
                    receivedAccessTokenOverride = accessTokenOverride;
                    return Task.FromResult(new GetQueueConnectorMessageResult(
                        true,
                        QueueFailureKind.None,
                        null,
                        new QueueConnectorMessageResult(
                            "9376d53a-176e-4716-beaa-bd4486561bbd",
                            "2026-09-03 10:07:09"),
                        null));
                }
            },
            NullLogger<QueuesController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.Request.Headers["X-Gestiona-Access-Token"] = "header-token";

        await controller.GetConnectorMessage(
            "sigma-medidata-file-docs",
            "message-1",
            "operation-1",
            CancellationToken.None);

        Assert.Null(receivedAccessTokenOverride);
    }

    [Fact]
    public async Task GetConnectorMessage_WhenConnectorDoesNotExist_ReturnsNotFoundGatewayError()
    {
        var controller = new QueuesController(
            new TestGestionaQueueService
            {
                GetConnectorMessageAsyncHandler = (connectorName, messageId, accessTokenOverride, cancellationToken) =>
                    Task.FromResult(new GetQueueConnectorMessageResult(
                        false,
                        QueueFailureKind.NotFound,
                        "No Gestiona connector was found.",
                        null,
                        null))
            },
            NullLogger<QueuesController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var response = await controller.GetConnectorMessage(
            "missing-connector",
            "message-1",
            "operation-1",
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        var gatewayResponse = Assert.IsType<GatewayResponse>(objectResult.Value);
        Assert.Equal("operation-1", gatewayResponse.OperationId);
        Assert.False(gatewayResponse.Success);
        var error = Assert.IsType<QueueError>(gatewayResponse.Result);
        Assert.Equal(StatusCodes.Status404NotFound, error.Code);
        Assert.Equal("Not Found", error.Name);
        Assert.Equal(QueueFailureKind.NotFound.ToString(), error.Kind);
        Assert.Equal("No Gestiona connector was found.", error.Message);
    }

    [Fact]
    public async Task GetConnectorMessage_WhenSubscribeFails_ReturnsGatewayErrorWithUpstreamDescription()
    {
        var controller = new QueuesController(
            new TestGestionaQueueService
            {
                GetConnectorMessageAsyncHandler = (connectorName, messageId, accessTokenOverride, cancellationToken) =>
                    Task.FromResult(new GetQueueConnectorMessageResult(
                        false,
                        QueueFailureKind.Upstream,
                        "already exists other consumer subscribed to queue",
                        null,
                        412))
            },
            NullLogger<QueuesController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var response = await controller.GetConnectorMessage(
            "sigma-medidata-file-docs",
            "message-1",
            "operation-1",
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, objectResult.StatusCode);
        var gatewayResponse = Assert.IsType<GatewayResponse>(objectResult.Value);
        Assert.False(gatewayResponse.Success);
        var error = Assert.IsType<QueueError>(gatewayResponse.Result);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, error.Code);
        Assert.Equal("Precondition Failed", error.Name);
        Assert.Equal(QueueFailureKind.Upstream.ToString(), error.Kind);
        Assert.Equal("already exists other consumer subscribed to queue", error.Message);
    }

    [Fact]
    public async Task SendConnectorResponse_WhenServiceSucceeds_ReturnsGatewayResponse()
    {
        GestionaGateway.Core.Models.QueueConnectorResponseRequest? receivedRequest = null;
        string? receivedAccessTokenOverride = "not-called";
        var controller = new QueuesController(
            new TestGestionaQueueService
            {
                SendConnectorResponseAsyncHandler = (connectorName, messageId, request, accessTokenOverride, cancellationToken) =>
                {
                    receivedRequest = request;
                    receivedAccessTokenOverride = accessTokenOverride;
                    return Task.FromResult(new SendQueueConnectorResponseResult(
                        true,
                        QueueFailureKind.None,
                        null,
                        new GestionaGateway.Core.Models.QueueConnectorResponseResult(
                            connectorName,
                            messageId),
                        200));
                }
            },
            NullLogger<QueuesController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.Request.Headers["X-Gestiona-Access-Token"] = "header-token";

        var response = await controller.SendConnectorResponse(
            "sigma-medidata-file-docs",
            "message-1",
            new SendQueueConnectorResponseRequest("FALSE", "Mensaje de respuesta al conector"),
            "operation-1",
            CancellationToken.None);

        var objectResult = Assert.IsType<OkObjectResult>(response.Result);
        var gatewayResponse = Assert.IsType<GatewayResponse>(objectResult.Value);
        Assert.Equal("operation-1", gatewayResponse.OperationId);
        Assert.True(gatewayResponse.Success);
        var result = Assert.IsType<GestionaGatewayAPI.Models.QueueConnectorResponseResult>(gatewayResponse.Result);
        Assert.Equal("sigma-medidata-file-docs", result.ConnectorName);
        Assert.Equal("message-1", result.MessageId);
        Assert.Equal("FALSE", receivedRequest!.ResultSuccess);
        Assert.Equal("Mensaje de respuesta al conector", receivedRequest.Message);
        Assert.Null(receivedAccessTokenOverride);
    }

    private sealed class TestGestionaQueueService : IGestionaQueueService
    {
        public Func<string, string?, CancellationToken, Task<GetQueueConnectorMessagesResult>>? GetConnectorMessagesAsyncHandler { get; init; }
        public Func<string, string, string?, CancellationToken, Task<GetQueueConnectorMessageResult>>? GetConnectorMessageAsyncHandler { get; init; }
        public Func<string, string, GestionaGateway.Core.Models.QueueConnectorResponseRequest, string?, CancellationToken, Task<SendQueueConnectorResponseResult>>? SendConnectorResponseAsyncHandler { get; init; }

        public Task<GetQueueConnectorMessagesResult> GetConnectorMessagesAsync(
            string connectorName,
            string? accessTokenOverride,
            CancellationToken cancellationToken)
        {
            if (GetConnectorMessagesAsyncHandler is null)
            {
                throw new InvalidOperationException("No test handler was configured.");
            }

            return GetConnectorMessagesAsyncHandler(connectorName, accessTokenOverride, cancellationToken);
        }

        public Task<GetQueueConnectorMessageResult> GetConnectorMessageAsync(
            string connectorName,
            string messageId,
            string? accessTokenOverride,
            CancellationToken cancellationToken)
        {
            if (GetConnectorMessageAsyncHandler is null)
            {
                throw new InvalidOperationException("No test handler was configured.");
            }

            return GetConnectorMessageAsyncHandler(connectorName, messageId, accessTokenOverride, cancellationToken);
        }

        public Task<SendQueueConnectorResponseResult> SendConnectorResponseAsync(
            string connectorName,
            string messageId,
            GestionaGateway.Core.Models.QueueConnectorResponseRequest request,
            string? accessTokenOverride,
            CancellationToken cancellationToken)
        {
            if (SendConnectorResponseAsyncHandler is null)
            {
                throw new InvalidOperationException("No test handler was configured.");
            }

            return SendConnectorResponseAsyncHandler(connectorName, messageId, request, accessTokenOverride, cancellationToken);
        }
    }
}
