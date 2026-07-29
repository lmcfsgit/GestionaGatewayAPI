namespace GestionaGatewayAPI.Models;

/// <summary>
/// Represents an error payload returned by the process documents endpoint.
/// </summary>
public sealed record ProcessDocumentsError(
    int Code,
    string Name,
    string Kind,
    string Message);
