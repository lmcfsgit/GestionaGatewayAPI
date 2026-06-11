using GestionaGatewayAPI.Models;
using GestionaGateway.Core.Models;
using GestionaGateway.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace GestionaGatewayAPI.Controllers;

[ApiController]
[Route("documents")]
public sealed class DocumentsController : ControllerBase
{
    private readonly IGestionaDocumentService _gestionaDocumentService;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IGestionaDocumentService gestionaDocumentService,
        ILogger<DocumentsController> logger)
    {
        _gestionaDocumentService = gestionaDocumentService;
        _logger = logger;
    }

    /// <summary>
    /// Downloads a document from Gestiona and returns its raw binary content.
    /// </summary>
    /// <param name="documentId">The identifier of the document to download.</param>
    /// <param name="operationId">An optional operation identifier echoed back in error responses and success headers.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A file response containing the document bytes on success, or an error payload when the download fails.
    /// </returns>
    [HttpGet("{document_id}")]
    public async Task<ActionResult> DownloadDocument(
        [FromRoute(Name = "document_id")] string documentId,
        [FromQuery(Name = "operationId")] string? operationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "{Method} received download request for document {DocumentId} with operationId {OperationId}",
            nameof(DownloadDocument),
            documentId,
            operationId);

        if (string.IsNullOrWhiteSpace(documentId))
        {
            return CreateDownloadErrorResponse(
                operationId,
                StatusCodes.Status400BadRequest,
                DownloadDocumentFailureKind.Validation,
                "document_id route parameter is required.");
        }

        if (string.Equals(documentId, "{{document_id}}", StringComparison.Ordinal))
        {
            return CreateDownloadErrorResponse(
                operationId,
                StatusCodes.Status400BadRequest,
                DownloadDocumentFailureKind.Validation,
                "document_id route parameter contains an unresolved variable.");
        }

        var result = await _gestionaDocumentService.DownloadDocumentAsync(
            documentId,
            GestionaRequestHeaders.GetAccessToken(Request),
            cancellationToken);

        if (!result.Success)
        {
            var statusCode = result.FailureKind switch
            {
                DownloadDocumentFailureKind.Configuration => StatusCodes.Status500InternalServerError,
                DownloadDocumentFailureKind.Validation => StatusCodes.Status400BadRequest,
                DownloadDocumentFailureKind.NotFound => StatusCodes.Status404NotFound,
                _ => result.UpstreamStatusCode ?? StatusCodes.Status502BadGateway
            };

            return CreateDownloadErrorResponse(
                operationId,
                statusCode,
                result.FailureKind,
                result.ErrorMessage ?? "Unknown error.");
        }

        var document = result.Document!;
        var contentType = string.IsNullOrWhiteSpace(document.ContentType)
            ? "application/octet-stream"
            : document.ContentType;

        var fileName = ResolveDownloadFileName(document);
        if (!string.IsNullOrWhiteSpace(operationId))
        {
            Response.Headers["X-Operation-Id"] = operationId;
        }

        if (!string.IsNullOrWhiteSpace(document.StorageExtension))
        {
            Response.Headers["X-Storage-Extension"] = document.StorageExtension;
        }

        return File(document.Content, contentType, fileName);
    }

    /// <summary>
    /// Creates a standardized download error response payload.
    /// </summary>
    /// <param name="operationId">The optional operation identifier associated with the request.</param>
    /// <param name="statusCode">The HTTP status code to return.</param>
    /// <param name="failureKind">The classified reason for the failure.</param>
    /// <param name="message">The human-readable error message.</param>
    /// <returns>An <see cref="ActionResult"/> containing the download error envelope.</returns>
    private ActionResult CreateDownloadErrorResponse(
        string? operationId,
        int statusCode,
        DownloadDocumentFailureKind failureKind,
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

    /// <summary>
    /// Resolves the file name used in the download response.
    /// </summary>
    /// <param name="document">The downloaded document metadata returned by the service layer.</param>
    /// <returns>
    /// The explicit file name when present; otherwise a name built from the document identifier and storage extension,
    /// or the document identifier alone as a final fallback.
    /// </returns>
    private static string ResolveDownloadFileName(DownloadedDocument document)
    {
        if (!string.IsNullOrWhiteSpace(document.FileName))
        {
            return document.FileName;
        }

        if (!string.IsNullOrWhiteSpace(document.StorageExtension))
        {
            return $"{document.DocumentId}.{document.StorageExtension}";
        }

        return document.DocumentId;
    }
}
