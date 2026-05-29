namespace GestionaGateway.Core.Models;

/// <summary>
/// Represents downloaded document content and metadata returned by Gestiona.
/// </summary>
/// <param name="DocumentId">The Gestiona document identifier.</param>
/// <param name="FileName">The file name resolved from the upstream response.</param>
/// <param name="ContentType">The response content type.</param>
/// <param name="StorageSize">The upstream storage size metadata.</param>
/// <param name="StorageExtension">The upstream storage extension metadata.</param>
/// <param name="StorageMimeType">The upstream storage MIME type metadata.</param>
/// <param name="StorageMd5">The upstream MD5 metadata.</param>
/// <param name="StorageSha1">The upstream SHA-1 metadata.</param>
/// <param name="StorageSha512">The upstream SHA-512 metadata.</param>
/// <param name="Content">The downloaded document bytes.</param>
public sealed record DownloadedDocument(
    string DocumentId,
    string? FileName,
    string? ContentType,
    long? StorageSize,
    string? StorageExtension,
    string? StorageMimeType,
    string? StorageMd5,
    string? StorageSha1,
    string? StorageSha512,
    byte[] Content);
