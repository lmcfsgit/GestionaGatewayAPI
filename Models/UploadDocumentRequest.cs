using Microsoft.AspNetCore.Mvc;

namespace GestionaGatewayAPI.Models;

public sealed class UploadDocumentRequest
{
    [FromQuery(Name = "filename")]
    public string? FileName { get; init; }

    [FromQuery(Name = "process_id")]
    public string? ProcessId { get; init; }
}
