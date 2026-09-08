using System.Text.Json.Serialization;

namespace GestionaGatewayAPI.Models;

public sealed record SendQueueConnectorResponseRequest(
    [property: JsonPropertyName("result_success")] string? ResultSuccess,
    [property: JsonPropertyName("message")] string? Message);

public sealed record QueueConnectorMessageResponse(
    [property: JsonPropertyName("message_id")] string? MessageId,
    [property: JsonPropertyName("date_signed")] string? DateSigned);

public sealed record QueueConnectorResponseResult(
    [property: JsonPropertyName("connector_name")] string ConnectorName,
    [property: JsonPropertyName("message_id")] string MessageId);

/// <summary>
/// Represents an error payload returned by queue endpoints.
/// </summary>
/// <param name="Code">The HTTP status code returned by the gateway.</param>
/// <param name="Name">The HTTP reason phrase.</param>
/// <param name="Kind">The gateway failure classification.</param>
/// <param name="Message">The human-readable error message.</param>
public sealed record QueueError(
    int Code,
    string Name,
    string Kind,
    string Message);
