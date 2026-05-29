using System.Globalization;
using System.Text.Json;
using GestionaGateway.Core.Configuration;
using GestionaGateway.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestionaGateway.Core.Services;

/// <summary>
/// Provides process-scoped document creation workflows for the Gestiona API.
/// </summary>
public sealed class GestionaProcessService : IGestionaProcessService
{
    private readonly GestionaOptions _gestionaOptions;
    private readonly IGestionaApiClient _gestionaApiClient;
    private readonly ILogger<GestionaProcessService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GestionaProcessService"/> class.
    /// </summary>
    /// <param name="gestionaOptions">The configured Gestiona options.</param>
    /// <param name="gestionaApiClient">The client used to communicate with the Gestiona API.</param>
    /// <param name="logger">The logger used for operational and diagnostic events.</param>
    public GestionaProcessService(
        IOptions<GestionaOptions> gestionaOptions,
        IGestionaApiClient gestionaApiClient,
        ILogger<GestionaProcessService> logger)
    {
        _gestionaOptions = gestionaOptions.Value;
        _gestionaApiClient = gestionaApiClient;
        _logger = logger;
    }

    /// <summary>
    /// Creates a document in Gestiona for the specified process.
    /// </summary>
    /// <param name="request">The upload request that describes the document source and metadata.</param>
    /// <param name="processId">The process identifier or Gestiona file identifier.</param>
    /// <param name="resolveFileIdFromProcessCode">Indicates whether the process identifier must first be resolved to a Gestiona file identifier.</param>
    /// <param name="documentsFolder">The base folder used to resolve file uploads from local storage.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The result of the document creation workflow.</returns>
    public async Task<CreateDocumentInProcessResult> CreateDocumentInProcessAsync(
        UploadDocumentRequest request,
        string processId,
        string? folderId,
        bool resolveFileIdFromProcessCode,
        string documentsFolder,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "({Method}) started. ProcessId={ProcessId}, FolderId={FolderId}, " +
            "ResolveFileIdFromProcessCode={ResolveFileIdFromProcessCode}, " +
            "DocumentSourceType={DocumentSourceType}, FileName={FileName}, " +
            "HasContent={HasContent}, HasExternalUrl={HasExternalUrl}",
            nameof(CreateDocumentInProcessAsync),
            processId,
            folderId,
            resolveFileIdFromProcessCode,
            request.DocumentSourceType,
            request.FileName,
            !string.IsNullOrWhiteSpace(request.Content),
            !string.IsNullOrWhiteSpace(request.Url));

        var gestionaApiBaseUrl = _gestionaOptions.GestionaApiBaseUrl;
        var accessToken = _gestionaOptions.AccessToken;

        if (string.IsNullOrWhiteSpace(gestionaApiBaseUrl))
        {
            _logger.LogWarning("({Method}) failed at step {Step} for process {ProcessId}", nameof(CreateDocumentInProcessAsync), "ValidateConfiguration:GestionaApiBaseUrl", processId);
            return Failure(CreateDocumentInProcessFailureKind.Configuration, "Gestiona API base URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _logger.LogWarning("({Method}) failed at step {Step} for process {ProcessId}", nameof(CreateDocumentInProcessAsync), "ValidateConfiguration:AccessToken", processId);
            return Failure(CreateDocumentInProcessFailureKind.Configuration, "Gestiona access token is not configured.");
        }

        string? fileId = processId;
        if (resolveFileIdFromProcessCode)
        {
            _logger.LogDebug("({Method}) resolving Gestiona file id for process {ProcessId}",
                        nameof(CreateDocumentInProcessAsync), processId);

            // When the process identifier is not a Gestiona file ID, attempt to resolve it using the dedicated API. 
            // This supports scenarios where the process ID corresponds to a business-specific code - process_number - that must be 
            // translated to a file ID for document operations.
            var fileIdResult = await _gestionaApiClient.GetFileIdFromProcessCode(
                gestionaApiBaseUrl,
                accessToken,
                processId,
                cancellationToken);

            fileId = fileIdResult.Value;

            if (!fileIdResult.Success || fileId is null)
            {
                _logger.LogWarning("({Method}) failed at step {Step} for process {ProcessId}", nameof(CreateDocumentInProcessAsync), "ResolveFileIdFromProcessCode", processId);
                var failureKind = fileIdResult.StatusCode == 204
                    ? CreateDocumentInProcessFailureKind.NotFound
                    : CreateDocumentInProcessFailureKind.Upstream;
                var errorMessage = fileIdResult.StatusCode == 204
                    ? $"No Gestiona file was found for process number: {processId}."
                    : "Failed to resolve Gestiona file ID.";
                return Failure(failureKind, errorMessage, fileIdResult.StatusCode);
            }

            _logger.LogInformation("({Method}) resolved Gestiona file id {FileId} for process {ProcessId}", nameof(CreateDocumentInProcessAsync), fileId, processId);
        }

        var documentName = string.IsNullOrWhiteSpace(request.Name)
            ? "document"
            : request.Name;

        var createDocumentRequest = new CreateDocumentInFileRequest
        {
            Name = documentName,
            MetadataLanguage = "ES", // Is this important to set? Should it be configurable?
            Trashed = "false",
            Version = "1" // ?? Is this required for creation? Should it be configurable?
        };

        CreateDocumentAndFolderResponse? createdDocument;

        // The document creation flow diverges based on the declared source type. DIGITAL uploads first create
        // temporary content in Gestiona, EXTERNAL_URL uses the provided URL directly, and FOLDER sends a
        // folder-specific payload to the same documents-and-folders endpoint.
        if (string.Equals(request.DocumentSourceType, "FOLDER", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("({Method}) using FOLDER document flow for process {ProcessId}", nameof(CreateDocumentInProcessAsync), processId);



            createDocumentRequest = createDocumentRequest with
            {
                Line = "1" // ?? Is this required for folder creation? Should it be configurable or auto-generated?
            };

            _logger.LogDebug(
                "({Method}) CreateDocumentRequest for FOLDER:{NewLine}{CreateDocumentRequest}",
                nameof(CreateDocumentInProcessAsync),
                Environment.NewLine,
                JsonSerializer.Serialize(createDocumentRequest, new JsonSerializerOptions { WriteIndented = true }));

            var createdDocumentResult = await _gestionaApiClient.CreateFolderAsync(
                gestionaApiBaseUrl,
                accessToken,
                fileId,
                folderId,
                createDocumentRequest,
                cancellationToken);
            createdDocument = createdDocumentResult.Value;
            if (!createdDocumentResult.Success)
            {
                _logger.LogWarning("({Method}) failed at step {Step} for process {ProcessId}", nameof(CreateDocumentInProcessAsync), "CreateFolderInGestiona", processId);
                return Failure(CreateDocumentInProcessFailureKind.Upstream, "Failed to create folder in Gestiona file.", createdDocumentResult.StatusCode);
            }
        }
        else if (string.Equals(request.DocumentSourceType, "DIGITAL", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("({Method}) using DIGITAL document flow for process {ProcessId}", nameof(CreateDocumentInProcessAsync), processId);

            if (string.IsNullOrWhiteSpace(request.FileName) &&
                string.IsNullOrWhiteSpace(request.Content))
            {
                _logger.LogWarning("({Method}) failed at step {Step} for process {ProcessId}", nameof(CreateDocumentInProcessAsync), "ValidateDigitalDocumentInput", processId);
                return Failure(CreateDocumentInProcessFailureKind.Validation, "For DIGITAL documents, either fileName or content must be provided.");
            }

            var uploadFile = await GetUploadFileAsync(
                request.FileName,
                request.Content,
                documentsFolder,
                cancellationToken);
            if (uploadFile.FailureResult is not null)
            {
                _logger.LogWarning("({Method}) failed at step {Step} for process {ProcessId}: {ErrorMessage}", nameof(CreateDocumentInProcessAsync), "GetUploadFile", processId, uploadFile.FailureResult.ErrorMessage);
                return uploadFile.FailureResult;
            }

            var content = uploadFile.Content!;

            _logger.LogDebug(
                "({Method}) uploading document content. Source={Source}, FileName={FileName}, ContentLength={ContentLength}",
                nameof(CreateDocumentInProcessAsync),
                string.IsNullOrWhiteSpace(request.Content) ? "file" : "base64",
                request.FileName,
                content.Length);

            var uploadLocationResult = await _gestionaApiClient.CreateUploadSpaceAsync(
                gestionaApiBaseUrl,
                accessToken,
                cancellationToken);

            var uploadLocation = uploadLocationResult.Value;

            if (!uploadLocationResult.Success || uploadLocation is null)
            {
                _logger.LogWarning("({Method}) failed at step {Step} for process {ProcessId}", nameof(CreateDocumentInProcessAsync), "CreateUploadSpace", processId);
                return Failure(CreateDocumentInProcessFailureKind.Upstream, "Failed to create upload space in Gestiona.", uploadLocationResult.StatusCode);
            }

            var uploadResult = await _gestionaApiClient.UploadDocumentContentAsync(
                gestionaApiBaseUrl,
                uploadLocation,
                accessToken,
                content,
                cancellationToken);
            if (!uploadResult.Success)
            {
                _logger.LogWarning("({Method}) failed at step {Step} for process {ProcessId}", nameof(CreateDocumentInProcessAsync), "UploadDocumentContent", processId);
                return Failure(CreateDocumentInProcessFailureKind.Upstream, "Failed to upload document content to Gestiona.", uploadResult.StatusCode);
            }

            createDocumentRequest = createDocumentRequest with
            {
                Type = "DIGITAL",
                ContentHref = ResolveUploadHref(gestionaApiBaseUrl, uploadLocation)
            };

            _logger.LogDebug(
                "({Method}) CreateDocumentRequest for DIGITAL:{NewLine}{CreateDocumentRequest}",
                nameof(CreateDocumentInProcessAsync),
                Environment.NewLine,
                JsonSerializer.Serialize(createDocumentRequest, new JsonSerializerOptions { WriteIndented = true }));

            var createdDocumentResult = await _gestionaApiClient.CreateDocumentAndFolderAsync(
                gestionaApiBaseUrl,
                accessToken,
                fileId,
                folderId,
                createDocumentRequest,
                cancellationToken);
            createdDocument = createdDocumentResult.Value;
            if (!createdDocumentResult.Success)
            {
                _logger.LogWarning("({Method}) failed at step {Step} for process {ProcessId}", nameof(CreateDocumentInProcessAsync), "CreateDocumentInGestiona", processId);
                return Failure(CreateDocumentInProcessFailureKind.Upstream, "Failed to create document in Gestiona file.", createdDocumentResult.StatusCode);
            }
        }
        else
        {
            _logger.LogDebug("({Method}) using EXTERNAL_URL document flow for process {ProcessId}", nameof(CreateDocumentInProcessAsync), processId);

            createDocumentRequest = createDocumentRequest with
            {
                Type = "EXTERNAL_URL",
                ExternalUrl = request.Url
            };

            _logger.LogDebug(
                "({Method}) CreateDocumentRequest for EXTERNAL_URL:{NewLine}{CreateDocumentRequest}",
                nameof(CreateDocumentInProcessAsync),
                Environment.NewLine,
                JsonSerializer.Serialize(createDocumentRequest, new JsonSerializerOptions { WriteIndented = true }));

            var createdDocumentResult = await _gestionaApiClient.CreateDocumentUrlAsync(
                gestionaApiBaseUrl,
                accessToken,
                fileId,
                folderId,
                createDocumentRequest,
                cancellationToken);
            createdDocument = createdDocumentResult.Value;
            if (!createdDocumentResult.Success)
            {
                _logger.LogWarning("({Method}) failed at step {Step} for process {ProcessId}", nameof(CreateDocumentInProcessAsync), "CreateDocumentUrlInGestiona", processId);
                return Failure(CreateDocumentInProcessFailureKind.Upstream, "Failed to create document in Gestiona file.", createdDocumentResult.StatusCode);
            }
        }

        var createdEntityId = createdDocument?.GetResolvedId();
        if (createdDocument is null || string.IsNullOrWhiteSpace(createdEntityId))
        {
            _logger.LogWarning("({Method}) failed at step {Step} for process {ProcessId}", nameof(CreateDocumentInProcessAsync), "CreateDocumentInGestiona", processId);
            return Failure(CreateDocumentInProcessFailureKind.Upstream, "Failed to create document in Gestiona file.");
        }

        _logger.LogInformation(
            "({Method}) succeeded. ProcessId={ProcessId}, FileId={FileId}, " +
            "FolderId={FolderId}, DocumentId={DocumentId}, " +
            "SourceType={DocumentSourceType}",
            nameof(CreateDocumentInProcessAsync),
            processId,
            fileId,
            folderId,
            createdEntityId,
            request.DocumentSourceType);

        _logger.LogDebug(
            "({Method}) created Gestiona document payload:{NewLine}{CreatedDocument}",
            nameof(CreateDocumentInProcessAsync),
            Environment.NewLine,
            JsonSerializer.Serialize(createdDocument, new JsonSerializerOptions { WriteIndented = true }));

        return new CreateDocumentInProcessResult(
            true,
            CreateDocumentInProcessFailureKind.None,
            null,
            new CreateDocumentInProcessDocument(
                createdEntityId,
                fileId,
                FormatUnixTimestamp(createdDocument.CreationDate),
                FormatUnixTimestamp(createdDocument.ModificationDate)),
            null);
    }

    public async Task<GetProcessThirdsResult> GetProcessThirdsAsync(
        string processId,
        bool resolveFileIdFromProcessCode,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "({Method}) started. ProcessId={ProcessId}, ResolveFileIdFromProcessCode={ResolveFileIdFromProcessCode}",
            nameof(GetProcessThirdsAsync),
            processId,
            resolveFileIdFromProcessCode);

        var gestionaApiBaseUrl = _gestionaOptions.GestionaApiBaseUrl;
        var accessToken = _gestionaOptions.AccessToken;

        if (string.IsNullOrWhiteSpace(gestionaApiBaseUrl))
        {
            _logger.LogWarning("({Method}) failed at step {Step} for process {ProcessId}", nameof(GetProcessThirdsAsync), "ValidateConfiguration:GestionaApiBaseUrl", processId);
            return ThirdsFailure(GetProcessThirdsFailureKind.Configuration, "Gestiona API base URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _logger.LogWarning("({Method}) failed at step {Step} for process {ProcessId}", nameof(GetProcessThirdsAsync), "ValidateConfiguration:AccessToken", processId);
            return ThirdsFailure(GetProcessThirdsFailureKind.Configuration, "Gestiona access token is not configured.");
        }

        if (string.IsNullOrWhiteSpace(processId))
        {
            _logger.LogWarning("({Method}) failed at step {Step}", nameof(GetProcessThirdsAsync), "ValidateProcessId");
            var inputName = resolveFileIdFromProcessCode ? "processNumber" : "processId";
            return ThirdsFailure(GetProcessThirdsFailureKind.Validation, $"{inputName} is required.");
        }

        var fileId = processId;
        if (resolveFileIdFromProcessCode)
        {
            _logger.LogDebug(
                "({Method}) resolving Gestiona file id for process number {ProcessNumber}",
                nameof(GetProcessThirdsAsync),
                processId);

            var fileIdResult = await _gestionaApiClient.GetFileIdFromProcessCode(
                gestionaApiBaseUrl,
                accessToken,
                processId,
                cancellationToken);

            if (!fileIdResult.Success || string.IsNullOrWhiteSpace(fileIdResult.Value))
            {
                _logger.LogWarning("({Method}) failed at step {Step} for process number {ProcessNumber}", nameof(GetProcessThirdsAsync), "ResolveFileIdFromProcessCode", processId);
                var failureKind = fileIdResult.StatusCode == 204
                    ? GetProcessThirdsFailureKind.NotFound
                    : GetProcessThirdsFailureKind.Upstream;
                var errorMessage = fileIdResult.StatusCode == 204
                    ? $"No Gestiona file was found for process number: {processId}."
                    : "Failed to resolve Gestiona file ID.";
                return ThirdsFailure(failureKind, errorMessage, GetUpstreamErrorStatusCode(fileIdResult.StatusCode));
            }

            fileId = fileIdResult.Value;
            _logger.LogInformation("({Method}) resolved Gestiona file id {FileId} for process number {ProcessNumber}", nameof(GetProcessThirdsAsync), fileId, processId);
        }

        var thirdIdsResult = await _gestionaApiClient.GetProcessThirdIdsAsync(
            gestionaApiBaseUrl,
            accessToken,
            fileId,
            cancellationToken);

        if (!thirdIdsResult.Success)
        {
            _logger.LogWarning("({Method}) failed at step {Step} for process {ProcessId}", nameof(GetProcessThirdsAsync), "GetProcessThirdPartiesFromGestiona", fileId);
            var failureKind = thirdIdsResult.StatusCode == 404
                ? GetProcessThirdsFailureKind.NotFound
                : GetProcessThirdsFailureKind.Upstream;
            return ThirdsFailure(
                failureKind,
                $"Failed to get third parties from Gestiona process: {fileId}.",
                GetUpstreamErrorStatusCode(thirdIdsResult.StatusCode));
        }

        var thirdIds = string.Join(';', thirdIdsResult.Value ?? []);

        _logger.LogInformation(
            "({Method}) succeeded. ProcessId={ProcessId}, ThirdCount={ThirdCount}",
            nameof(GetProcessThirdsAsync),
            fileId,
            thirdIdsResult.Value?.Count ?? 0);

        return new GetProcessThirdsResult(
            true,
            GetProcessThirdsFailureKind.None,
            null,
            fileId,
            thirdIds,
            null);
    }

    private static CreateDocumentInProcessResult Failure(
        CreateDocumentInProcessFailureKind failureKind,
        string errorMessage,
        int? upstreamStatusCode = null)
    {
        return new CreateDocumentInProcessResult(false, failureKind, errorMessage, null, upstreamStatusCode);
    }

    private static GetProcessThirdsResult ThirdsFailure(
        GetProcessThirdsFailureKind failureKind,
        string errorMessage,
        int? upstreamStatusCode = null)
    {
        return new GetProcessThirdsResult(false, failureKind, errorMessage, null, null, upstreamStatusCode);
    }

    private static int? GetUpstreamErrorStatusCode(int statusCode)
    {
        return statusCode >= 400
            ? statusCode
            : null;
    }

    /// <summary>
    /// Resolves the upload content from either inline base64 data or a file stored in the configured documents folder.
    /// </summary>
    /// <param name="fileName">The file name to resolve from the documents folder when inline content is not provided.</param>
    /// <param name="base64Content">The optional base64-encoded content that, when present, is used instead of reading from disk.</param>
    /// <param name="documentsFolder">The configured base folder that contains uploadable documents.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous file read operation.</param>
    /// <returns>
    /// An <see cref="UploadFileResult"/> containing either the resolved binary content and safe file name, or a failure result when validation or file access fails.
    /// </returns>
    private static async Task<UploadFileResult> GetUploadFileAsync(
        string? fileName,
        string? base64Content,
        string documentsFolder,
        CancellationToken cancellationToken)
    {
        // Inline base64 content takes precedence over filesystem lookup when both inputs are present.
        if (!string.IsNullOrWhiteSpace(base64Content))
        {
            try
            {
                var binaryContent = Convert.FromBase64String(base64Content);
                return new UploadFileResult(null, null, binaryContent);
            }
            catch (FormatException)
            {
                return new UploadFileResult(
                    Failure(CreateDocumentInProcessFailureKind.Validation, "content must be a valid base64 string."),
                    null,
                    null);
            }
        }

        if (string.IsNullOrWhiteSpace(documentsFolder))
        {
            return new UploadFileResult(
                Failure(CreateDocumentInProcessFailureKind.Configuration, "Document storage path is not configured."),
                null,
                null);
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return new UploadFileResult(
                Failure(CreateDocumentInProcessFailureKind.Validation, "fileName is required in the request body."),
                null,
                null);
        }

        var safeFileName = GetSafeFileName(fileName);
        if (safeFileName is null)
        {
            return new UploadFileResult(
                Failure(CreateDocumentInProcessFailureKind.Validation, "filename must not contain directory segments."),
                null,
                null);
        }

        // Normalize both the configured folder and the candidate file path before comparing them.
        var fullDocumentsFolder = Path.GetFullPath(documentsFolder);
        var fullDocumentsFolderWithSeparator = fullDocumentsFolder.EndsWith(Path.DirectorySeparatorChar)
            ? fullDocumentsFolder
            : fullDocumentsFolder + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(fullDocumentsFolder, safeFileName));

        // Require the resolved file to remain under the configured documents folder to block path traversal.
        if (!fullPath.StartsWith(fullDocumentsFolderWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            return new UploadFileResult(
                Failure(CreateDocumentInProcessFailureKind.Validation, "filename resolves outside the configured document folder."),
                null,
                null);
        }

        if (!File.Exists(fullPath))
        {
            return new UploadFileResult(
                Failure(CreateDocumentInProcessFailureKind.NotFound, $"Document not found: {safeFileName}"),
                null,
                null);
        }

        var content = await File.ReadAllBytesAsync(fullPath, cancellationToken);
        return new UploadFileResult(null, safeFileName, content);
    }


    // Helper method to extract the file name from the provided input and ensure it does not contain any path segments. 
    // This is a basic check and should be complemented with additional validation as needed.
    private static string? GetSafeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var safeFileName = Path.GetFileName(fileName);
        return string.Equals(fileName, safeFileName, StringComparison.Ordinal)
            ? safeFileName
            : null;
    }


    // Resolves the absolute upload URL for the given upload location, which may be either an absolute URL or a relative path.
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

        var portugalTimeZone = ResolvePortugalTimeZone();
        var portugalTime = TimeZoneInfo.ConvertTime(
            DateTimeOffset.FromUnixTimeSeconds(unixSeconds),
            portugalTimeZone);

        return portugalTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static TimeZoneInfo ResolvePortugalTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }

        return TimeZoneInfo.Utc;
    }

    private sealed record UploadFileResult(
        CreateDocumentInProcessResult? FailureResult,
        string? SafeFileName,
        byte[]? Content);
}
