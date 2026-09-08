using System.Text.Json.Serialization;

namespace GestionaGateway.Core.Models;

public sealed record QueueSubscription(
    [property: JsonPropertyName("name")] string? Name);

public sealed record QueueSubscriptionsResponse(
    [property: JsonPropertyName("content")] IReadOnlyList<QueueSubscription>? Content);

public sealed record QueueConnector(
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("name")] string? Name);

public sealed record QueueConnectorsResponse(
    [property: JsonPropertyName("content")] IReadOnlyList<QueueConnector>? Content);

public sealed record QueueConnectorMessage(
    [property: JsonPropertyName("payload")] QueueConnectorMessagePayload? Payload,
    [property: JsonPropertyName("entry")] string? Entry,
    [property: JsonPropertyName("next_delivery")] string? NextDelivery);

public sealed record QueueConnectorMessagesResponse(
    [property: JsonPropertyName("content")] IReadOnlyList<QueueConnectorMessage>? Content);

public sealed record QueueConnectorMessagePayload(
    [property: JsonPropertyName("target")] string? Target);

public sealed record QueueConnectorMessageResult(
    string? MessageId,
    string? DateSigned);

public sealed record QueueConnectorResponseRequest(
    [property: JsonPropertyName("result_success")] string? ResultSuccess,
    [property: JsonPropertyName("message")] string? Message);

public sealed record QueueConnectorResponseResult(
    string ConnectorName,
    string MessageId);

public sealed record GetQueueConnectorMessageResult(
    bool Success,
    QueueFailureKind FailureKind,
    string? ErrorMessage,
    QueueConnectorMessageResult? Message,
    int? UpstreamStatusCode);

public sealed record GetQueueConnectorMessagesResult(
    bool Success,
    QueueFailureKind FailureKind,
    string? ErrorMessage,
    IReadOnlyList<QueueConnectorMessageResult> Messages,
    int? UpstreamStatusCode);

public sealed record SendQueueConnectorResponseResult(
    bool Success,
    QueueFailureKind FailureKind,
    string? ErrorMessage,
    QueueConnectorResponseResult? Response,
    int? UpstreamStatusCode);

public enum QueueFailureKind
{
    None,
    Configuration,
    Validation,
    NotFound,
    NoActiveSubscription,
    Upstream
}
