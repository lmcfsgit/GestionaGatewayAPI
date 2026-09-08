using GestionaGateway.Core.Models;

namespace GestionaGateway.Core.Services;

public interface IGestionaQueueService
{
    Task<GetQueueConnectorMessagesResult> GetConnectorMessagesAsync(
        string connectorName,
        string? accessTokenOverride,
        CancellationToken cancellationToken);

    Task<GetQueueConnectorMessageResult> GetConnectorMessageAsync(
        string connectorName,
        string messageId,
        string? accessTokenOverride,
        CancellationToken cancellationToken);

    Task<SendQueueConnectorResponseResult> SendConnectorResponseAsync(
        string connectorName,
        string messageId,
        QueueConnectorResponseRequest request,
        string? accessTokenOverride,
        CancellationToken cancellationToken);
}
