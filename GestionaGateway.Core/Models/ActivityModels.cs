using System.Text.Json.Serialization;

namespace GestionaGateway.Core.Models;

public sealed record Activity(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name);

public sealed record ActivitiesResponse(
    [property: JsonPropertyName("content")] IReadOnlyList<Activity>? Content);

public sealed record Procedure(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name);

public sealed record ExternalProcedure(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("title")] string? Title);

public sealed record ExternalProceduresResponse(
    [property: JsonPropertyName("content")] IReadOnlyList<ExternalProcedure>? Content);

public sealed record GetActivitiesResult(
    bool Success,
    GetActivitiesFailureKind FailureKind,
    string? ErrorMessage,
    IReadOnlyList<Activity>? Activities,
    int? UpstreamStatusCode);

public sealed record GetProceduresResult(
    bool Success,
    GetActivitiesFailureKind FailureKind,
    string? ErrorMessage,
    IReadOnlyList<Procedure>? Procedures,
    int? UpstreamStatusCode);

public enum GetActivitiesFailureKind
{
    None,
    Configuration,
    Upstream
}
