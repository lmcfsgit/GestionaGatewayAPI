using GestionaGateway.Core.Models;

namespace GestionaGateway.Core.Services;

public interface IGestionaDocumentService
{
    Task<CreateDocumentInProcessResult> CreateDocumentInProcessAsync(
        UploadDocumentRequest request,
        string processId,
        bool resolveFileIdFromProcessCode,
        string documentsFolder,
        CancellationToken cancellationToken);
}
