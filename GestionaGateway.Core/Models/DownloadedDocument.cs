namespace GestionaGateway.Core.Models;

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
