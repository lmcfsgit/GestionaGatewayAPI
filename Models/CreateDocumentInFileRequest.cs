namespace GestionaGatewayAPI.Models;

public sealed record CreateDocumentInFileRequest(
    string Name,
    string Type,
    string MetadataLanguage,
    string Trashed,
    string Version,
    string ContentHref);
