using GestionaGatewayAPI.Configuration;
using GestionaGatewayAPI.Models;
using GestionaGatewayAPI.Services;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

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

    [HttpPost("documents")]
    public async Task<ActionResult<UploadDocumentResponse>> Upload(
        [FromQuery(Name = "process_number")] string processNumber,
        [FromBody] UploadDocumentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received upload request body for process lookup route {ProcessNumber}:{NewLine}{Request}",
            processNumber,
            Environment.NewLine,
            JsonSerializer.Serialize(request, LogJsonOptions));

        if (!string.Equals(request.DocumentSourceType, "FILE", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "documentSourceType must be FILE." });
        }

        if (string.IsNullOrWhiteSpace(processNumber))
        {
            return BadRequest(new { error = "process_number query parameter is required." });
        }

        return await UploadDocumentCore(
            request.OperationId,
            request.Name,
            request.FileName,
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
        _logger.LogInformation(
            "Received upload request body for direct file route {ProcessId}:{NewLine}{Request}",
            processId,
            Environment.NewLine,
            JsonSerializer.Serialize(request, LogJsonOptions));

        if (!string.Equals(request.DocumentSourceType, "FILE", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "documentSourceType must be FILE." });
        }

        if (string.IsNullOrWhiteSpace(processId))
        {
            return BadRequest(new { error = "process_id route parameter is required." });
        }

        return await UploadDocumentCore(
            request.OperationId,
            request.Name,
            request.FileName,
            processId,
            resolveFileIdFromProcessCode: false,
            cancellationToken);
    }

    private async Task<ActionResult<UploadDocumentResponse>> UploadDocumentCore(
        string? operationId,
        string? documentName,
        string? fileName,
        string processId,
        bool resolveFileIdFromProcessCode,
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

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest(new { error = "fileName is required in the request body." });
        }

        string? fileId = processId;
        if (resolveFileIdFromProcessCode)
        {
            // Resolve the Gestiona file identifier from the external process 
            // code before creating the document.
            fileId = await _gestionaApiClient.GetFileIdFromProcessCode(
                gestionaApiBaseUrl,
                accessToken,
                processId,
                cancellationToken);

            if (fileId is null)
            {
                return Problem(
                    detail: "Failed to resolve Gestiona file ID.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
        }

        var safeFileName = Path.GetFileName(fileName);
        if (!string.Equals(fileName, safeFileName, StringComparison.Ordinal))
        {
            return BadRequest(new { error = "filename must not contain directory segments." });
        }

        var fullDocumentsFolder = Path.GetFullPath(documentsFolder);
        var fullDocumentsFolderWithSeparator = fullDocumentsFolder.EndsWith(Path.DirectorySeparatorChar)
            ? fullDocumentsFolder
            : fullDocumentsFolder + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(fullDocumentsFolder, safeFileName));
        if (!fullPath.StartsWith(fullDocumentsFolderWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "filename resolves outside the configured document folder." });
        }

        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound(new { error = "Document not found.", filename = safeFileName, process_id = processId });
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

        // Prepare the request to create the document in Gestiona, using the uploaded content.
        var createDocumentRequest = new CreateDocumentInFileRequest(
            Name: string.IsNullOrWhiteSpace(documentName) ? safeFileName : documentName,
            Type: "DIGITAL",
            MetadataLanguage: "ES",
            Trashed: "false",
            Version: "1",
            ContentHref: ResolveUploadHref(gestionaApiBaseUrl, uploadLocation));

        // Create the document in the resolved Gestiona file, using the uploaded content.
        var createdDocument = await _gestionaApiClient.CreateDocumentAndFolderAsync(
            gestionaApiBaseUrl,
            accessToken,
            fileId,
            createDocumentRequest,
            cancellationToken);

        if (createdDocument is null)
        {
            return Problem(
                detail: "Failed to create document in Gestiona file.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        // Return the details of the created document in the response.
        return Ok(new UploadDocumentResponse(
            operationId,
            true,
            new UploadDocumentResult(
                createdDocument.Id,
                fileId,
                FormatUnixTimestamp(createdDocument.CreationDate),
                FormatUnixTimestamp(createdDocument.ModificationDate))));
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

    private static string FormatUnixTimestamp(string unixTimestamp)
    {
        if (!long.TryParse(unixTimestamp, out var unixSeconds))
        {
            return unixTimestamp;
        }

        var portugalTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");
        var portugalTime = TimeZoneInfo.ConvertTime(
            DateTimeOffset.FromUnixTimeSeconds(unixSeconds),
            portugalTimeZone);

        return portugalTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }
}
