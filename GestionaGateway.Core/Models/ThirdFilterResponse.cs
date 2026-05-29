using System.Text.Json.Serialization;

namespace GestionaGateway.Core.Models;

/// <summary>
/// Represents the subset of the Gestiona third filter response needed to resolve a third identifier.
/// </summary>
/// <param name="Content">The filtered third items returned by Gestiona.</param>
public sealed record ThirdFilterResponse(
    [property: JsonPropertyName("content")] IReadOnlyList<Third>? Content);
