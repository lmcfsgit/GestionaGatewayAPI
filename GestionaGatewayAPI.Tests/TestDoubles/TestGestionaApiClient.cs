using GestionaGateway.Core.Models;
using GestionaGateway.Core.Services;

namespace GestionaGatewayAPI.Tests.TestDoubles;

internal sealed class TestGestionaApiClient : IGestionaApiClient
{
    public Func<string, string, CancellationToken, Task<GestionaApiCallResult<string?>>>? CreateUploadSpaceAsyncHandler { get; init; }
    public Func<string, string, string, byte[], CancellationToken, Task<GestionaApiCallResult>>? UploadDocumentContentAsyncHandler { get; init; }
    public Func<string, string, string, CancellationToken, Task<GestionaApiCallResult<string?>>>? GetFileSelfHrefAsyncHandler { get; init; }
    public Func<string, string, string, CancellationToken, Task<GestionaApiCallResult<string?>>>? GetFileIdFromProcessCodeHandler { get; init; }
    public Func<string, string, string, string?, CreateDocumentInFileRequest, CancellationToken, Task<GestionaApiCallResult<CreateDocumentAndFolderResponse?>>>? CreateDocumentAndFolderAsyncHandler { get; init; }
    public Func<string, string, string, string?, CreateDocumentInFileRequest, CancellationToken, Task<GestionaApiCallResult<CreateDocumentAndFolderResponse?>>>? CreateDocumentUrlAsyncHandler { get; init; }
    public Func<string, string, string, string?, CreateDocumentInFileRequest, CancellationToken, Task<GestionaApiCallResult<CreateDocumentAndFolderResponse?>>>? CreateFolderAsyncHandler { get; init; }
    public Func<string, string, string, CancellationToken, Task<GestionaApiCallResult<DownloadedDocument?>>>? DownloadDocumentAsyncHandler { get; init; }
    public Func<string, string, string, CancellationToken, Task<GestionaApiCallResult<IReadOnlyList<string>>>>? GetProcessThirdIdsAsyncHandler { get; init; }
    public Func<string, string, string, CancellationToken, Task<GestionaApiCallResult<Third?>>>? GetThirdAsyncHandler { get; init; }
    public Func<string, string, string, CancellationToken, Task<GestionaApiCallResult<string?>>>? GetThirdIdByNifAsyncHandler { get; init; }
    public Func<string, string, string, CancellationToken, Task<GestionaApiCallResult<ThirdDefaultAddress?>>>? GetThirdDefaultAddressAsyncHandler { get; init; }

    public Task<GestionaApiCallResult<string?>> CreateUploadSpaceAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        CancellationToken cancellationToken)
    {
        return Invoke<GestionaApiCallResult<string?>>(CreateUploadSpaceAsyncHandler, gestionaApiBaseUrl, accessToken, cancellationToken);
    }

    public Task<GestionaApiCallResult> UploadDocumentContentAsync(
        string gestionaApiBaseUrl,
        string uploadLocation,
        string accessToken,
        byte[] content,
        CancellationToken cancellationToken)
    {
        return Invoke<GestionaApiCallResult>(UploadDocumentContentAsyncHandler, gestionaApiBaseUrl, uploadLocation, accessToken, content, cancellationToken);
    }

    public Task<GestionaApiCallResult<string?>> GetFileSelfHrefAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string processId,
        CancellationToken cancellationToken)
    {
        return Invoke<GestionaApiCallResult<string?>>(GetFileSelfHrefAsyncHandler, gestionaApiBaseUrl, accessToken, processId, cancellationToken);
    }

    public Task<GestionaApiCallResult<string?>> GetFileIdFromProcessCode(
        string gestionaApiBaseUrl,
        string accessToken,
        string processId,
        CancellationToken cancellationToken)
    {
        return Invoke<GestionaApiCallResult<string?>>(GetFileIdFromProcessCodeHandler, gestionaApiBaseUrl, accessToken, processId, cancellationToken);
    }

    public Task<GestionaApiCallResult<CreateDocumentAndFolderResponse?>> CreateDocumentAndFolderAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string fileId,
        string? folderId,
        CreateDocumentInFileRequest request,
        CancellationToken cancellationToken)
    {
        return Invoke<GestionaApiCallResult<CreateDocumentAndFolderResponse?>>(CreateDocumentAndFolderAsyncHandler, gestionaApiBaseUrl, accessToken, fileId, folderId, request, cancellationToken);
    }

    public Task<GestionaApiCallResult<CreateDocumentAndFolderResponse?>> CreateDocumentUrlAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string fileId,
        string? folderId,
        CreateDocumentInFileRequest request,
        CancellationToken cancellationToken)
    {
        return Invoke<GestionaApiCallResult<CreateDocumentAndFolderResponse?>>(CreateDocumentUrlAsyncHandler, gestionaApiBaseUrl, accessToken, fileId, folderId, request, cancellationToken);
    }

    public Task<GestionaApiCallResult<CreateDocumentAndFolderResponse?>> CreateFolderAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string fileId,
        string? folderId,
        CreateDocumentInFileRequest request,
        CancellationToken cancellationToken)
    {
        return Invoke<GestionaApiCallResult<CreateDocumentAndFolderResponse?>>(CreateFolderAsyncHandler, gestionaApiBaseUrl, accessToken, fileId, folderId, request, cancellationToken);
    }

    public Task<GestionaApiCallResult<DownloadedDocument?>> DownloadDocumentAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string documentId,
        CancellationToken cancellationToken)
    {
        return Invoke<GestionaApiCallResult<DownloadedDocument?>>(DownloadDocumentAsyncHandler, gestionaApiBaseUrl, accessToken, documentId, cancellationToken);
    }

    public Task<GestionaApiCallResult<IReadOnlyList<string>>> GetProcessThirdIdsAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string processId,
        CancellationToken cancellationToken)
    {
        return Invoke<GestionaApiCallResult<IReadOnlyList<string>>>(GetProcessThirdIdsAsyncHandler, gestionaApiBaseUrl, accessToken, processId, cancellationToken);
    }

    public Task<GestionaApiCallResult<Third?>> GetThirdAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string thirdId,
        CancellationToken cancellationToken)
    {
        return Invoke<GestionaApiCallResult<Third?>>(GetThirdAsyncHandler, gestionaApiBaseUrl, accessToken, thirdId, cancellationToken);
    }

    public Task<GestionaApiCallResult<string?>> GetThirdIdByNifAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string nif,
        CancellationToken cancellationToken)
    {
        return Invoke<GestionaApiCallResult<string?>>(GetThirdIdByNifAsyncHandler, gestionaApiBaseUrl, accessToken, nif, cancellationToken);
    }

    public Task<GestionaApiCallResult<ThirdDefaultAddress?>> GetThirdDefaultAddressAsync(
        string gestionaApiBaseUrl,
        string accessToken,
        string thirdId,
        CancellationToken cancellationToken)
    {
        return Invoke<GestionaApiCallResult<ThirdDefaultAddress?>>(GetThirdDefaultAddressAsyncHandler, gestionaApiBaseUrl, accessToken, thirdId, cancellationToken);
    }

    private static Task<T> Invoke<T>(Delegate? handler, params object?[] args)
    {
        if (handler is null)
        {
            throw new InvalidOperationException("No test handler was configured for this API client method.");
        }

        return (Task<T>)handler.DynamicInvoke(args)!;
    }
}
