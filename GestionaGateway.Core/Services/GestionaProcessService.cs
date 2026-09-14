using System.Text.Json;
using GestionaGateway.Core;
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
    /// <param name="accessTokenOverride">The optional request-provided Gestiona access token. When absent, the configured token is used.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The result of the document creation workflow.</returns>
    public async Task<CreateDocumentInProcessResult> CreateDocumentInProcessAsync(
        UploadDocumentRequest request,
        string processId,
        string? folderId,
        bool resolveFileIdFromProcessCode,
        string documentsFolder,
        string? accessTokenOverride,
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
        var accessToken = GestionaAccessTokenResolver.Resolve(
            _gestionaOptions,
            accessTokenOverride,
            _logger);

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
            // Trashed = "false",
            // Version = "1" // ?? Is this required for creation? Should it be configurable?
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
                DateTimeHelpers.FormatUnixTimestamp(createdDocument.CreationDate),
                DateTimeHelpers.FormatUnixTimestamp(createdDocument.ModificationDate)),
            null);
    }

    /// <summary>
    /// Resolves the Gestiona file identifier associated with the provided process number.
    /// </summary>
    /// <param name="processNumber">The process number used to find the Gestiona file.</param>
    /// <param name="accessTokenOverride">The optional request-provided Gestiona access token. When absent, the configured token is used.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The process lookup result, including the resolved file id on success.</returns>
    public async Task<GetProcessResult> GetProcessAsync(
        string processNumber,
        string? accessTokenOverride,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "({Method}) started. ProcessNumber={ProcessNumber}",
            nameof(GetProcessAsync),
            processNumber);

        var gestionaApiBaseUrl = _gestionaOptions.GestionaApiBaseUrl;
        var accessToken = GestionaAccessTokenResolver.Resolve(
            _gestionaOptions,
            accessTokenOverride,
            _logger);

        if (string.IsNullOrWhiteSpace(gestionaApiBaseUrl))
        {
            _logger.LogWarning("({Method}) failed at step {Step} for process number {ProcessNumber}", nameof(GetProcessAsync), "ValidateConfiguration:GestionaApiBaseUrl", processNumber);
            return ProcessFailure(GetProcessFailureKind.Configuration, "Gestiona API base URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _logger.LogWarning("({Method}) failed at step {Step} for process number {ProcessNumber}", nameof(GetProcessAsync), "ValidateConfiguration:AccessToken", processNumber);
            return ProcessFailure(GetProcessFailureKind.Configuration, "Gestiona access token is not configured.");
        }

        if (string.IsNullOrWhiteSpace(processNumber))
        {
            _logger.LogWarning("({Method}) failed at step {Step}", nameof(GetProcessAsync), "ValidateProcessNumber");
            return ProcessFailure(GetProcessFailureKind.Validation, "processNumber is required.");
        }

        var fileIdResult = await _gestionaApiClient.GetFileIdFromProcessCode(
            gestionaApiBaseUrl,
            accessToken,
            processNumber,
            cancellationToken);

        if (!fileIdResult.Success || string.IsNullOrWhiteSpace(fileIdResult.Value))
        {
            _logger.LogWarning("({Method}) failed at step {Step} for process number {ProcessNumber}", nameof(GetProcessAsync), "ResolveFileIdFromProcessCode", processNumber);
            var failureKind = fileIdResult.StatusCode == 204
                ? GetProcessFailureKind.NotFound
                : GetProcessFailureKind.Upstream;
            var errorMessage = fileIdResult.StatusCode == 204
                ? $"No Gestiona file was found for process number: {processNumber}."
                : "Failed to resolve Gestiona file ID.";

            return ProcessFailure(failureKind, errorMessage, GetUpstreamErrorStatusCode(fileIdResult.StatusCode));
        }

        _logger.LogInformation(
            "({Method}) succeeded. ProcessNumber={ProcessNumber}, FileId={FileId}",
            nameof(GetProcessAsync),
            processNumber,
            fileIdResult.Value);

        return new GetProcessResult(
            true,
            GetProcessFailureKind.None,
            null,
            fileIdResult.Value,
            processNumber,
            null);
    }

    /// <summary>
    /// Creates a related-file link between two Gestiona process files.
    /// </summary>
    /// <param name="request">The request containing the source file id and the related file id.</param>
    /// <param name="accessTokenOverride">The optional request-provided Gestiona access token. When absent, the configured token is used.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The relation result, including the original request body on success.</returns>
    public async Task<RelatedProcessesResult> RelateProcessesAsync(
        RelatedProcessesRequest request,
        string? accessTokenOverride,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "({Method}) started. Id1={Id1}, Id2={Id2}",
            nameof(RelateProcessesAsync),
            request.Id1,
            request.Id2);

        var gestionaApiBaseUrl = _gestionaOptions.GestionaApiBaseUrl;
        var accessToken = GestionaAccessTokenResolver.Resolve(
            _gestionaOptions,
            accessTokenOverride,
            _logger);

        if (string.IsNullOrWhiteSpace(gestionaApiBaseUrl))
        {
            return RelatedProcessesFailure(
                GetProcessFailureKind.Configuration,
                "Gestiona API base URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return RelatedProcessesFailure(
                GetProcessFailureKind.Configuration,
                "Gestiona access token is not configured.");
        }

        if (string.IsNullOrWhiteSpace(request.Id1))
        {
            return RelatedProcessesFailure(
                GetProcessFailureKind.Validation,
                "id1 is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Id2))
        {
            return RelatedProcessesFailure(
                GetProcessFailureKind.Validation,
                "id2 is required.");
        }

        var relatedFilesRequest = new RelatedFilesRequest(
        [
            new GestionaLink(
                "related-files",
                ResolveResourceHref(gestionaApiBaseUrl, "files", request.Id2!),
                null)
        ]);

        var result = await _gestionaApiClient.RelateFilesAsync(
            gestionaApiBaseUrl,
            accessToken,
            request.Id1!,
            relatedFilesRequest,
            cancellationToken);

        if (!result.Success)
        {
            var failureKind = result.StatusCode == 404
                ? GetProcessFailureKind.NotFound
                : GetProcessFailureKind.Upstream;
            return RelatedProcessesFailure(
                failureKind,
                "Failed to relate Gestiona files.",
                GetUpstreamErrorStatusCode(result.StatusCode));
        }

        return new RelatedProcessesResult(
            true,
            GetProcessFailureKind.None,
            null,
            request,
            null);
    }

    /// <summary>
    /// Deletes a related-file link from a Gestiona process file.
    /// </summary>
    /// <param name="processId">The Gestiona file id that owns the related-file link.</param>
    /// <param name="relatedProcessId">The related Gestiona file id to remove.</param>
    /// <param name="accessTokenOverride">The optional request-provided Gestiona access token. When absent, the configured token is used.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The delete result, including failure details when the upstream deletion fails.</returns>
    public async Task<DeleteRelatedProcessResult> DeleteRelatedProcessAsync(
        string processId,
        string relatedProcessId,
        string? accessTokenOverride,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "({Method}) started. ProcessId={ProcessId}, RelatedProcessId={RelatedProcessId}",
            nameof(DeleteRelatedProcessAsync),
            processId,
            relatedProcessId);

        var gestionaApiBaseUrl = _gestionaOptions.GestionaApiBaseUrl;
        var accessToken = GestionaAccessTokenResolver.Resolve(
            _gestionaOptions,
            accessTokenOverride,
            _logger);

        if (string.IsNullOrWhiteSpace(gestionaApiBaseUrl))
        {
            return DeleteRelatedProcessFailure(
                GetProcessFailureKind.Configuration,
                "Gestiona API base URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return DeleteRelatedProcessFailure(
                GetProcessFailureKind.Configuration,
                "Gestiona access token is not configured.");
        }

        if (string.IsNullOrWhiteSpace(processId))
        {
            return DeleteRelatedProcessFailure(
                GetProcessFailureKind.Validation,
                "processId is required.");
        }

        if (string.IsNullOrWhiteSpace(relatedProcessId))
        {
            return DeleteRelatedProcessFailure(
                GetProcessFailureKind.Validation,
                "relatedProcessId is required.");
        }

        var result = await _gestionaApiClient.DeleteRelatedFileAsync(
            gestionaApiBaseUrl,
            accessToken,
            processId,
            relatedProcessId,
            cancellationToken);

        if (!result.Success)
        {
            var failureKind = result.StatusCode == 404
                ? GetProcessFailureKind.NotFound
                : GetProcessFailureKind.Upstream;
            return DeleteRelatedProcessFailure(
                failureKind,
                "Failed to delete Gestiona related file.",
                GetUpstreamErrorStatusCode(result.StatusCode));
        }

        return new DeleteRelatedProcessResult(
            true,
            GetProcessFailureKind.None,
            null,
            null);
    }

    /// <summary>
    /// Gets the Gestiona process files related to the specified process file.
    /// </summary>
    /// <param name="processId">The Gestiona file id whose related files should be retrieved.</param>
    /// <param name="accessTokenOverride">The optional request-provided Gestiona access token. When absent, the configured token is used.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The related-processes result, including mapped ids and process numbers on success.</returns>
    public async Task<GetRelatedProcessesResult> GetRelatedProcessesAsync(
        string processId,
        string? accessTokenOverride,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "({Method}) started. ProcessId={ProcessId}",
            nameof(GetRelatedProcessesAsync),
            processId);

        var gestionaApiBaseUrl = _gestionaOptions.GestionaApiBaseUrl;
        var accessToken = GestionaAccessTokenResolver.Resolve(
            _gestionaOptions,
            accessTokenOverride,
            _logger);

        if (string.IsNullOrWhiteSpace(gestionaApiBaseUrl))
        {
            return GetRelatedProcessesFailure(
                GetProcessFailureKind.Configuration,
                "Gestiona API base URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return GetRelatedProcessesFailure(
                GetProcessFailureKind.Configuration,
                "Gestiona access token is not configured.");
        }

        if (string.IsNullOrWhiteSpace(processId))
        {
            return GetRelatedProcessesFailure(
                GetProcessFailureKind.Validation,
                "processId is required.");
        }

        var result = await _gestionaApiClient.GetRelatedFilesAsync(
            gestionaApiBaseUrl,
            accessToken,
            processId,
            cancellationToken);

        if (!result.Success)
        {
            var failureKind = result.StatusCode == 404
                ? GetProcessFailureKind.NotFound
                : GetProcessFailureKind.Upstream;
            return GetRelatedProcessesFailure(
                failureKind,
                "Failed to get Gestiona related files.",
                GetUpstreamErrorStatusCode(result.StatusCode));
        }

        var relatedProcesses = result.Value?
            .Select(file => new RelatedProcessItem(file.Id, file.Code))
            .ToArray() ?? [];

        return new GetRelatedProcessesResult(
            true,
            GetProcessFailureKind.None,
            null,
            relatedProcesses,
            null);
    }

    /// <summary>
    /// Gets the third identifiers associated with a Gestiona process file.
    /// </summary>
    /// <param name="processId">The process number or Gestiona file id to inspect.</param>
    /// <param name="resolveFileIdFromProcessCode">Indicates whether <paramref name="processId"/> must first be resolved from a process number.</param>
    /// <param name="accessTokenOverride">The optional request-provided Gestiona access token. When absent, the configured token is used.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The process thirds result, including semicolon-separated third ids on success.</returns>
    public async Task<GetProcessThirdsResult> GetProcessThirdsAsync(
        string processId,
        bool resolveFileIdFromProcessCode,
        string? accessTokenOverride,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "({Method}) started. ProcessId={ProcessId}, ResolveFileIdFromProcessCode={ResolveFileIdFromProcessCode}",
            nameof(GetProcessThirdsAsync),
            processId,
            resolveFileIdFromProcessCode);

        var gestionaApiBaseUrl = _gestionaOptions.GestionaApiBaseUrl;
        var accessToken = GestionaAccessTokenResolver.Resolve(
            _gestionaOptions,
            accessTokenOverride,
            _logger);


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

    /// <summary>
    /// Gets documents and folders from a Gestiona process file or from a nested document/folder.
    /// </summary>
    /// <param name="processId">The Gestiona file id that contains the documents.</param>
    /// <param name="documentId">The optional document or folder id whose children should be retrieved.</param>
    /// <param name="accessTokenOverride">The optional request-provided Gestiona access token. When absent, the configured token is used.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The process documents result, including mapped documents and folders on success.</returns>
    public async Task<GetProcessDocumentsResult> GetProcessDocumentsAsync(
        string processId,
        string? documentId,
        string? accessTokenOverride,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "({Method}) started. ProcessId={ProcessId}, DocumentId={DocumentId}",
            nameof(GetProcessDocumentsAsync),
            processId,
            documentId);

        var gestionaApiBaseUrl = _gestionaOptions.GestionaApiBaseUrl;
        var accessToken = GestionaAccessTokenResolver.Resolve(
            _gestionaOptions,
            accessTokenOverride,
            _logger);

        if (string.IsNullOrWhiteSpace(gestionaApiBaseUrl))
        {
            return DocumentsFailure(
                GetProcessDocumentsFailureKind.Configuration,
                "Gestiona API base URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return DocumentsFailure(
                GetProcessDocumentsFailureKind.Configuration,
                "Gestiona access token is not configured.");
        }

        if (string.IsNullOrWhiteSpace(processId))
        {
            return DocumentsFailure(
                GetProcessDocumentsFailureKind.Validation,
                "processId is required.");
        }

        var documentsResult = await _gestionaApiClient.GetProcessDocumentsAsync(
            gestionaApiBaseUrl,
            accessToken,
            processId,
            documentId,
            cancellationToken);

        if (!documentsResult.Success)
        {
            var failureKind = documentsResult.StatusCode == 404
                ? GetProcessDocumentsFailureKind.NotFound
                : GetProcessDocumentsFailureKind.Upstream;
            return DocumentsFailure(
                failureKind,
                $"Failed to get documents from Gestiona process: {processId}.",
                GetUpstreamErrorStatusCode(documentsResult.StatusCode));
        }

        _logger.LogInformation(
            "({Method}) succeeded. ProcessId={ProcessId}, ItemCount={ItemCount}",
            nameof(GetProcessDocumentsAsync),
            processId,
            documentsResult.Value?.Count ?? 0);

        return new GetProcessDocumentsResult(
            true,
            GetProcessDocumentsFailureKind.None,
            null,
            documentsResult.Value ?? [],
            null);
    }

    /// <summary>
    /// Gets signatures for a Gestiona process document and enriches them with signer user information.
    /// </summary>
    /// <param name="processId">The Gestiona file id that contains the document.</param>
    /// <param name="documentId">The Gestiona document id whose signatures should be retrieved.</param>
    /// <param name="accessTokenOverride">The optional request-provided Gestiona access token. When absent, the configured token is used.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The process document signatures result, including signer username and name on success.</returns>
    public async Task<GetProcessDocumentSignaturesResult> GetProcessDocumentSignaturesAsync(
        string processId,
        string documentId,
        string? accessTokenOverride,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "({Method}) started. ProcessId={ProcessId}, DocumentId={DocumentId}",
            nameof(GetProcessDocumentSignaturesAsync),
            processId,
            documentId);

        var gestionaApiBaseUrl = _gestionaOptions.GestionaApiBaseUrl;
        var accessToken = GestionaAccessTokenResolver.Resolve(
            _gestionaOptions,
            accessTokenOverride,
            _logger);

        if (string.IsNullOrWhiteSpace(gestionaApiBaseUrl))
        {
            return SignaturesFailure(
                GetProcessDocumentsFailureKind.Configuration,
                "Gestiona API base URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return SignaturesFailure(
                GetProcessDocumentsFailureKind.Configuration,
                "Gestiona access token is not configured.");
        }

        if (string.IsNullOrWhiteSpace(processId))
        {
            return SignaturesFailure(
                GetProcessDocumentsFailureKind.Validation,
                "processId is required.");
        }

        if (string.IsNullOrWhiteSpace(documentId))
        {
            return SignaturesFailure(
                GetProcessDocumentsFailureKind.Validation,
                "documentId is required.");
        }

        var signaturesResult = await _gestionaApiClient.GetProcessDocumentSignaturesAsync(
            gestionaApiBaseUrl,
            accessToken,
            processId,
            documentId,
            cancellationToken);

        if (!signaturesResult.Success)
        {
            var failureKind = signaturesResult.StatusCode == 404
                ? GetProcessDocumentsFailureKind.NotFound
                : GetProcessDocumentsFailureKind.Upstream;
            return SignaturesFailure(
                failureKind,
                "Failed to get Gestiona document signatures.",
                GetUpstreamErrorStatusCode(signaturesResult.StatusCode));
        }

        var signatures = new List<ProcessDocumentSignatureResult>();
        foreach (var signature in signaturesResult.Value ?? [])
        {
            ProcessAssigneeUser? signer = null;
            var signerHref = GetSignerUserHref(signature);
            if (!string.IsNullOrWhiteSpace(signerHref))
            {
                var signerResult = await _gestionaApiClient.GetUserByHrefAsync(
                    gestionaApiBaseUrl,
                    accessToken,
                    signerHref,
                    cancellationToken);

                if (!signerResult.Success)
                {
                    var failureKind = signerResult.StatusCode == 404
                        ? GetProcessDocumentsFailureKind.NotFound
                        : GetProcessDocumentsFailureKind.Upstream;
                    return SignaturesFailure(
                        failureKind,
                        "Failed to get Gestiona signature user.",
                        GetUpstreamErrorStatusCode(signerResult.StatusCode));
                }

                signer = signerResult.Value;
            }

            signatures.Add(new ProcessDocumentSignatureResult(
                FormatUnixTimestamp(signature.Date),
                signature.SignatureState,
                signer?.Username,
                signer?.Name));
        }

        return new GetProcessDocumentSignaturesResult(
            true,
            GetProcessDocumentsFailureKind.None,
            null,
            signatures,
            null);
    }

    /// <summary>
    /// Gets the first Gestiona assignee user matching the supplied username.
    /// </summary>
    /// <param name="request">The assignee user filter request.</param>
    /// <param name="accessTokenOverride">The optional request-provided Gestiona access token. When absent, the configured token is used.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The assignee user result, including the first matching user on success.</returns>
    public async Task<GetProcessAssigneeUserResult> GetProcessAssigneeUserAsync(
        GetProcessAssigneeUserRequest request,
        string? accessTokenOverride,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "({Method}) started. Username={Username}",
            nameof(GetProcessAssigneeUserAsync),
            request.Username);

        var gestionaApiBaseUrl = _gestionaOptions.GestionaApiBaseUrl;
        var accessToken = GestionaAccessTokenResolver.Resolve(
            _gestionaOptions,
            accessTokenOverride,
            _logger);

        if (string.IsNullOrWhiteSpace(gestionaApiBaseUrl))
        {
            return AssigneeUserFailure(
                GetProcessFailureKind.Configuration,
                "Gestiona API base URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return AssigneeUserFailure(
                GetProcessFailureKind.Configuration,
                "Gestiona access token is not configured.");
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return AssigneeUserFailure(
                GetProcessFailureKind.Validation,
                "username is required.");
        }

        GestionaApiCallResult<ProcessAssigneeUser?> userResult = await _gestionaApiClient.GetProcessAssigneeUserAsync(
            gestionaApiBaseUrl,
            accessToken,
            request,
            cancellationToken);

        if (!userResult.Success)
        {
            var failureKind = userResult.StatusCode == 404
                ? GetProcessFailureKind.NotFound
                : GetProcessFailureKind.Upstream;
            return AssigneeUserFailure(
                failureKind,
                "Failed to get assignee user from Gestiona.",
                GetUpstreamErrorStatusCode(userResult.StatusCode));
        }

        if (userResult.Value is null)
        {
            return AssigneeUserFailure(
                GetProcessFailureKind.NotFound,
                $"No Gestiona assignee user was found for username: {request.Username}.");
        }

        return new GetProcessAssigneeUserResult(
            true,
            GetProcessFailureKind.None,
            null,
            userResult.Value,
            null);
    }

    /// <summary>
    /// Gets the Gestiona assignee groups available for process assignment.
    /// </summary>
    /// <param name="accessTokenOverride">The optional request-provided Gestiona access token. When absent, the configured token is used.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The assignee groups result, including all mapped groups on success.</returns>
    public async Task<GetProcessAssigneeGroupsResult> GetProcessAssigneeGroupsAsync(
        string? accessTokenOverride,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("({Method}) started.", nameof(GetProcessAssigneeGroupsAsync));

        var gestionaApiBaseUrl = _gestionaOptions.GestionaApiBaseUrl;
        var accessToken = GestionaAccessTokenResolver.Resolve(
            _gestionaOptions,
            accessTokenOverride,
            _logger);

        if (string.IsNullOrWhiteSpace(gestionaApiBaseUrl))
        {
            return AssigneeGroupsFailure(
                GetProcessFailureKind.Configuration,
                "Gestiona API base URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return AssigneeGroupsFailure(
                GetProcessFailureKind.Configuration,
                "Gestiona access token is not configured.");
        }

        var groupsResult = await _gestionaApiClient.GetProcessAssigneeGroupsAsync(
            gestionaApiBaseUrl,
            accessToken,
            cancellationToken);

        if (!groupsResult.Success)
        {
            var failureKind = groupsResult.StatusCode == 404
                ? GetProcessFailureKind.NotFound
                : GetProcessFailureKind.Upstream;
            return AssigneeGroupsFailure(
                failureKind,
                "Failed to get assignee groups from Gestiona.",
                GetUpstreamErrorStatusCode(groupsResult.StatusCode));
        }

        return new GetProcessAssigneeGroupsResult(
            true,
            GetProcessFailureKind.None,
            null,
            groupsResult.Value ?? [],
            null);
    }

    /// <summary>
    /// Creates a failed document-creation result.
    /// </summary>
    /// <param name="failureKind">The document-creation failure classification.</param>
    /// <param name="errorMessage">The human-readable failure message.</param>
    /// <param name="upstreamStatusCode">The optional upstream Gestiona status code.</param>
    /// <returns>A failed document-creation result.</returns>
    private static CreateDocumentInProcessResult Failure(
        CreateDocumentInProcessFailureKind failureKind,
        string errorMessage,
        int? upstreamStatusCode = null)
    {
        return new CreateDocumentInProcessResult(false, failureKind, errorMessage, null, upstreamStatusCode);
    }

    /// <summary>
    /// Creates a failed process-thirds result.
    /// </summary>
    /// <param name="failureKind">The process-thirds failure classification.</param>
    /// <param name="errorMessage">The human-readable failure message.</param>
    /// <param name="upstreamStatusCode">The optional upstream Gestiona status code.</param>
    /// <returns>A failed process-thirds result.</returns>
    private static GetProcessThirdsResult ThirdsFailure(
        GetProcessThirdsFailureKind failureKind,
        string errorMessage,
        int? upstreamStatusCode = null)
    {
        return new GetProcessThirdsResult(false, failureKind, errorMessage, null, null, upstreamStatusCode);
    }

    /// <summary>
    /// Creates a failed process-documents result.
    /// </summary>
    /// <param name="failureKind">The process-documents failure classification.</param>
    /// <param name="errorMessage">The human-readable failure message.</param>
    /// <param name="upstreamStatusCode">The optional upstream Gestiona status code.</param>
    /// <returns>A failed process-documents result.</returns>
    private static GetProcessDocumentsResult DocumentsFailure(
        GetProcessDocumentsFailureKind failureKind,
        string errorMessage,
        int? upstreamStatusCode = null)
    {
        return new GetProcessDocumentsResult(false, failureKind, errorMessage, null, upstreamStatusCode);
    }

    /// <summary>
    /// Creates a failed process document signatures result.
    /// </summary>
    /// <param name="failureKind">The process document signatures failure classification.</param>
    /// <param name="errorMessage">The human-readable failure message.</param>
    /// <param name="upstreamStatusCode">The optional upstream Gestiona status code.</param>
    /// <returns>A failed process document signatures result.</returns>
    private static GetProcessDocumentSignaturesResult SignaturesFailure(
        GetProcessDocumentsFailureKind failureKind,
        string errorMessage,
        int? upstreamStatusCode = null)
    {
        return new GetProcessDocumentSignaturesResult(false, failureKind, errorMessage, null, upstreamStatusCode);
    }

    /// <summary>
    /// Creates a failed process lookup result.
    /// </summary>
    /// <param name="failureKind">The process lookup failure classification.</param>
    /// <param name="errorMessage">The human-readable failure message.</param>
    /// <param name="upstreamStatusCode">The optional upstream Gestiona status code.</param>
    /// <returns>A failed process lookup result.</returns>
    private static GetProcessResult ProcessFailure(
        GetProcessFailureKind failureKind,
        string errorMessage,
        int? upstreamStatusCode = null)
    {
        return new GetProcessResult(false, failureKind, errorMessage, null, null, upstreamStatusCode);
    }

    /// <summary>
    /// Creates a failed assignee-user result.
    /// </summary>
    /// <param name="failureKind">The assignee-user failure classification.</param>
    /// <param name="errorMessage">The human-readable failure message.</param>
    /// <param name="upstreamStatusCode">The optional upstream Gestiona status code.</param>
    /// <returns>A failed assignee-user result.</returns>
    private static GetProcessAssigneeUserResult AssigneeUserFailure(
        GetProcessFailureKind failureKind,
        string errorMessage,
        int? upstreamStatusCode = null)
    {
        return new GetProcessAssigneeUserResult(false, failureKind, errorMessage, null, upstreamStatusCode);
    }

    /// <summary>
    /// Creates a failed assignee-groups result.
    /// </summary>
    /// <param name="failureKind">The assignee-groups failure classification.</param>
    /// <param name="errorMessage">The human-readable failure message.</param>
    /// <param name="upstreamStatusCode">The optional upstream Gestiona status code.</param>
    /// <returns>A failed assignee-groups result.</returns>
    private static GetProcessAssigneeGroupsResult AssigneeGroupsFailure(
        GetProcessFailureKind failureKind,
        string errorMessage,
        int? upstreamStatusCode = null)
    {
        return new GetProcessAssigneeGroupsResult(false, failureKind, errorMessage, null, upstreamStatusCode);
    }

    /// <summary>
    /// Creates a failed related-processes result.
    /// </summary>
    /// <param name="failureKind">The related-processes failure classification.</param>
    /// <param name="errorMessage">The human-readable failure message.</param>
    /// <param name="upstreamStatusCode">The optional upstream Gestiona status code.</param>
    /// <returns>A failed related-processes result.</returns>
    private static RelatedProcessesResult RelatedProcessesFailure(
        GetProcessFailureKind failureKind,
        string errorMessage,
        int? upstreamStatusCode = null)
    {
        return new RelatedProcessesResult(false, failureKind, errorMessage, null, upstreamStatusCode);
    }

    /// <summary>
    /// Creates a failed related-process deletion result.
    /// </summary>
    /// <param name="failureKind">The related-process deletion failure classification.</param>
    /// <param name="errorMessage">The human-readable failure message.</param>
    /// <param name="upstreamStatusCode">The optional upstream Gestiona status code.</param>
    /// <returns>A failed related-process deletion result.</returns>
    private static DeleteRelatedProcessResult DeleteRelatedProcessFailure(
        GetProcessFailureKind failureKind,
        string errorMessage,
        int? upstreamStatusCode = null)
    {
        return new DeleteRelatedProcessResult(false, failureKind, errorMessage, upstreamStatusCode);
    }

    /// <summary>
    /// Creates a failed get-related-processes result.
    /// </summary>
    /// <param name="failureKind">The get-related-processes failure classification.</param>
    /// <param name="errorMessage">The human-readable failure message.</param>
    /// <param name="upstreamStatusCode">The optional upstream Gestiona status code.</param>
    /// <returns>A failed get-related-processes result.</returns>
    private static GetRelatedProcessesResult GetRelatedProcessesFailure(
        GetProcessFailureKind failureKind,
        string errorMessage,
        int? upstreamStatusCode = null)
    {
        return new GetRelatedProcessesResult(false, failureKind, errorMessage, null, upstreamStatusCode);
    }

    /// <summary>
    /// Creates a failed process-creation result.
    /// </summary>
    /// <param name="failureKind">The process-creation failure classification.</param>
    /// <param name="errorMessage">The human-readable failure message.</param>
    /// <param name="upstreamStatusCode">The optional upstream Gestiona status code.</param>
    /// <returns>A failed process-creation result.</returns>
    private static CreateProcessResult CreateProcessFailure(
        CreateProcessFailureKind failureKind,
        string errorMessage,
        int? upstreamStatusCode = null)
    {
        return new CreateProcessResult(false, failureKind, errorMessage, null, upstreamStatusCode);
    }

    /// <summary>
    /// Validates the required fields for process creation.
    /// </summary>
    /// <param name="request">The process creation request to validate.</param>
    /// <returns>A validation error message, or null when the request is valid.</returns>
    private static string? ValidateCreateProcessRequest(CreateProcessRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ActivityId))
        {
            return "activityId is required.";
        }

        if (string.IsNullOrWhiteSpace(request.ProcedureId))
        {
            return "procedureId is required.";
        }

        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return "userId is required.";
        }

        if (string.IsNullOrWhiteSpace(request.GroupId))
        {
            return "groupId is required.";
        }

        if (string.IsNullOrWhiteSpace(request.FreeSubject))
        {
            return "freeSubject is required.";
        }

        return null;
    }

    /// <summary>
    /// Builds an absolute Gestiona resource href from a route name and identifier.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The configured Gestiona API base URL.</param>
    /// <param name="route">The resource route, such as files, users, or groups.</param>
    /// <param name="id">The resource identifier to append to the route.</param>
    /// <returns>The absolute Gestiona resource href.</returns>
    private static string ResolveResourceHref(
        string gestionaApiBaseUrl,
        string route,
        string id)
    {
        var normalizedBaseUrl = gestionaApiBaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? gestionaApiBaseUrl
            : $"{gestionaApiBaseUrl}/";

        return new Uri(
            new Uri(normalizedBaseUrl, UriKind.Absolute),
            $"{route}/{Uri.EscapeDataString(id)}").ToString();
    }

    /// <summary>
    /// Extracts the Gestiona process file id from a file-open href.
    /// </summary>
    /// <param name="fileOpenHref">The absolute or relative file-open href returned by Gestiona.</param>
    /// <returns>The extracted process file id, or null when it cannot be resolved.</returns>
    private static string? ResolveProcessIdFromFileOpenHref(string fileOpenHref)
    {
        var path = Uri.TryCreate(fileOpenHref, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri.AbsolutePath
            : fileOpenHref;

        var segments = path.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var openSegmentIndex = Array.FindLastIndex(
            segments,
            segment => string.Equals(segment, "open", StringComparison.OrdinalIgnoreCase));

        if (openSegmentIndex > 0)
        {
            return Uri.UnescapeDataString(segments[openSegmentIndex - 1]);
        }

        var filesSegmentIndex = Array.FindLastIndex(
            segments,
            segment => string.Equals(segment, "files", StringComparison.OrdinalIgnoreCase));

        return filesSegmentIndex >= 0 && filesSegmentIndex + 1 < segments.Length
            ? Uri.UnescapeDataString(segments[filesSegmentIndex + 1])
            : null;
    }

    /// <summary>
    /// Returns the upstream status code only when it represents an error.
    /// </summary>
    /// <param name="statusCode">The status code returned by Gestiona.</param>
    /// <returns>The upstream error status code, or null for non-error status codes.</returns>
    private static int? GetUpstreamErrorStatusCode(int statusCode)
    {
        return statusCode >= 400
            ? statusCode
            : null;
    }

    /// <summary>
    /// Gets the signer user href from a signature item.
    /// </summary>
    /// <param name="signature">The upstream signature item.</param>
    /// <returns>The signer user href, or null when the signature does not contain one.</returns>
    private static string? GetSignerUserHref(ProcessDocumentSignature signature)
    {
        return signature.Links?
            .FirstOrDefault(link =>
                string.Equals(link.Rel, "signed-user", StringComparison.Ordinal) ||
                string.Equals(link.Rel, "signer-user", StringComparison.Ordinal))
            ?.Href;
    }

    /// <summary>
    /// Formats a Unix timestamp using the shared gateway date-time helper.
    /// </summary>
    /// <param name="unixTimestamp">The Unix timestamp value returned by Gestiona.</param>
    /// <returns>The formatted local date-time string, or the original value when it is blank.</returns>
    private static string? FormatUnixTimestamp(string? unixTimestamp)
    {
        return string.IsNullOrWhiteSpace(unixTimestamp)
            ? unixTimestamp
            : DateTimeHelpers.FormatUnixTimestamp(unixTimestamp);
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

    /// <summary>
    /// Carries the resolved upload content or the validation failure produced while resolving it.
    /// </summary>
    /// <param name="FailureResult">The failure result when upload content could not be resolved.</param>
    /// <param name="SafeFileName">The validated file name when content came from local storage.</param>
    /// <param name="Content">The binary content to upload.</param>
    private sealed record UploadFileResult(
        CreateDocumentInProcessResult? FailureResult,
        string? SafeFileName,
            byte[]? Content);

    /// <summary>
    /// Creates and opens a Gestiona process file from the supplied catalog procedure, external procedure, user, group, and subject.
    /// </summary>
    /// <param name="request">The process creation request.</param>
    /// <param name="accessTokenOverride">The optional request-provided Gestiona access token. When absent, the configured token is used.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The process creation result, including the opened process id and number on success.</returns>
    public async Task<CreateProcessResult> CreateProcessAsync(
        CreateProcessRequest request,
        string? accessTokenOverride,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "({Method}) started. ActivityId={ActivityId}, ProcedureId={ProcedureId}, UserId={UserId}, GroupId={GroupId}",
            nameof(CreateProcessAsync),
            request.ActivityId,
            request.ProcedureId,
            request.UserId,
            request.GroupId);

        var gestionaApiBaseUrl = _gestionaOptions.GestionaApiBaseUrl;
        var accessToken = GestionaAccessTokenResolver.Resolve(
            _gestionaOptions,
            accessTokenOverride,
            _logger);

        if (string.IsNullOrWhiteSpace(gestionaApiBaseUrl))
        {
            return CreateProcessFailure(
                CreateProcessFailureKind.Configuration,
                "Gestiona API base URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return CreateProcessFailure(
                CreateProcessFailureKind.Configuration,
                "Gestiona access token is not configured.");
        }

        var validationMessage = ValidateCreateProcessRequest(request);
        if (validationMessage is not null)
        {
            return CreateProcessFailure(CreateProcessFailureKind.Validation, validationMessage);
        }

        var createFileResult = await _gestionaApiClient.CreateProcessFileAsync(
            gestionaApiBaseUrl,
            accessToken,
            request.ActivityId!,
            request.ProcedureId!,
            cancellationToken);

        if (!createFileResult.Success || createFileResult.Value is null)
        {
            return CreateProcessFailure(
                CreateProcessFailureKind.Upstream,
                "Failed to create Gestiona process file.",
                GetUpstreamErrorStatusCode(createFileResult.StatusCode));
        }

        var entryDate = createFileResult.Value.EntryDate;
        if (string.IsNullOrWhiteSpace(entryDate))
        {
            return CreateProcessFailure(
                CreateProcessFailureKind.Upstream,
                "Gestiona create-file response did not include entry_date.",
                GetUpstreamErrorStatusCode(createFileResult.StatusCode));
        }

        var fileOpenHref = createFileResult.Value.Links?
            .FirstOrDefault(link => string.Equals(link.Rel, "file-open", StringComparison.Ordinal))?
            .Href;

        if (string.IsNullOrWhiteSpace(fileOpenHref))
        {
            return CreateProcessFailure(
                CreateProcessFailureKind.Upstream,
                "Gestiona create-file response did not include a file-open link.",
                GetUpstreamErrorStatusCode(createFileResult.StatusCode));
        }

        var createdProcessId = ResolveProcessIdFromFileOpenHref(fileOpenHref);
        if (string.IsNullOrWhiteSpace(createdProcessId))
        {
            return CreateProcessFailure(
                CreateProcessFailureKind.Upstream,
                "Gestiona create-file response file-open link did not include a process id.",
                GetUpstreamErrorStatusCode(createFileResult.StatusCode));
        }

        var selectableTitlesResult = await _gestionaApiClient.GetSelectableTitlesAsync(
            gestionaApiBaseUrl,
            accessToken,
            createdProcessId,
            cancellationToken);

        if (!selectableTitlesResult.Success)
        {
            return CreateProcessFailure(
                CreateProcessFailureKind.Upstream,
                "Failed to get Gestiona selectable titles.",
                GetUpstreamErrorStatusCode(selectableTitlesResult.StatusCode));
        }

        var selectableTitle = selectableTitlesResult.Value?.SelectableTitles?
            .FirstOrDefault(title => !string.IsNullOrWhiteSpace(title));

        var openFileRequest = new OpenProcessFileRequest
        {
            EntryDate = entryDate,
            FreeTitle = request.FreeSubject!,
            SelectableTitle = selectableTitle,
            UserHref = ResolveResourceHref(gestionaApiBaseUrl, "users", request.UserId!),
            GroupHref = ResolveResourceHref(gestionaApiBaseUrl, "groups", request.GroupId!)
        };

        var openFileResult = await _gestionaApiClient.OpenProcessFileAsync(
            gestionaApiBaseUrl,
            accessToken,
            fileOpenHref,
            openFileRequest,
            cancellationToken);

        if (!openFileResult.Success || openFileResult.Value is null)
        {
            return CreateProcessFailure(
                CreateProcessFailureKind.Upstream,
                "Failed to open Gestiona process file.",
                GetUpstreamErrorStatusCode(openFileResult.StatusCode));
        }

        if (string.IsNullOrWhiteSpace(openFileResult.Value.Id) ||
            string.IsNullOrWhiteSpace(openFileResult.Value.Code))
        {
            return CreateProcessFailure(
                CreateProcessFailureKind.Upstream,
                "Gestiona file-open response did not include id and code.",
                GetUpstreamErrorStatusCode(openFileResult.StatusCode));
        }

        return new CreateProcessResult(
            true,
            CreateProcessFailureKind.None,
            null,
            new CreatedProcess(openFileResult.Value.Id, openFileResult.Value.Code),
            null);
    }
}
