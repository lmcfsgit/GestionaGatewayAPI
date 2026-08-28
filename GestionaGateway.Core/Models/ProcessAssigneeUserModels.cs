using System.Text.Json.Serialization;

namespace GestionaGateway.Core.Models;

public sealed record GetProcessAssigneeUserRequest(
    [property: JsonPropertyName("username")] string? Username);

public sealed record ProcessAssigneeUser(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("name")] string? Name);

public sealed record ProcessAssigneeUsersResponse(
    [property: JsonPropertyName("content")] IReadOnlyList<ProcessAssigneeUser>? Content);

public sealed record GetProcessAssigneeUserResult(
    bool Success,
    GetProcessFailureKind FailureKind,
    string? ErrorMessage,
    ProcessAssigneeUser? User,
    int? UpstreamStatusCode);

public sealed record ProcessAssigneeGroup(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name);

public sealed record ProcessAssigneeGroupsResponse(
    [property: JsonPropertyName("content")] IReadOnlyList<ProcessAssigneeGroup>? Content);

public sealed record GetProcessAssigneeGroupsResult(
    bool Success,
    GetProcessFailureKind FailureKind,
    string? ErrorMessage,
    IReadOnlyList<ProcessAssigneeGroup>? Groups,
    int? UpstreamStatusCode);
