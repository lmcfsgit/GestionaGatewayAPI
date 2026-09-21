using System.Text.Json.Serialization;

namespace GestionaGateway.Core.Models;

public sealed record AddOnAuthorization(
    [property: JsonPropertyName("authId")] string AuthId);

public sealed record AddOnAuthorizationStatus(
    [property: JsonPropertyName("authorized")] bool Authorized,
    [property: JsonPropertyName("authorizeUrl"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? AuthorizeUrl,
    [property: JsonPropertyName("authorizedInfo"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AddOnAuthorizedInfo? AuthorizedInfo);

public sealed record AddOnAuthorizedInfo(
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("accessToken")] string AccessToken);

public sealed record GestionaAddOnAuthorization(
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("user_id")] string? UserId);

public sealed record GestionaAddOnAuthorizationStatus(
    string? AuthorizeUrl,
    GestionaAddOnAuthorization? Authorization);

public sealed record CreateAddOnAuthorizationResult(
    bool Success,
    CreateAddOnAuthorizationFailureKind FailureKind,
    string? ErrorMessage,
    AddOnAuthorization? Authorization,
    int? UpstreamStatusCode);

public sealed record GetAddOnAuthorizationResult(
    bool Success,
    CreateAddOnAuthorizationFailureKind FailureKind,
    string? ErrorMessage,
    AddOnAuthorizationStatus? AuthorizationStatus,
    int? UpstreamStatusCode);

public enum CreateAddOnAuthorizationFailureKind
{
    None,
    Configuration,
    Upstream
}
