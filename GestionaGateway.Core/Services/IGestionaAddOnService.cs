using GestionaGateway.Core.Models;

namespace GestionaGateway.Core.Services;

public interface IGestionaAddOnService
{
    Task<CreateAddOnAuthorizationResult> CreateAuthorizationAsync(
        CancellationToken cancellationToken);

    Task<GetAddOnAuthorizationResult> GetAuthorizationAsync(
        string authId,
        CancellationToken cancellationToken);
}
