namespace GestionaGatewayAPI.Models;

/// <summary>
/// Represents the standard gateway response envelope.
/// </summary>
/// <param name="OperationId">The optional operation identifier echoed from the request.</param>
/// <param name="Success">Indicates whether the request completed successfully.</param>
/// <param name="Result">The endpoint-specific success or error payload.</param>
public sealed record GatewayResponse(
    string? OperationId,
    bool Success,
    object Result);
