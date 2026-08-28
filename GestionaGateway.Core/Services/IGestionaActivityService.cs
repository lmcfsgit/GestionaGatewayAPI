using GestionaGateway.Core.Models;

namespace GestionaGateway.Core.Services;

public interface IGestionaActivityService
{
    Task<GetActivitiesResult> GetActivitiesAsync(
        string? accessTokenOverride,
        CancellationToken cancellationToken);

    Task<GetProceduresResult> GetProceduresAsync(
        string activityId,
        string? accessTokenOverride,
        CancellationToken cancellationToken);
}
