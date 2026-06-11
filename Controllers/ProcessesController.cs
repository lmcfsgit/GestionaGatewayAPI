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
    /// Gets the third identifiers associated with a Gestiona process resolved from a process number.
    /// </summary>
    /// <param name="processNumber">The external process number used to resolve the Gestiona file identifier.</param>
    /// <param name="operationId">An optional operation identifier echoed back in the response envelope.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A payload containing the resolved process identifier and semicolon-separated third identifiers.</returns>
    [HttpGet("thirds")]
    public async Task<ActionResult<GatewayResponse>> GetThirdsByProcessNumber(
        [FromQuery(Name = "process_number")] string processNumber,
        [FromQuery(Name = "operationId")] string? operationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "{Method} received process thirds request for process number {ProcessNumber} with operationId {OperationId}",
            nameof(GetThirdsByProcessNumber),
            processNumber,
            operationId);

        if (string.IsNullOrWhiteSpace(processNumber))
        {
            return CreateProcessThirdsErrorResponse(
                operationId,
                StatusCodes.Status400BadRequest,
                GetProcessThirdsFailureKind.Validation,
                "process_number query parameter is required.");
        }

        if (string.Equals(processNumber, "{{process_number}}", StringComparison.Ordinal))
        {
            return CreateProcessThirdsErrorResponse(
                operationId,
                StatusCodes.Status400BadRequest,
                GetProcessThirdsFailureKind.Validation,
                "process_number query parameter contains an unresolved variable.");
        }

        return await GetThirdsCore(
            processNumber,
            resolveFileIdFromProcessCode: true,
            operationId,
            cancellationToken);
    }

    /// <summary>
    /// Gets the third identifiers associated with a Gestiona process.
    /// </summary>
    /// <param name="processId">The Gestiona file identifier whose third parties should be retrieved.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A payload containing the process identifier and semicolon-separated third identifiers.</returns>
    [HttpGet("{process_id}/thirds")]
    public async Task<ActionResult<GatewayResponse>> GetThirds(
        [FromRoute(Name = "process_id")] string processId,
        [FromQuery(Name = "operationId")] string? operationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "{Method} received process thirds request for {ProcessId} with operationId {OperationId}",
            nameof(GetThirds),
            processId,
            operationId);

        if (string.IsNullOrWhiteSpace(processId))
        {
            return CreateProcessThirdsErrorResponse(
                operationId,
                StatusCodes.Status400BadRequest,
                GetProcessThirdsFailureKind.Validation,
                "process_id route parameter is required.");
        }

        if (string.Equals(processId, "{{process_id}}", StringComparison.Ordinal))
        {
            return CreateProcessThirdsErrorResponse(
                operationId,
                StatusCodes.Status400BadRequest,
                GetProcessThirdsFailureKind.Validation,
                "process_id route parameter contains an unresolved variable.");
        }

        return await GetThirdsCore(
            processId,
            resolveFileIdFromProcessCode: false,
            operationId,
            cancellationToken);
    }

    private async Task<ActionResult<GatewayResponse>> GetThirdsCore(
        string processId,
        bool resolveFileIdFromProcessCode,
        string? operationId,
        CancellationToken cancellationToken)
    {
        var result = await _gestionaProcessService.GetProcessThirdsAsync(
            processId,
            resolveFileIdFromProcessCode,
            GestionaRequestHeaders.GetAccessToken(Request),
            cancellationToken);

        if (!result.Success)
        {
            var statusCode = result.FailureKind switch
            {
                GetProcessThirdsFailureKind.Configuration => StatusCodes.Status500InternalServerError,
                GetProcessThirdsFailureKind.Validation => StatusCodes.Status400BadRequest,
                GetProcessThirdsFailureKind.NotFound => StatusCodes.Status404NotFound,
                _ => result.UpstreamStatusCode ?? StatusCodes.Status502BadGateway
            };

            return CreateProcessThirdsErrorResponse(
                operationId,
                statusCode,
                result.FailureKind,
                result.ErrorMessage ?? "Unknown error.");
        }

        return Ok(new GatewayResponse(
            operationId,
            true,
            new ProcessThirdsResponse(
                result.ProcessId!,
                result.Thirds ?? string.Empty)));
    }

    /// <summary>
    /// Creates a document by resolving the target Gestiona file from the provided process number.
    /// </summary>
    /// <param name="processNumber">The external process number used to resolve the Gestiona file identifier.</param>
    /// <param name="request">The upload request containing the document metadata and source information.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// An <see cref="GatewayResponse"/> containing the created document information on success,
    /// or an error payload when the request cannot be processed.
    /// </returns>
    [HttpPost("documents")]
    public async Task<ActionResult<GatewayResponse>> Upload(
        [FromQuery(Name = "process_number")] string processNumber,
        [FromBody] UploadDocumentRequest request,
        CancellationToken cancellationToken)
    {
        LogUploadRequest(
            LogLevel.Information,
            nameof(Upload),
            "process lookup route {ProcessNumber}",
            request,
            processNumber);

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
    /// An <see cref="GatewayResponse"/> containing the created document information on success,
    /// or an error payload when the request cannot be processed.
    /// </returns>
    [HttpPost("documents/{folder_id}")]
    public async Task<ActionResult<GatewayResponse>> UploadToResolvedFolder(
        [FromRoute(Name = "folder_id")] string folderId,
        [FromQuery(Name = "process_number")] string processNumber,
        [FromBody] UploadDocumentRequest request,
        CancellationToken cancellationToken)
    {
        LogUploadRequest(
            LogLevel.Information,
            nameof(UploadToResolvedFolder),
            "process lookup folder route {ProcessNumber}/{FolderId}",
            request,
            processNumber,
            folderId);

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
    /// An <see cref="GatewayResponse"/> containing the created document information on success,
    /// or an error payload when the request cannot be processed.
    /// </returns>
    [HttpPost("{process_id}/documents")]
    public async Task<ActionResult<GatewayResponse>> UploadToFile(
        [FromRoute(Name = "process_id")] string processId,
        [FromBody] UploadDocumentRequest request,
        CancellationToken cancellationToken)
    {
        LogUploadRequest(
            LogLevel.Debug,
            nameof(UploadToFile),
            "direct file route {ProcessId}",
            request,
            processId);

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
    /// An <see cref="GatewayResponse"/> containing the created document information on success,
    /// or an error payload when the request cannot be processed.
    /// </returns>
    [HttpPost("{process_id}/documents/{folder_id}")]
    public async Task<ActionResult<GatewayResponse>> UploadToFolder(
        [FromRoute(Name = "process_id")] string processId,
        [FromRoute(Name = "folder_id")] string folderId,
        [FromBody] UploadDocumentRequest request,
        CancellationToken cancellationToken)
    {
        LogUploadRequest(
            LogLevel.Debug,
            nameof(UploadToFolder),
            "folder route {ProcessId}/{FolderId}",
            request,
            processId,
            folderId);

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
    /// An <see cref="GatewayResponse"/> containing the created document information on success,
    /// or an error payload when the request cannot be processed.
    /// </returns>
    private async Task<ActionResult<GatewayResponse>> UploadDocumentCore(
        UploadDocumentRequest request,
        string processId,
        string? folderId,
        bool resolveFileIdFromProcessCode,
        CancellationToken cancellationToken)
    {
        var documentsFolder = _configuration["DocumentStorage:BasePath"];
        var gestionaAccessToken = GestionaRequestHeaders.GetAccessToken(Request);
        _logger.LogDebug(
            "({Method}) resolved request Gestiona access token {GestionaAccessToken}",
            nameof(UploadDocumentCore),
            MaskAccessToken(gestionaAccessToken));

        var result = await _gestionaProcessService.CreateDocumentInProcessAsync(
            request,
            processId,
            folderId,
            resolveFileIdFromProcessCode,
            documentsFolder ?? string.Empty,
            gestionaAccessToken,
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

        return Ok(new GatewayResponse(
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
    private ActionResult<GatewayResponse> CreateErrorResponse(
        string? operationId,
        int statusCode,
        CreateDocumentInProcessFailureKind failureKind,
        string message)
    {
        return StatusCode(
            statusCode,
            new GatewayResponse(
                operationId,
                false,
                new UploadDocumentError(
                    statusCode,
                    ReasonPhrases.GetReasonPhrase(statusCode),
                    failureKind.ToString(),
                    message)));
    }

    private ActionResult<GatewayResponse> CreateProcessThirdsErrorResponse(
        string? operationId,
        int statusCode,
        GetProcessThirdsFailureKind failureKind,
        string message)
    {
        return StatusCode(
            statusCode,
            new GatewayResponse(
                operationId,
                false,
                new ProcessThirdsError(
                    statusCode,
                    ReasonPhrases.GetReasonPhrase(statusCode),
                    failureKind.ToString(),
                    message)));
    }


    /// <summary>
    /// Logs a sanitized representation of the upload request without writing raw document content to the logs.
    /// </summary>
    /// <param name="logLevel">The log level to use for the entry.</param>
    /// <param name="methodName">The controller method name associated with the request.</param>
    /// <param name="routeDescriptionTemplate">The route description template containing placeholders for route values.</param>
    /// <param name="request">The upload request to log.</param>
    /// <param name="routeValues">The route values used to render the route description.</param>
    private void LogUploadRequest(
        LogLevel logLevel,
        string methodName,
        string routeDescriptionTemplate,
        UploadDocumentRequest request,
        params object?[] routeValues)
    {
        var routeDescription = BuildRouteDescription(routeDescriptionTemplate, routeValues);
        var sanitizedRequest = new
        {
            request.OperationId,
            request.Name,
            request.FileName,
            request.DocumentSourceType,
            request.Url,
            HasContent = !string.IsNullOrWhiteSpace(request.Content)
        };

        _logger.Log(
            logLevel,
            "({Method}) received upload request body for {RouteDescription}:{NewLine}{Request}",
            methodName,
            routeDescription,
            Environment.NewLine,
            JsonSerializer.Serialize(sanitizedRequest, LogJsonOptions));
    }

    /// <summary>
    /// Replaces placeholder tokens in the route description template with the provided route values in order.
    /// </summary>
    /// <param name="routeDescriptionTemplate">The template containing placeholder tokens such as <c>{ProcessId}</c>.</param>
    /// <param name="routeValues">The values that should be inserted into the template.</param>
    /// <returns>The rendered route description.</returns>
    private static string BuildRouteDescription(
        string routeDescriptionTemplate,
        object?[] routeValues)
    {
        var routeDescription = routeDescriptionTemplate;
        foreach (var routeValue in routeValues)
        {
            var placeholderStart = routeDescription.IndexOf('{');
            var placeholderEnd = routeDescription.IndexOf('}', placeholderStart + 1);
            if (placeholderStart < 0 || placeholderEnd < 0)
            {
                break;
            }

            routeDescription = routeDescription.Remove(
                placeholderStart,
                placeholderEnd - placeholderStart + 1)
                .Insert(placeholderStart, routeValue?.ToString() ?? string.Empty);
        }

        return routeDescription;
    }

    private static string MaskAccessToken(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return "<none>";
        }

        return accessToken.Length <= 8
            ? "********"
            : $"{accessToken[..4]}...{accessToken[^4..]}";
    }

}
