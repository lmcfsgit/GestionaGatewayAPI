using System.Text.Json.Serialization;

namespace GestionaGatewayAPI.Models;

public sealed record CreateDocumentAndFolderResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("creation_date")] string CreationDate,
    [property: JsonPropertyName("modification_date")] string ModificationDate);
