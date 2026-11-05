using System.Text.Json;
using System.Text.Json.Serialization;

namespace GestionaGateway.Core.Models;

public sealed record CircuitTemplateFilter(
    [property: JsonPropertyName("name")] string? Name);

public sealed record CircuitTemplate(
    [property: JsonPropertyName("name")] string? Name)
{
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? Fields { get; init; }
}

public sealed record CreateCircuitResult(
    bool Success,
    GetProcessDocumentsFailureKind FailureKind,
    string? ErrorMessage,
    int? UpstreamStatusCode);

public sealed record CircuitTemplatesResponse(
    [property: JsonPropertyName("content")] IReadOnlyList<CircuitTemplate>? Content,
    [property: JsonPropertyName("links")] IReadOnlyList<GestionaLink>? Links);

public sealed record GetCircuitTemplatesResult(
    bool Success,
    GetProcessDocumentsFailureKind FailureKind,
    string? ErrorMessage,
    IReadOnlyList<CircuitTemplate>? Templates,
    int? UpstreamStatusCode);
