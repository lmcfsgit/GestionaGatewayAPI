using GestionaGateway.Core.Models;
using GestionaGateway.Core.Services;
using GestionaGatewayAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace GestionaGatewayAPI.Controllers;

[ApiController]
[Route("thirds")]
public sealed class ThirdsController : ControllerBase
{
    private readonly IGestionaThirdService _gestionaThirdService;
    private readonly ILogger<ThirdsController> _logger;

    public ThirdsController(
        IGestionaThirdService gestionaThirdService,
        ILogger<ThirdsController> logger)
    {
        _gestionaThirdService = gestionaThirdService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a third from Gestiona by NIF.
    /// </summary>
    /// <param name="nif">The NIF used to resolve the Gestiona third identifier.</param>
    /// <param name="operationId">An optional operation identifier echoed back in the response envelope.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A response envelope containing the third data on success, or an error payload when the lookup fails.</returns>
    [HttpGet]
    public async Task<ActionResult<GatewayResponse>> GetThirdByNif(
        [FromQuery(Name = "nif")] string nif,
        [FromQuery(Name = "operationId")] string? operationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "{Method} received third request for nif {Nif} with operationId {OperationId}",
            nameof(GetThirdByNif),
            nif,
            operationId);

        if (string.IsNullOrWhiteSpace(nif))
        {
            return CreateThirdErrorResponse(
                operationId,
                StatusCodes.Status400BadRequest,
                GetThirdFailureKind.Validation,
                "nif query parameter is required.");
        }

        if (string.Equals(nif, "{{nif}}", StringComparison.Ordinal))
        {
            return CreateThirdErrorResponse(
                operationId,
                StatusCodes.Status400BadRequest,
                GetThirdFailureKind.Validation,
                "nif query parameter contains an unresolved variable.");
        }

        var result = await _gestionaThirdService.GetThirdByNifAsync(
            nif,
            GestionaRequestHeaders.GetAccessToken(Request),
            cancellationToken);

        return CreateThirdResponse(
            operationId,
            result);
    }

    /// <summary>
    /// Gets a third from Gestiona.
    /// </summary>
    /// <param name="thirdId">The Gestiona third identifier.</param>
    /// <param name="operationId">An optional operation identifier echoed back in the response envelope.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A response envelope containing the third data on success, or an error payload when the lookup fails.</returns>
    [HttpGet("{third_id}")]
    public async Task<ActionResult<GatewayResponse>> GetThird(
        [FromRoute(Name = "third_id")] string thirdId,
        [FromQuery(Name = "operationId")] string? operationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "{Method} received third request for {ThirdId} with operationId {OperationId}",
            nameof(GetThird),
            thirdId,
            operationId);

        if (string.IsNullOrWhiteSpace(thirdId))
        {
            return CreateThirdErrorResponse(
                operationId,
                StatusCodes.Status400BadRequest,
                GetThirdFailureKind.Validation,
                "third_id route parameter is required.");
        }

        if (string.Equals(thirdId, "{{third_id}}", StringComparison.Ordinal))
        {
            return CreateThirdErrorResponse(
                operationId,
                StatusCodes.Status400BadRequest,
                GetThirdFailureKind.Validation,
                "third_id route parameter contains an unresolved variable.");
        }

        var result = await _gestionaThirdService.GetThirdAsync(
            thirdId,
            GestionaRequestHeaders.GetAccessToken(Request),
            cancellationToken);

        return CreateThirdResponse(
            operationId,
            result);
    }

    private ActionResult<GatewayResponse> CreateThirdResponse(
        string? operationId,
        GetThirdResult result)
    {
        if (!result.Success)
        {
            var statusCode = result.FailureKind switch
            {
                GetThirdFailureKind.Configuration => StatusCodes.Status500InternalServerError,
                GetThirdFailureKind.Validation => StatusCodes.Status400BadRequest,
                GetThirdFailureKind.NotFound => StatusCodes.Status404NotFound,
                _ => result.UpstreamStatusCode ?? StatusCodes.Status502BadGateway
            };

            return CreateThirdErrorResponse(
                operationId,
                statusCode,
                result.FailureKind,
                result.ErrorMessage ?? "Unknown error.");
        }

        return Ok(new GatewayResponse(
            operationId,
            true,
            result.Third!));
    }

    private ActionResult<GatewayResponse> CreateThirdErrorResponse(
        string? operationId,
        int statusCode,
        GetThirdFailureKind failureKind,
        string message)
    {
        return StatusCode(
            statusCode,
            new GatewayResponse(
                operationId,
                false,
                new ThirdError(
                    statusCode,
                    ReasonPhrases.GetReasonPhrase(statusCode),
                    failureKind.ToString(),
                    message)));
    }
}
