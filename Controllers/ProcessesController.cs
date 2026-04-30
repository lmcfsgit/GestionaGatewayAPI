using GestionaGatewayAPI.Configuration;
using GestionaGatewayAPI.Models;
using GestionaGatewayAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GestionaGatewayAPI.Controllers;

[ApiController]
[Route("processes")]
public sealed class ProcessesController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly GestionaOptions _gestionaOptions;
    private readonly IGestionaApiClient _gestionaApiClient;
    private readonly ILogger<ProcessesController> _logger;

    public ProcessesController(
        IConfiguration configuration,
        IOptions<GestionaOptions> gestionaOptions,
        IGestionaApiClient gestionaApiClient,
        ILogger<ProcessesController> logger)
    {
        _configuration = configuration;
        _gestionaOptions = gestionaOptions.Value;
        _gestionaApiClient = gestionaApiClient;
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<ActionResult<UploadDocumentResponse>> Upload(
        [FromQuery] UploadDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var gestionaApiBaseUrl = _gestionaOptions.GestionaApiBaseUrl;
        var accessToken = _gestionaOptions.AccessToken;
        var documentsFolder = _configuration["DocumentStorage:BasePath"];

        if (string.IsNullOrWhiteSpace(gestionaApiBaseUrl))
        {
            return Problem(
                detail: "Gestiona API base URL is not configured.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Problem(
                detail: "Gestiona access token is not configured.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        if (string.IsNullOrWhiteSpace(documentsFolder))
        {
            return Problem(
                detail: "Document storage path is not configured.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        // Directory.CreateDirectory(documentsFolder);

        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            return BadRequest(new { error = "filename query parameter is required." });
        }

        if (string.IsNullOrWhiteSpace(request.ProcessId))
        {
            return BadRequest(new { error = "process_id query parameter is required." });
        }

        // Resolve the Gestiona file identifier from the external process 
        // code before creating the document.
        var fileId = await _gestionaApiClient.GetFileIdFromProcessCode(
            gestionaApiBaseUrl,
            accessToken,
            request.ProcessId,
            cancellationToken);

        if (fileId is null)
        {
            return Problem(
                detail: "Failed to resolve Gestiona file ID.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        var safeFileName = Path.GetFileName(request.FileName);
        if (!string.Equals(request.FileName, safeFileName, StringComparison.Ordinal))
        {
            return BadRequest(new { error = "filename must not contain directory segments." });
        }

        var fullDocumentsFolder = Path.GetFullPath(documentsFolder);
        var fullPath = Path.GetFullPath(Path.Combine(fullDocumentsFolder, safeFileName));
        if (!fullPath.StartsWith(fullDocumentsFolder, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "filename resolves outside the configured document folder." });
        }

        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound(new { error = "Document not found.", filename = safeFileName, process_id = request.ProcessId });
        }

        // Read the document content from the file system.
        var content = await System.IO.File.ReadAllBytesAsync(fullPath, cancellationToken);
        // Create an upload space in Gestiona. 
        // This is a temporary location where the document content can be uploaded 
        // before creating the document in the file.
        var uploadLocation = await _gestionaApiClient.CreateUploadSpaceAsync(
            gestionaApiBaseUrl,
            accessToken,
            cancellationToken);
        if (uploadLocation is null)
        {
            return Problem(
                detail: "Failed to create upload space in Gestiona.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        // Upload the document content to the temporary upload location in Gestiona.
        var uploadSucceeded = await _gestionaApiClient.UploadDocumentContentAsync(
            gestionaApiBaseUrl,
            uploadLocation,
            accessToken,
            content,
            cancellationToken);
        if (!uploadSucceeded)
        {
            return Problem(
                detail: "Failed to upload document content to Gestiona.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        var createDocumentRequest = new CreateDocumentInFileRequest(
            Name: safeFileName,
            Type: "DIGITAL",
            MetadataLanguage: "ES",
            Trashed: "false",
            Version: "1",
            ContentHref: ResolveUploadHref(gestionaApiBaseUrl, uploadLocation));

        // Create the document in the resolved Gestiona file, using the uploaded content.
        var documentCreated = await _gestionaApiClient.CreateDocumentAndFolderAsync(
            gestionaApiBaseUrl,
            accessToken,
            fileId,
            createDocumentRequest,
            cancellationToken);

        if (!documentCreated)
        {
            return Problem(
                detail: "Failed to create document in Gestiona file.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        return Ok(new UploadDocumentResponse(
            request.ProcessId,
            safeFileName,
            fullPath,
            content.Length));
    }

    private static string ResolveUploadHref(string gestionaApiBaseUrl, string uploadLocation)
    {
        if (Uri.TryCreate(uploadLocation, UriKind.Absolute, out var absoluteUploadUri))
        {
            return absoluteUploadUri.ToString();
        }

        var normalizedBaseUrl = gestionaApiBaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? gestionaApiBaseUrl
            : $"{gestionaApiBaseUrl}/";

        return new Uri(new Uri(normalizedBaseUrl, UriKind.Absolute), uploadLocation).ToString();
    }
}
