using System.Text.Json.Serialization;

namespace GestionaGateway.Core.Models;

public sealed class UploadDocumentRequest
{
    public string? Id { get; init; }

    public string? OperationId { get; init; }

    public string? DocumentSourceType { get; init; }

    public string? Url { get; init; }

    public string? Name { get; init; }

    public string? FileName { get; init; }

    public string? Content { get; init; }

    public string? Category { get; init; }

    public string? ExternalReference { get; init; }
}
