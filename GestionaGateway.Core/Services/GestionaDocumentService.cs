using System.Globalization;
using System.Text.Json;
using GestionaGateway.Core.Configuration;
using GestionaGateway.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestionaGateway.Core.Services;

public sealed class GestionaDocumentService : IGestionaDocumentService
{
    private readonly GestionaOptions _gestionaOptions;
    private readonly IGestionaApiClient _gestionaApiClient;
    private readonly ILogger<GestionaDocumentService> _logger;

    public GestionaDocumentService(
        IOptions<GestionaOptions> gestionaOptions,
        IGestionaApiClient gestionaApiClient,
        ILogger<GestionaDocumentService> logger)
    {
        _gestionaOptions = gestionaOptions.Value;
        _gestionaApiClient = gestionaApiClient;
        _logger = logger;
    }

    public async Task<CreateDocumentInProcessResult> CreateDocumentInProcessAsync(
        UploadDocumentRequest request,
        string processId,
        bool resolveFileIdFromProcessCode,
        string documentsFolder,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "CreateDocumentInProcess started. ProcessId={ProcessId}, ResolveFileIdFromProcessCode={ResolveFileIdFromProcessCode}, DocumentSourceType={DocumentSourceType}, FileName={FileName}, HasContent={HasContent}, HasExternalUrl={HasExternalUrl}",
            processId,
            resolveFileIdFromProcessCode,
            request.DocumentSourceType,
            request.FileName,
            !string.IsNullOrWhiteSpace(request.Content),
            !string.IsNullOrWhiteSpace(request.Url));

        // Read configuration values
        var gestionaApiBaseUrl = _gestionaOptions.GestionaApiBaseUrl;
        var accessToken = _gestionaOptions.AccessToken;

        if (string.IsNullOrWhiteSpace(gestionaApiBaseUrl))
        {
            _logger.LogWarning("CreateDocumentInProcess failed at step {Step} for process {ProcessId}", "ValidateConfiguration:GestionaApiBaseUrl", processId);
            return Failure(CreateDocumentInProcessFailureKind.Configuration, "Gestiona API base URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _logger.LogWarning("CreateDocumentInProcess failed at step {Step} for process {ProcessId}", "ValidateConfiguration:AccessToken", processId);
            return Failure(CreateDocumentInProcessFailureKind.Configuration, "Gestiona access token is not configured.");
        }

        // Resolve file ID from process number if needed or use process ID as file ID
        string? fileId = processId;
        if (resolveFileIdFromProcessCode)
        {
            _logger.LogDebug("Resolving Gestiona file id for process {ProcessId}", processId);

            fileId = await _gestionaApiClient.GetFileIdFromProcessCode(
                gestionaApiBaseUrl,
                accessToken,
                processId,
                cancellationToken);

            if (fileId is null)
            {
                _logger.LogWarning("CreateDocumentInProcess failed at step {Step} for process {ProcessId}", "ResolveFileIdFromProcessCode", processId);
                return Failure(CreateDocumentInProcessFailureKind.Upstream, "Failed to resolve Gestiona file ID.");
            }

            _logger.LogInformation("Resolved Gestiona file id {FileId} for process {ProcessId}", fileId, processId);
        }

        var documentName = string.IsNullOrWhiteSpace(request.Name)
            ? "document"
            : request.Name;

        var createDocumentRequest = new CreateDocumentInFileRequest
        {
            Name = documentName,
            MetadataLanguage = "ES",
            Trashed = "false",
            Version = "1"
        };

        CreateDocumentAndFolderResponse? createdDocument;
        if (string.Equals(request.DocumentSourceType, "DIGITAL", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Using DIGITAL document flow for process {ProcessId}", processId);

            // Get document content either from base64 string or from file system
            // If both are provided, base64 content takes precedence over file system content
            var uploadFile = await GetUploadFileAsync(
                request.FileName,
                request.Content,
                documentsFolder,
                cancellationToken);
            if (uploadFile.FailureResult is not null)
            {
                _logger.LogWarning("CreateDocumentInProcess failed at step {Step} for process {ProcessId}: {ErrorMessage}", "GetUploadFile", processId, uploadFile.FailureResult.ErrorMessage);
                return uploadFile.FailureResult;
            }

            var content = uploadFile.Content!;

            _logger.LogDebug(
                "Uploading document content. Source={Source}, FileName={FileName}, ContentLength={ContentLength}",
                string.IsNullOrWhiteSpace(request.Content) ? "file" : "base64",
                request.FileName,
                content.Length);

            var uploadLocation = await _gestionaApiClient.CreateUploadSpaceAsync(
                gestionaApiBaseUrl,
                accessToken,
                cancellationToken);
            if (uploadLocation is null)
            {
                _logger.LogWarning("CreateDocumentInProcess failed at step {Step} for process {ProcessId}", "CreateUploadSpace", processId);
                return Failure(CreateDocumentInProcessFailureKind.Upstream, "Failed to create upload space in Gestiona.");
            }

            var uploadSucceeded = await _gestionaApiClient.UploadDocumentContentAsync(
                gestionaApiBaseUrl,
                uploadLocation,
                accessToken,
                content,
                cancellationToken);
            if (!uploadSucceeded)
            {
                _logger.LogWarning("CreateDocumentInProcess failed at step {Step} for process {ProcessId}", "UploadDocumentContent", processId);
                return Failure(CreateDocumentInProcessFailureKind.Upstream, "Failed to upload document content to Gestiona.");
            }

            createDocumentRequest = createDocumentRequest with
            {
                Type = "DIGITAL",
                ContentHref = ResolveUploadHref(gestionaApiBaseUrl, uploadLocation)
            };

            _logger.LogDebug(
                "CreateDocumentRequest for DIGITAL:{NewLine}{CreateDocumentRequest}",
                Environment.NewLine,
                JsonSerializer.Serialize(createDocumentRequest, new JsonSerializerOptions { WriteIndented = true }));

            createdDocument = await _gestionaApiClient.CreateDocumentAndFolderAsync(
                gestionaApiBaseUrl,
                accessToken,
                fileId,
                createDocumentRequest,
                cancellationToken);
        }
        else
        {
            _logger.LogDebug("Using EXTERNAL_URL document flow for process {ProcessId}", processId);

            createDocumentRequest = createDocumentRequest with
            {
                Type = "EXTERNAL_URL",
                ExternalUrl = request.Url
            };

            _logger.LogDebug(
                "CreateDocumentRequest for EXTERNAL_URL:{NewLine}{CreateDocumentRequest}",
                Environment.NewLine,
                JsonSerializer.Serialize(createDocumentRequest, new JsonSerializerOptions { WriteIndented = true }));

            createdDocument = await _gestionaApiClient.CreateDocumentUrlAsync(
                gestionaApiBaseUrl,
                accessToken,
                fileId,
                createDocumentRequest,
                cancellationToken);
        }

        if (createdDocument is null)
        {
            _logger.LogWarning("CreateDocumentInProcess failed at step {Step} for process {ProcessId}", "CreateDocumentInGestiona", processId);
            return Failure(CreateDocumentInProcessFailureKind.Upstream, "Failed to create document in Gestiona file.");
        }

        _logger.LogInformation(
            "CreateDocumentInProcess succeeded. ProcessId={ProcessId}, FileId={FileId}, DocumentId={DocumentId}, SourceType={DocumentSourceType}",
            processId,
            fileId,
            createdDocument.Id,
            request.DocumentSourceType);

        _logger.LogDebug(
            "Created Gestiona document payload:{NewLine}{CreatedDocument}",
            Environment.NewLine,
            JsonSerializer.Serialize(createdDocument, new JsonSerializerOptions { WriteIndented = true }));

        return new CreateDocumentInProcessResult(
            true,
            CreateDocumentInProcessFailureKind.None,
            null,
            new CreateDocumentInProcessDocument(
                createdDocument.Id,
                fileId,
                FormatUnixTimestamp(createdDocument.CreationDate),
                FormatUnixTimestamp(createdDocument.ModificationDate)));
    }

    private static CreateDocumentInProcessResult Failure(
        CreateDocumentInProcessFailureKind failureKind,
        string errorMessage)
    {
        return new CreateDocumentInProcessResult(false, failureKind, errorMessage, null);
    }

    private static async Task<UploadFileResult> GetUploadFileAsync(
        string? fileName,
        string? base64Content,
        string documentsFolder,
        CancellationToken cancellationToken)
    {
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

        var fullDocumentsFolder = Path.GetFullPath(documentsFolder);
        var fullDocumentsFolderWithSeparator = fullDocumentsFolder.EndsWith(Path.DirectorySeparatorChar)
            ? fullDocumentsFolder
            : fullDocumentsFolder + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(fullDocumentsFolder, safeFileName!));

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

    private sealed record UploadFileResult(
        CreateDocumentInProcessResult? FailureResult,
        string? SafeFileName,
        byte[]? Content);
}
