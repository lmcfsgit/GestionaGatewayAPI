using System.Text.Json.Serialization;

namespace GestionaGateway.Core.Models;

public sealed record RelatedProcessesRequest(
    [property: JsonPropertyName("id1")] string? Id1,
    [property: JsonPropertyName("id2")] string? Id2);

public sealed record RelatedProcessesResult(
    bool Success,
    GetProcessFailureKind FailureKind,
    string? ErrorMessage,
    RelatedProcessesRequest? RelatedProcesses,
    int? UpstreamStatusCode);

public sealed record DeleteRelatedProcessResult(
    bool Success,
    GetProcessFailureKind FailureKind,
    string? ErrorMessage,
    int? UpstreamStatusCode);

public sealed record GetRelatedProcessesResult(
    bool Success,
    GetProcessFailureKind FailureKind,
    string? ErrorMessage,
    IReadOnlyList<RelatedProcessItem>? RelatedProcesses,
    int? UpstreamStatusCode);

public sealed record RelatedProcessItem(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("processNumber")] string? ProcessNumber);

public sealed record RelatedFilesRequest(
    [property: JsonPropertyName("links")] IReadOnlyList<GestionaLink> Links);

public sealed record RelatedFilesResponse(
    [property: JsonPropertyName("content")] IReadOnlyList<RelatedFile>? Content);

public sealed record RelatedFile(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("code")] string? Code);
