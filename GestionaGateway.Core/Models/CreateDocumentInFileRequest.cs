namespace GestionaGateway.Core.Models;

public sealed record CreateDocumentInFileRequest
{
    public string Name { get; init; } = null!;

    public string Type { get; init; } = null!;

    public string MetadataLanguage { get; init; } = null!;

    public string Trashed { get; init; } = null!;

    public string Version { get; init; } = null!;

    public string? ExternalUrl { get; init; }

    public string? ContentHref { get; init; }
}
