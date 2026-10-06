using System.Text.Json;
using System.Text.Json.Serialization;

namespace GestionaGateway.Core.Models;

public sealed record ProcessProcedureDataResponse(
    [property: JsonPropertyName("content")]
    IReadOnlyList<ProcessProcedureDataDefinition>? Content,
    [property: JsonPropertyName("links")]
    IReadOnlyList<GestionaLink>? Links);

public sealed record ProcessProcedureDataDefinition(
    [property: JsonPropertyName("reference")]
    string? Reference,
    [property: JsonPropertyName("i18n_name")]
    string? I18nName,
    [property: JsonPropertyName("type")]
    string? Type,
    [property: JsonPropertyName("value")]
    string? Value = null);

public sealed record ProcessProcedureData(
    [property: JsonPropertyName("reference")]
    string? Reference,
    [property: JsonPropertyName("caption")]
    string? Caption,
    [property: JsonPropertyName("type")]
    string? Type);

public sealed record GetProcessProcedureDataResult(
    bool Success,
    GetProcessDocumentsFailureKind FailureKind,
    string? ErrorMessage,
    IReadOnlyList<ProcessProcedureData>? ProcedureData,
    int? UpstreamStatusCode);

public sealed record ProcessProcedureDataValue
{
    [JsonPropertyName("reference")]
    public string? Reference { get; init; }

    [JsonPropertyName("caption")]
    public string? Caption { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("value")]
    public JsonElement Value { get; init; }
}

public sealed record GetProcessProcedureDataValueResult(
    bool Success,
    GetProcessDocumentsFailureKind FailureKind,
    string? ErrorMessage,
    ProcessProcedureDataValue? ProcedureData,
    int? UpstreamStatusCode);
