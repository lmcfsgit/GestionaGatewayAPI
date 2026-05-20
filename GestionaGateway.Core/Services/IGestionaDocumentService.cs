using GestionaGateway.Core.Models;

namespace GestionaGateway.Core.Services;

public interface IGestionaDocumentService
{
    Task<DownloadDocumentResult> DownloadDocumentAsync(
        string documentId,
        CancellationToken cancellationToken);
}
