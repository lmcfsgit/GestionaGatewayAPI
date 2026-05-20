namespace GestionaGateway.Core.Models;

public sealed record GestionaApiCallResult(
    int StatusCode,
    bool Success);

public sealed record GestionaApiCallResult<T>(
    int StatusCode,
    bool Success,
    T? Value);
