using GestionaGateway.Core.Models;
using GestionaGateway.Core.Services;
using GestionaGatewayAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace GestionaGatewayAPI.Controllers;

/// <summary>
/// Provides operations for retrieving Gestiona activities.
/// </summary>
[ApiController]
[Route("activities")]
public sealed class ActivitiesController : ControllerBase
{
    private readonly IGestionaActivityService _gestionaActivityService;
    private readonly ILogger<ActivitiesController> _logger;

    public ActivitiesController(
        IGestionaActivityService gestionaActivityService,
        ILogger<ActivitiesController> logger)
    {
        _gestionaActivityService = gestionaActivityService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the activities available in the Gestiona catalog.
    /// </summary>
    /// <param name="operationId">An optional operation identifier echoed back in the response envelope.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A response envelope containing the activity list on success, or an error payload when the lookup fails.</returns>
    [HttpGet]
    public async Task<ActionResult<GatewayResponse>> GetActivities(
        [FromQuery(Name = "operationId")] string? operationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "{Method} received activities request with operationId {OperationId}",
            nameof(GetActivities),
            operationId);

        var result = await _gestionaActivityService.GetActivitiesAsync(
            GestionaRequestHeaders.GetAccessToken(Request),
            cancellationToken);

        if (!result.Success)
        {
            var statusCode = result.FailureKind switch
            {
                GetActivitiesFailureKind.Configuration => StatusCodes.Status500InternalServerError,
                _ => result.UpstreamStatusCode ?? StatusCodes.Status502BadGateway
            };

            return CreateActivitiesErrorResponse(
                operationId,
                statusCode,
                result.FailureKind,
                result.ErrorMessage ?? "Unknown error.");
        }

        return Ok(new GatewayResponse(
            operationId,
            true,
            result.Activities ?? []));
    }

    /// <summary>
    /// Gets the external procedures available for a Gestiona activity.
    /// </summary>
    /// <param name="activityId">The Gestiona catalog activity identifier.</param>
    /// <param name="operationId">An optional operation identifier echoed back in the response envelope.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A response envelope containing the procedure list on success, or an error payload when the lookup fails.</returns>
    [HttpGet("{activity_id}/procedures")]
    public async Task<ActionResult<GatewayResponse>> GetProcedures(
        [FromRoute(Name = "activity_id")] string activityId,
        [FromQuery(Name = "operationId")] string? operationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "{Method} received procedures request for activity {ActivityId} with operationId {OperationId}",
            nameof(GetProcedures),
            activityId,
            operationId);

        var result = await _gestionaActivityService.GetProceduresAsync(
            activityId,
            GestionaRequestHeaders.GetAccessToken(Request),
            cancellationToken);

        if (!result.Success)
        {
            var statusCode = result.FailureKind switch
            {
                GetActivitiesFailureKind.Configuration => StatusCodes.Status500InternalServerError,
                _ => result.UpstreamStatusCode ?? StatusCodes.Status502BadGateway
            };

            return CreateActivitiesErrorResponse(
                operationId,
                statusCode,
                result.FailureKind,
                result.ErrorMessage ?? "Unknown error.");
        }

        return Ok(new GatewayResponse(
            operationId,
            true,
            result.Procedures ?? []));
    }

    private ActionResult<GatewayResponse> CreateActivitiesErrorResponse(
        string? operationId,
        int statusCode,
        GetActivitiesFailureKind failureKind,
        string message)
    {
        return StatusCode(
            statusCode,
            new GatewayResponse(
                operationId,
                false,
                new ActivityError(
                    statusCode,
                    ReasonPhrases.GetReasonPhrase(statusCode),
                    failureKind.ToString(),
                    message)));
    }
}
