namespace GestionaGateway.Core.Models;

/// <summary>
/// Represents the payload used to create a document or folder in a Gestiona file.
/// </summary>
public sealed record CreateDocumentInFileRequest
{
    /// <summary>
    /// Gets the document or folder name.
    /// </summary>
    public string Name { get; init; } = null!;

    /// <summary>
    /// Gets the document source type sent to Gestiona.
    /// </summary>
    public string Type { get; init; } = null!;

    /// <summary>
    /// Gets the metadata language code sent to Gestiona.
    /// </summary>
    public string MetadataLanguage { get; init; } = null!;

    /// <summary>
    /// Gets the upstream trashed flag value.
    /// </summary>
    // public string Trashed { get; init; } = null!;

    /// <summary>
    /// Gets the upstream version value.
    /// </summary>
    // public string Version { get; init; } = null!;

    /// <summary>
    /// Gets the external URL used when creating an external URL document.
    /// </summary>
    public string? ExternalUrl { get; init; }

    /// <summary>
    /// Gets the uploaded content link used when creating a digital document.
    /// </summary>
    public string? ContentHref { get; init; }

    /// <summary>
    /// Gets the optional line value used when creating a folder.
    /// </summary>
    public string? Line { get; init; }
}
