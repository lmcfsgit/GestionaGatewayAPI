using System.Text.Json.Serialization;

namespace GestionaGateway.Core.Models;

/// <summary>
/// Represents the subset of the Gestiona file third-party response needed to extract third identifiers.
/// </summary>
/// <param name="Content">The upstream third-party items returned for the file.</param>
public sealed record ProcessThirdPartyLinksResponse(
    [property: JsonPropertyName("content")] IReadOnlyList<ProcessThirdPartyLinkItem>? Content);

/// <summary>
/// Represents a third-party item returned by Gestiona.
/// </summary>
/// <param name="Links">The links associated with the third-party item.</param>
public sealed record ProcessThirdPartyLinkItem(
    [property: JsonPropertyName("links")] IReadOnlyList<GestionaLink>? Links);

/// <summary>
/// Represents a generic link returned by Gestiona.
/// </summary>
/// <param name="Rel">The link relation name.</param>
/// <param name="Href">The link target URL.</param>
/// <param name="Title">The optional link title.</param>
public sealed record GestionaLink(
    [property: JsonPropertyName("rel")] string? Rel,
    [property: JsonPropertyName("href")] string? Href,
    [property: JsonPropertyName("title")] string? Title);
