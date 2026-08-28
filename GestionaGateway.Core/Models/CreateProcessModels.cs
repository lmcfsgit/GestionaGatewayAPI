using System.Text.Json.Serialization;

namespace GestionaGateway.Core.Models;

public sealed record CreateProcessRequest
{
    public string? ActivityId { get; init; }

    public string? ProcedureId { get; init; }

    public string? UserId { get; init; }

    public string? GroupId { get; init; }

    public string? FreeSubject { get; init; }
}

public sealed record CreateProcessResult(
    bool Success,
    CreateProcessFailureKind FailureKind,
    string? ErrorMessage,
    CreatedProcess? Process,
    int? UpstreamStatusCode);

public sealed record CreatedProcess(
    string Id,
    string ProcessNumber);

public enum CreateProcessFailureKind
{
    None,
    Configuration,
    Validation,
    NotFound,
    Upstream
}

public sealed record CreateProcessFileResponse
{
    [JsonPropertyName("entry_date")]
    public string? EntryDate { get; init; }

    [JsonPropertyName("links")]
    public IReadOnlyList<GestionaLink>? Links { get; init; }
}

public sealed record OpenProcessFileRequest
{
    public string EntryDate { get; init; } = null!;

    public string FreeTitle { get; init; } = null!;

    public string? SelectableTitle { get; init; }

    public string UserHref { get; init; } = null!;

    public string GroupHref { get; init; } = null!;
}

public sealed record SelectableTitlesResponse
{
    [JsonPropertyName("required")]
    public bool Required { get; init; }

    [JsonPropertyName("selectable_titles")]
    public IReadOnlyList<string>? SelectableTitles { get; init; }
}

public sealed record OpenProcessFileResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; }
}
