namespace GestionaGateway.Core.Models;

/// <summary>
/// Represents the response data for a temporary Gestiona upload space.
/// </summary>
/// <param name="UploadUrl">The URL where document content should be uploaded.</param>
public sealed record CreateUploadSpaceResponse(string UploadUrl);
