using GestionaGateway.Core.Models;

namespace GestionaGateway.Core.Services;

public interface IGestionaProcessService
{
    Task<CreateDocumentInProcessResult> CreateDocumentInProcessAsync(
        UploadDocumentRequest request,
        string processId,
        string? folderId,
        bool resolveFileIdFromProcessCode,
        string documentsFolder,
        CancellationToken cancellationToken);
}
