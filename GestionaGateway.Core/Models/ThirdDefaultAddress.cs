using System.Text.Json.Serialization;

namespace GestionaGateway.Core.Models;

/// <summary>
/// Represents the selected fields returned by the Gestiona third default-address endpoint.
/// </summary>
/// <param name="Address">The street name.</param>
/// <param name="Number">The address number.</param>
/// <param name="ZipCode">The zip code.</param>
/// <param name="Province">The province.</param>
/// <param name="Country">The country.</param>
/// <param name="TypeOfRoad">The type of road.</param>
/// <param name="Zone">The zone.</param>
/// <param name="Links">The links associated with the address.</param>
public sealed record ThirdDefaultAddress(
    [property: JsonPropertyName("address")] string? Address,
    [property: JsonPropertyName("number")] string? Number,
    [property: JsonPropertyName("zip_code")] string? ZipCode,
    [property: JsonPropertyName("province")] string? Province,
    [property: JsonPropertyName("country")] string? Country,
    [property: JsonPropertyName("type_of_road")] string? TypeOfRoad,
    [property: JsonPropertyName("zone")] string? Zone,
    [property: JsonPropertyName("links")] IReadOnlyList<GestionaLink>? Links);
