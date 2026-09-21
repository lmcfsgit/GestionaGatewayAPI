using GestionaGateway.Core.Models;
using GestionaGateway.Core.Services;
using GestionaGatewayAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace GestionaGatewayAPI.Controllers;

/// <summary>
/// Provides operations for Gestiona add-ons.
/// </summary>
[ApiController]
[Route("addon")]
public sealed class AddOnController : ControllerBase
{
    private readonly IGestionaAddOnService _gestionaAddOnService;
    private readonly ILogger<AddOnController> _logger;

    public AddOnController(
        IGestionaAddOnService gestionaAddOnService,
        ILogger<AddOnController> logger)
    {
        _gestionaAddOnService = gestionaAddOnService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a Gestiona add-on authorization.
    /// </summary>
    /// <param name="operationId">An optional operation identifier echoed back in the response envelope.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A response envelope containing the add-on authorization identifier.</returns>
    [HttpPost("authorizations")]
    public async Task<ActionResult<GatewayResponse>> CreateAuthorization(
        [FromQuery(Name = "operationId")] string? operationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "{Method} received add-on authorization request with operationId {OperationId}",
            nameof(CreateAuthorization),
            operationId);

        var result = await _gestionaAddOnService.CreateAuthorizationAsync(cancellationToken);

        if (!result.Success)
        {
            var statusCode = result.FailureKind switch
            {
                CreateAddOnAuthorizationFailureKind.Configuration => StatusCodes.Status500InternalServerError,
                _ => result.UpstreamStatusCode ?? StatusCodes.Status502BadGateway
            };

            return CreateAddOnAuthorizationErrorResponse(
                operationId,
                statusCode,
                result.FailureKind,
                result.ErrorMessage ?? "Unknown error.");
        }

        return Ok(new GatewayResponse(
            operationId,
            true,
            result.Authorization!));
    }

    /// <summary>
    /// Gets the current state of a Gestiona add-on authorization.
    /// </summary>
    [HttpGet("authorizations/{authId}")]
    public async Task<ActionResult<GatewayResponse>> GetAuthorization(
        [FromRoute] string authId,
        [FromQuery(Name = "operationId")] string? operationId,
        CancellationToken cancellationToken)
    {
        var result = await _gestionaAddOnService.GetAuthorizationAsync(authId, cancellationToken);

        if (!result.Success)
        {
            var statusCode = result.FailureKind == CreateAddOnAuthorizationFailureKind.Configuration
                ? StatusCodes.Status500InternalServerError
                : result.UpstreamStatusCode ?? StatusCodes.Status502BadGateway;
            return CreateAddOnAuthorizationErrorResponse(
                operationId,
                statusCode,
                result.FailureKind,
                result.ErrorMessage ?? "Unknown error.");
        }

        return Ok(new GatewayResponse(operationId, true, result.AuthorizationStatus!));
    }

    private ActionResult<GatewayResponse> CreateAddOnAuthorizationErrorResponse(
        string? operationId,
        int statusCode,
        CreateAddOnAuthorizationFailureKind failureKind,
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
