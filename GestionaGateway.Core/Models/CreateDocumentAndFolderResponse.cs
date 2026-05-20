using System.Text.Json.Serialization;

namespace GestionaGateway.Core.Models;

public sealed record CreateDocumentAndFolderResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("creation_date")]
    public string CreationDate { get; init; } = string.Empty;

    [JsonPropertyName("modification_date")]
    public string ModificationDate { get; init; } = string.Empty;

    [JsonPropertyName("links")]
    public IReadOnlyList<CreateDocumentAndFolderLink>? Links { get; init; }

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

public sealed record CreateDocumentAndFolderLink
{
    [JsonPropertyName("rel")]
    public string? Rel { get; init; }

    [JsonPropertyName("href")]
    public string? Href { get; init; }
}
