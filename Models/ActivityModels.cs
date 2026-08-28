namespace GestionaGatewayAPI.Models;

public sealed record ActivityError(
    int Code,
    string Name,
    string Kind,
    string Message);
