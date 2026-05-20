using GestionaGatewayAPI.Models;
using GestionaGateway.Core.Models;
using GestionaGateway.Core.Services;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

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
    private readonly IGestionaProcessService _gestionaProcessService;
    private readonly ILogger<ProcessesController> _logger;

    public ProcessesController(
        IConfiguration configuration,
        IGestionaProcessService gestionaProcessService,
        ILogger<ProcessesController> logger)
    {
        _configuration = configuration;
        _gestionaProcessService = gestionaProcessService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a document by resolving the target Gestiona file from the provided process number.
    /// </summary>
    /// <param name="processNumber">The external process number used to resolve the Gestiona file identifier.</param>
    /// <param name="request">The upload request containing the document metadata and source information.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// An <see cref="UploadDocumentResponse"/> containing the created document information on success,
    /// or an error payload when the request cannot be processed.
    /// </returns>
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
            return CreateErrorResponse(
                request.OperationId,
                StatusCodes.Status400BadRequest,
                CreateDocumentInProcessFailureKind.Validation,
                "process_number query parameter is required.");
        }

        return await UploadDocumentCore(
            request,
            processNumber,
            folderId: null,
            resolveFileIdFromProcessCode: true,
            cancellationToken);
    }

    /// <summary>
    /// Creates a document inside the specified Gestiona folder by resolving the target file from the provided process number.
    /// </summary>
    /// <param name="folderId">The Gestiona folder identifier that will receive the new document.</param>
    /// <param name="processNumber">The external process number used to resolve the Gestiona file identifier.</param>
    /// <param name="request">The upload request containing the document metadata and source information.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// An <see cref="UploadDocumentResponse"/> containing the created document information on success,
    /// or an error payload when the request cannot be processed.
    /// </returns>
    [HttpPost("documents/{folder_id}")]
    public async Task<ActionResult<UploadDocumentResponse>> UploadToResolvedFolder(
        [FromRoute(Name = "folder_id")] string folderId,
        [FromQuery(Name = "process_number")] string processNumber,
        [FromBody] UploadDocumentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "{Method} received upload request body for process lookup folder route {ProcessNumber}/{FolderId}:{NewLine}{Request}",
            nameof(UploadToResolvedFolder),
            processNumber,
            folderId,
            Environment.NewLine,
            JsonSerializer.Serialize(request, LogJsonOptions));

        if (string.IsNullOrWhiteSpace(processNumber))
        {
            return CreateErrorResponse(
                request.OperationId,
                StatusCodes.Status400BadRequest,
                CreateDocumentInProcessFailureKind.Validation,
                "process_number query parameter is required.");
        }

        if (string.IsNullOrWhiteSpace(folderId))
        {
            return CreateErrorResponse(
                request.OperationId,
                StatusCodes.Status400BadRequest,
                CreateDocumentInProcessFailureKind.Validation,
                "folder_id route parameter is required.");
        }

        if (string.Equals(folderId, "{{folder_id}}", StringComparison.Ordinal))
        {
            return CreateErrorResponse(
                request.OperationId,
                StatusCodes.Status400BadRequest,
                CreateDocumentInProcessFailureKind.Validation,
                "folder_id route parameter contains an unresolved variable.");
        }

        return await UploadDocumentCore(
            request,
            processNumber,
            folderId,
            resolveFileIdFromProcessCode: true,
            cancellationToken);
    }

    /// <summary>
    /// Creates a document directly in the Gestiona file identified by the route parameter.
    /// </summary>
    /// <param name="processId">The Gestiona file identifier that will receive the new document.</param>
    /// <param name="request">The upload request containing the document metadata and source information.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// An <see cref="UploadDocumentResponse"/> containing the created document information on success,
    /// or an error payload when the request cannot be processed.
    /// </returns>
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
            return CreateErrorResponse(
                request.OperationId,
                StatusCodes.Status400BadRequest,
                CreateDocumentInProcessFailureKind.Validation,
                "process_id route parameter is required.");
        }

        if (string.Equals(processId, "{{process_id}}", StringComparison.Ordinal))
        {
            return CreateErrorResponse(
                request.OperationId,
                StatusCodes.Status400BadRequest,
                CreateDocumentInProcessFailureKind.Validation,
                "process_id route parameter contains an unresolved variable.");
        }

        return await UploadDocumentCore(
            request,
            processId,
            folderId: null,
            resolveFileIdFromProcessCode: false,
            cancellationToken);
    }

    /// <summary>
    /// Creates a document directly in the specified folder inside the Gestiona file identified by the route parameter.
    /// </summary>
    /// <param name="processId">The Gestiona file identifier that contains the target folder.</param>
    /// <param name="folderId">The Gestiona folder identifier that will receive the new document.</param>
    /// <param name="request">The upload request containing the document metadata and source information.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// An <see cref="UploadDocumentResponse"/> containing the created document information on success,
    /// or an error payload when the request cannot be processed.
    /// </returns>
    [HttpPost("{process_id}/documents/{folder_id}")]
    public async Task<ActionResult<UploadDocumentResponse>> UploadToFolder(
        [FromRoute(Name = "process_id")] string processId,
        [FromRoute(Name = "folder_id")] string folderId,
        [FromBody] UploadDocumentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "{Method} received upload request body for folder route {ProcessId}/{FolderId}:{NewLine}{Request}",
            nameof(UploadToFolder),
            processId,
            folderId,
            Environment.NewLine,
            JsonSerializer.Serialize(request, LogJsonOptions));

        if (string.IsNullOrWhiteSpace(processId))
        {
            return CreateErrorResponse(
                request.OperationId,
                StatusCodes.Status400BadRequest,
                CreateDocumentInProcessFailureKind.Validation,
                "process_id route parameter is required.");
        }

        if (string.Equals(processId, "{{process_id}}", StringComparison.Ordinal))
        {
            return CreateErrorResponse(
                request.OperationId,
                StatusCodes.Status400BadRequest,
                CreateDocumentInProcessFailureKind.Validation,
                "process_id route parameter contains an unresolved variable.");
        }

        if (string.IsNullOrWhiteSpace(folderId))
        {
            return CreateErrorResponse(
                request.OperationId,
                StatusCodes.Status400BadRequest,
                CreateDocumentInProcessFailureKind.Validation,
                "folder_id route parameter is required.");
        }

        if (string.Equals(folderId, "{{folder_id}}", StringComparison.Ordinal))
        {
            return CreateErrorResponse(
                request.OperationId,
                StatusCodes.Status400BadRequest,
                CreateDocumentInProcessFailureKind.Validation,
                "folder_id route parameter contains an unresolved variable.");
        }

        return await UploadDocumentCore(
            request,
            processId,
            folderId,
            resolveFileIdFromProcessCode: false,
            cancellationToken);
    }

    /// <summary>
    /// Executes the shared upload workflow for both upload endpoints.
    /// </summary>
    /// <param name="request">The upload request containing the document metadata and source information.</param>
    /// <param name="processId">The process identifier or Gestiona file identifier associated with the upload.</param>
    /// <param name="resolveFileIdFromProcessCode">Indicates whether the process identifier must first be resolved to a Gestiona file identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// An <see cref="UploadDocumentResponse"/> containing the created document information on success,
    /// or an error payload when the request cannot be processed.
    /// </returns>
    private async Task<ActionResult<UploadDocumentResponse>> UploadDocumentCore(
        UploadDocumentRequest request,
        string processId,
        string? folderId,
        bool resolveFileIdFromProcessCode,
        CancellationToken cancellationToken)
    {
        var documentsFolder = _configuration["DocumentStorage:BasePath"];
        var result = await _gestionaProcessService.CreateDocumentInProcessAsync(
            request,
            processId,
            folderId,
            resolveFileIdFromProcessCode,
            documentsFolder ?? string.Empty,
            cancellationToken);

        if (!result.Success)
        {
            var statusCode = result.FailureKind switch
            {
                CreateDocumentInProcessFailureKind.Configuration => StatusCodes.Status500InternalServerError,
                CreateDocumentInProcessFailureKind.Validation => StatusCodes.Status400BadRequest,
                CreateDocumentInProcessFailureKind.NotFound => StatusCodes.Status404NotFound,
                _ => result.UpstreamStatusCode ?? StatusCodes.Status502BadGateway
            };

            return CreateErrorResponse(
                request.OperationId,
                statusCode,
                result.FailureKind,
                result.ErrorMessage ?? "Unknown error.");
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

    /// <summary>
    /// Creates a standardized upload error response payload.
    /// </summary>
    /// <param name="operationId">The optional operation identifier associated with the request.</param>
    /// <param name="statusCode">The HTTP status code to return.</param>
    /// <param name="failureKind">The classified reason for the failure.</param>
    /// <param name="message">The human-readable error message.</param>
    /// <returns>An <see cref="ActionResult{TValue}"/> containing the upload error envelope.</returns>
    private ActionResult<UploadDocumentResponse> CreateErrorResponse(
        string? operationId,
        int statusCode,
        CreateDocumentInProcessFailureKind failureKind,
        string message)
    {
        return StatusCode(
            statusCode,
            new UploadDocumentResponse(
                operationId,
                false,
                new UploadDocumentError(
                    statusCode,
                    ReasonPhrases.GetReasonPhrase(statusCode),
                    failureKind.ToString(),
                    message)));
    }

}
