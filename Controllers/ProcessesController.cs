using GestionaGatewayAPI.Models;
using GestionaGateway.Core.Models;
using GestionaGateway.Core.Services;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace GestionaGatewayAPI.Controllers;

[ApiController]
[Route("processes")]
public sealed class ProcessesController : ControllerBase
{
    private static readonly JsonSerializerOptions LogJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IConfiguration _configuration;
    private readonly IGestionaDocumentService _gestionaDocumentService;
    private readonly ILogger<ProcessesController> _logger;

    public ProcessesController(
        IConfiguration configuration,
        IGestionaDocumentService gestionaDocumentService,
        ILogger<ProcessesController> logger)
    {
        _configuration = configuration;
        _gestionaDocumentService = gestionaDocumentService;
        _logger = logger;
    }

    [HttpPost("documents")]
    public async Task<ActionResult<UploadDocumentResponse>> Upload(
        [FromQuery(Name = "process_number")] string processNumber,
        [FromBody] UploadDocumentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "{Method} received upload request body for process lookup route {ProcessNumber}:{NewLine}{Request}",
            nameof(Upload),
            processNumber,
            Environment.NewLine,
            JsonSerializer.Serialize(request, LogJsonOptions));

        if (string.IsNullOrWhiteSpace(processNumber))
        {
            return BadRequest(new { error = "process_number query parameter is required." });
        }

        return await UploadDocumentCore(
            request,
            processNumber,
            resolveFileIdFromProcessCode: true,
            cancellationToken);
    }

    [HttpPost("{process_id}/documents")]
    public async Task<ActionResult<UploadDocumentResponse>> UploadToFile(
        [FromRoute(Name = "process_id")] string processId,
        [FromBody] UploadDocumentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "{Method} received upload request body for direct file route {ProcessId}:{NewLine}{Request}",
            nameof(UploadToFile),
            processId,
            Environment.NewLine,
            JsonSerializer.Serialize(request, LogJsonOptions));

        if (string.IsNullOrWhiteSpace(processId))
        {
            return BadRequest(new { error = "process_id route parameter is required." });
        }

        if (string.Equals(processId, "{{process_id}}", StringComparison.Ordinal))
        {
            return BadRequest(new { error = "process_id route parameter contains an unresolved Postman variable." });
        }

        return await UploadDocumentCore(
            request,
            processId,
            resolveFileIdFromProcessCode: false,
            cancellationToken);
    }

    private async Task<ActionResult<UploadDocumentResponse>> UploadDocumentCore(
        UploadDocumentRequest request,
        string processId,
        bool resolveFileIdFromProcessCode,
        CancellationToken cancellationToken)
    {
        var documentsFolder = _configuration["DocumentStorage:BasePath"];
        var result = await _gestionaDocumentService.CreateDocumentInProcessAsync(
            request,
            processId,
            resolveFileIdFromProcessCode,
            documentsFolder ?? string.Empty,
            cancellationToken);

        if (!result.Success)
        {
            return result.FailureKind switch
            {
                CreateDocumentInProcessFailureKind.Configuration => Problem(
                    detail: result.ErrorMessage,
                    statusCode: StatusCodes.Status500InternalServerError),
                CreateDocumentInProcessFailureKind.Validation => BadRequest(new { error = result.ErrorMessage }),
                CreateDocumentInProcessFailureKind.NotFound => NotFound(new { error = result.ErrorMessage, process_id = processId }),
                _ => Problem(
                    detail: result.ErrorMessage,
                    statusCode: StatusCodes.Status502BadGateway)
            };
        }

        return Ok(new UploadDocumentResponse(
            request.OperationId,
            true,
            new UploadDocumentResult(
                result.Document!.Id,
                result.Document.ProcessId,
                result.Document.CreationDate,
                result.Document.ModificationDate)));
    }
}
