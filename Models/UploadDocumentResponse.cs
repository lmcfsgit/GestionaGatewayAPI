namespace GestionaGatewayAPI.Models;

public sealed record UploadDocumentResponse(
    string ProcessId,
    string FileName,
    string SourcePath,
    int ContentLength);
