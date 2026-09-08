using GestionaGatewayAPI.Models;
using GestionaGateway.Core.Models;
using GestionaGateway.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace GestionaGatewayAPI.Controllers;

/// <summary>
/// Provides queue connector operations.
/// </summary>
[ApiController]
[Route("queues")]
public sealed class QueuesController : ControllerBase
{
    private const int NoActiveSubscriptionCode = 1001;
    private const string ConnectorResponseContentType = "application/vnd.gestiona.connector-response+json";

    private readonly IGestionaQueueService _gestionaQueueService;
    private readonly ILogger<QueuesController> _logger;

    public QueuesController(
        IGestionaQueueService gestionaQueueService,
        ILogger<QueuesController> logger)
    {
        _gestionaQueueService = gestionaQueueService;
        _logger = logger;
    }

    /// <summary>
    /// Gets queued connector messages.
    /// </summary>
    /// <param name="connectorName">The connector name associated with the queued messages.</param>
    /// <param name="operationId">An optional operation identifier echoed back in the response envelope.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A gateway response envelope containing queued messages, or an error payload.</returns>
    [HttpGet("connectors/{connector_name}")]
    public async Task<ActionResult<GatewayResponse>> GetConnectorMessages(
        [FromRoute(Name = "connector_name")] string connectorName,
        [FromQuery(Name = "operationId")] string? operationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "{Method} received queue connector messages request for connector {ConnectorName} with operationId {OperationId}",
            nameof(GetConnectorMessages),
            connectorName,
            operationId);

        if (string.IsNullOrWhiteSpace(connectorName))
        {
            return CreateQueueErrorResponse(
                operationId,
                StatusCodes.Status400BadRequest,
                QueueFailureKind.Validation,
                "connector_name route parameter is required.");
        }

        if (string.Equals(connectorName, "{{connector_name}}", StringComparison.Ordinal))
        {
            return CreateQueueErrorResponse(
                operationId,
                StatusCodes.Status400BadRequest,
                QueueFailureKind.Validation,
                "connector_name route parameter contains an unresolved variable.");
        }

        var result = await _gestionaQueueService.GetConnectorMessagesAsync(
            connectorName,
            accessTokenOverride: null,
            cancellationToken);

        if (!result.Success)
        {
            var statusCode = result.FailureKind switch
            {
                QueueFailureKind.Configuration => StatusCodes.Status500InternalServerError,
                QueueFailureKind.Validation => StatusCodes.Status400BadRequest,
                QueueFailureKind.NotFound => StatusCodes.Status404NotFound,
                _ => result.UpstreamStatusCode ?? StatusCodes.Status502BadGateway
            };

            return CreateQueueErrorResponse(
                operationId,
                statusCode,
                result.FailureKind,
                result.ErrorMessage ?? "Unknown error.");
        }

        var messages = result.Messages
            .Select(message => new QueueConnectorMessageResponse(
                message.MessageId,
                message.DateSigned))
            .ToArray();

        return Ok(new GatewayResponse(
            operationId,
            true,
            messages));
    }

    /// <summary>
    /// Gets a queued connector message.
    /// </summary>
    /// <param name="connectorName">The connector name associated with the queued message.</param>
    /// <param name="messageId">The queued message identifier.</param>
    /// <param name="operationId">An optional operation identifier echoed back in the response envelope.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A gateway response envelope containing the active subscription name, or an error payload when none exists.</returns>
    [HttpGet("connectors/{connector_name}/{message_id}")]
    public async Task<ActionResult<GatewayResponse>> GetConnectorMessage(
        [FromRoute(Name = "connector_name")] string connectorName,
        [FromRoute(Name = "message_id")] string messageId,
        [FromQuery(Name = "operationId")] string? operationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "{Method} received queue connector request for connector {ConnectorName}, message {MessageId} with operationId {OperationId}",
            nameof(GetConnectorMessage),
            connectorName,
            messageId,
            operationId);

        if (string.IsNullOrWhiteSpace(connectorName))
        {
            return CreateQueueErrorResponse(
                operationId,
                StatusCodes.Status400BadRequest,
                QueueFailureKind.Validation,
                "connector_name route parameter is required.");
        }

        if (string.Equals(connectorName, "{{connector_name}}", StringComparison.Ordinal))
        {
            return CreateQueueErrorResponse(
                operationId,
                StatusCodes.Status400BadRequest,
                QueueFailureKind.Validation,
                "connector_name route parameter contains an unresolved variable.");
        }

        if (string.IsNullOrWhiteSpace(messageId))
        {
            return CreateQueueErrorResponse(
                operationId,
                StatusCodes.Status400BadRequest,
                QueueFailureKind.Validation,
                "message_id route parameter is required.");
        }

        if (string.Equals(messageId, "{{message_id}}", StringComparison.Ordinal))
        {
            return CreateQueueErrorResponse(
                operationId,
                StatusCodes.Status400BadRequest,
                QueueFailureKind.Validation,
                "message_id route parameter contains an unresolved variable.");
        }

        var result = await _gestionaQueueService.GetConnectorMessageAsync(
            connectorName,
            messageId,
            accessTokenOverride: null,
            cancellationToken);

        if (!result.Success)
        {
            if (result.FailureKind == QueueFailureKind.NoActiveSubscription)
            {
                return CreateQueueErrorResponse(
                    operationId,
                    StatusCodes.Status200OK,
                    NoActiveSubscriptionCode,
                    "No Active Subscription",
                    result.FailureKind,
                    result.ErrorMessage ?? "No active subscription was found.");
            }

            var statusCode = result.FailureKind switch
            {
                QueueFailureKind.Configuration => StatusCodes.Status500InternalServerError,
                QueueFailureKind.Validation => StatusCodes.Status400BadRequest,
                QueueFailureKind.NotFound => StatusCodes.Status404NotFound,
                _ => result.UpstreamStatusCode ?? StatusCodes.Status502BadGateway
            };

            return CreateQueueErrorResponse(
                operationId,
                statusCode,
                result.FailureKind,
                result.ErrorMessage ?? "Unknown error.");
        }

        return Ok(new GatewayResponse(
            operationId,
            true,
            new QueueConnectorMessageResponse(
                result.Message!.MessageId,
                result.Message.DateSigned)));
    }

    /// <summary>
    /// Sends a response for a queued connector message.
    /// </summary>
    /// <param name="connectorName">The connector name associated with the queued message.</param>
    /// <param name="messageId">The queued message identifier.</param>
    /// <param name="request">The connector response payload.</param>
    /// <param name="operationId">An optional operation identifier echoed back in the response envelope.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A gateway response envelope confirming the sent response, or an error payload.</returns>
    [HttpPost("connectors/{connector_name}/{message_id}")]
    [Consumes("application/json", ConnectorResponseContentType)]
    public async Task<ActionResult<GatewayResponse>> SendConnectorResponse(
        [FromRoute(Name = "connector_name")] string connectorName,
        [FromRoute(Name = "message_id")] string messageId,
        [FromBody] SendQueueConnectorResponseRequest? request,
        [FromQuery(Name = "operationId")] string? operationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "{Method} received queue connector response for connector {ConnectorName}, message {MessageId} with operationId {OperationId}",
            nameof(SendConnectorResponse),
            connectorName,
            messageId,
            operationId);

        if (string.IsNullOrWhiteSpace(connectorName))
        {
            return CreateQueueErrorResponse(
                operationId,
                StatusCodes.Status400BadRequest,
                QueueFailureKind.Validation,
                "connector_name route parameter is required.");
        }

        if (string.Equals(connectorName, "{{connector_name}}", StringComparison.Ordinal))
        {
            return CreateQueueErrorResponse(
                operationId,
                StatusCodes.Status400BadRequest,
                QueueFailureKind.Validation,
                "connector_name route parameter contains an unresolved variable.");
        }

        if (string.IsNullOrWhiteSpace(messageId))
        {
            return CreateQueueErrorResponse(
                operationId,
                StatusCodes.Status400BadRequest,
                QueueFailureKind.Validation,
                "message_id route parameter is required.");
        }

        if (string.Equals(messageId, "{{message_id}}", StringComparison.Ordinal))
        {
            return CreateQueueErrorResponse(
                operationId,
                StatusCodes.Status400BadRequest,
                QueueFailureKind.Validation,
                "message_id route parameter contains an unresolved variable.");
        }

        if (request is null)
        {
            return CreateQueueErrorResponse(
                operationId,
                StatusCodes.Status400BadRequest,
                QueueFailureKind.Validation,
                "Request body is required.");
        }

        var result = await _gestionaQueueService.SendConnectorResponseAsync(
            connectorName,
            messageId,
            new GestionaGateway.Core.Models.QueueConnectorResponseRequest(
                request.ResultSuccess,
                request.Message),
            accessTokenOverride: null,
            cancellationToken);

        if (!result.Success)
        {
            var statusCode = result.FailureKind switch
            {
                QueueFailureKind.Configuration => StatusCodes.Status500InternalServerError,
                QueueFailureKind.Validation => StatusCodes.Status400BadRequest,
                QueueFailureKind.NotFound => StatusCodes.Status404NotFound,
                _ => result.UpstreamStatusCode ?? StatusCodes.Status502BadGateway
            };

            return CreateQueueErrorResponse(
                operationId,
                statusCode,
                result.FailureKind,
                result.ErrorMessage ?? "Unknown error.");
        }

        return Ok(new GatewayResponse(
            operationId,
            true,
            new GestionaGatewayAPI.Models.QueueConnectorResponseResult(
                result.Response!.ConnectorName,
                result.Response.MessageId)));
    }

    private ActionResult<GatewayResponse> CreateQueueErrorResponse(
        string? operationId,
        int statusCode,
        QueueFailureKind failureKind,
        string message)
    {
        return CreateQueueErrorResponse(
            operationId,
            statusCode,
            statusCode,
            ReasonPhrases.GetReasonPhrase(statusCode),
            failureKind,
            message);
    }

    private ActionResult<GatewayResponse> CreateQueueErrorResponse(
        string? operationId,
        int statusCode,
        int errorCode,
        string errorName,
        QueueFailureKind failureKind,
        string message)
    {
        return StatusCode(
            statusCode,
            new GatewayResponse(
                operationId,
                false,
                new QueueError(
                    errorCode,
                    errorName,
                    failureKind.ToString(),
                    message)));
    }
}
