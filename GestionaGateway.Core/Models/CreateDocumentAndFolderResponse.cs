using System.Text.Json.Serialization;

namespace GestionaGateway.Core.Models;

/// <summary>
/// Represents the upstream Gestiona response returned after creating a document or folder.
/// </summary>
public sealed record CreateDocumentAndFolderResponse
{
    /// <summary>
    /// Gets the upstream entity identifier when it is included directly in the response body.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// Gets the upstream creation timestamp.
    /// </summary>
    [JsonPropertyName("creation_date")]
    public string CreationDate { get; init; } = string.Empty;

    /// <summary>
    /// Gets the upstream modification timestamp.
    /// </summary>
    [JsonPropertyName("modification_date")]
    public string ModificationDate { get; init; } = string.Empty;

    /// <summary>
    /// Gets the upstream links associated with the created entity.
    /// </summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<CreateDocumentAndFolderLink>? Links { get; init; }

    /// <summary>
    /// Resolves the created entity identifier from <see cref="Id"/> or from the last segment of the self link.
    /// </summary>
    /// <returns>The resolved identifier, or an empty string when no identifier can be resolved.</returns>
    public string GetResolvedId()
    {
        if (!string.IsNullOrWhiteSpace(Id))
        {
            return Id;
        }

        var selfHref = Links?
            .FirstOrDefault(link => string.Equals(link.Rel, "self", StringComparison.Ordinal))?
            .Href;

        if (string.IsNullOrWhiteSpace(selfHref))
        {
            return string.Empty;
        }

        return Uri.TryCreate(selfHref, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri.Segments.LastOrDefault()?.Trim('/') ?? string.Empty
            : selfHref.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
    }
}

/// <summary>
/// Represents a link returned by Gestiona for a created document or folder.
/// </summary>
public sealed record CreateDocumentAndFolderLink
{
    /// <summary>
    /// Gets the link relation name.
    /// </summary>
    [JsonPropertyName("rel")]
    public string? Rel { get; init; }

    /// <summary>
    /// Gets the link target URL.
    /// </summary>
    [JsonPropertyName("href")]
    public string? Href { get; init; }
}
