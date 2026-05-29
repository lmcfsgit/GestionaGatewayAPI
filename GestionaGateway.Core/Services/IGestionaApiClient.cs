using GestionaGateway.Core.Models;

namespace GestionaGateway.Core.Services;

/// <summary>
/// Defines low-level HTTP operations against the Gestiona API.
/// </summary>
public interface IGestionaApiClient
{
    /// <summary>
    /// Creates a temporary upload space in Gestiona.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent in request headers.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the upload location when the request succeeds.</returns>
    Task<GestionaApiCallResult<string?>> CreateUploadSpaceAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        CancellationToken cancellationToken);

    /// <summary>
    /// Uploads document content to a previously created Gestiona upload location.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API, used when the upload location is relative.</param>
    /// <param name="uploadLocation">The absolute or relative upload location returned by Gestiona.</param>
    /// <param name="accessToken">The Gestiona access token sent in request headers.</param>
    /// <param name="content">The binary document content to upload.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result indicating whether the upload completed successfully.</returns>
    Task<GestionaApiCallResult> UploadDocumentContentAsync(
        string gestionaApiBaseUrl,
        string uploadLocation,
        string accessToken,
        byte[] content,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the self link for the Gestiona file associated with a process code.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent in request headers.</param>
    /// <param name="processId">The process code used to search for the Gestiona file.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the file self link when found.</returns>
    Task<GestionaApiCallResult<string?>> GetFileSelfHrefAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string processId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a Gestiona file identifier from a process code.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent in request headers.</param>
    /// <param name="processId">The process code used to search for the Gestiona file.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the resolved Gestiona file identifier when found.</returns>
    Task<GestionaApiCallResult<string?>> GetFileIdFromProcessCode(
        string gestionaApiBaseUrl,
        string accessToken,
        string processId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a document in a Gestiona file or folder.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent in request headers.</param>
    /// <param name="fileId">The Gestiona file identifier where the document should be created.</param>
    /// <param name="folderId">The optional Gestiona folder identifier where the document should be created.</param>
    /// <param name="request">The document creation request payload.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the created document response when available.</returns>
    Task<GestionaApiCallResult<CreateDocumentAndFolderResponse?>> CreateDocumentAndFolderAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string fileId,
        string? folderId,
        CreateDocumentInFileRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates an external URL document in a Gestiona file or folder.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent in request headers.</param>
    /// <param name="fileId">The Gestiona file identifier where the document should be created.</param>
    /// <param name="folderId">The optional Gestiona folder identifier where the document should be created.</param>
    /// <param name="request">The external URL document creation request payload.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the created document response when available.</returns>
    Task<GestionaApiCallResult<CreateDocumentAndFolderResponse?>> CreateDocumentUrlAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string fileId,
        string? folderId,
        CreateDocumentInFileRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a folder in a Gestiona file or parent folder.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent in request headers.</param>
    /// <param name="fileId">The Gestiona file identifier where the folder should be created.</param>
    /// <param name="folderId">The optional parent folder identifier where the folder should be created.</param>
    /// <param name="request">The folder creation request payload.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the created folder response when available.</returns>
    Task<GestionaApiCallResult<CreateDocumentAndFolderResponse?>> CreateFolderAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string fileId,
        string? folderId,
        CreateDocumentInFileRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Downloads a document from Gestiona.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent in request headers.</param>
    /// <param name="documentId">The Gestiona document identifier to download.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the downloaded document content and metadata when successful.</returns>
    Task<GestionaApiCallResult<DownloadedDocument?>> DownloadDocumentAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string documentId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the third identifiers associated with a Gestiona process file.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent in request headers.</param>
    /// <param name="processId">The Gestiona file identifier whose third parties should be retrieved.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing third identifiers extracted from upstream third-party links.</returns>
    Task<GestionaApiCallResult<IReadOnlyList<string>>> GetProcessThirdIdsAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string processId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets a third from Gestiona.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent in request headers.</param>
    /// <param name="thirdId">The Gestiona third identifier to retrieve.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the third when found.</returns>
    Task<GestionaApiCallResult<Third?>> GetThirdAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string thirdId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a Gestiona third identifier from a NIF.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent in request headers.</param>
    /// <param name="nif">The NIF used to filter thirds.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the resolved third identifier when found.</returns>
    Task<GestionaApiCallResult<string?>> GetThirdIdByNifAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string nif,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the default address for a Gestiona third.
    /// </summary>
    /// <param name="gestionaApiBaseUrl">The base URL of the Gestiona API.</param>
    /// <param name="accessToken">The Gestiona access token sent in request headers.</param>
    /// <param name="thirdId">The Gestiona third identifier whose default address should be retrieved.</param>
    /// <param name="cancellationToken">The token used to cancel the HTTP request.</param>
    /// <returns>The API call result containing the default address when found.</returns>
    Task<GestionaApiCallResult<ThirdDefaultAddress?>> GetThirdDefaultAddressAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string thirdId,
        CancellationToken cancellationToken);
}
