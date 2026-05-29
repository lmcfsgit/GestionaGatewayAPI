using System.Text.Json.Serialization;

namespace GestionaGateway.Core.Models;

/// <summary>
/// Represents a Gestiona third enriched with selected default-address fields.
/// </summary>
/// <param name="FullName">The third full name.</param>
/// <param name="NifCountry">The NIF country code.</param>
/// <param name="Id">The third identifier.</param>
/// <param name="Nif">The third NIF value.</param>
/// <param name="Type">The third type.</param>
/// <param name="Email">The third email address.</param>
/// <param name="Mobile">The third mobile number.</param>
/// <param name="NifType">The third NIF type.</param>
/// <param name="Address">The default address street name.</param>
/// <param name="Number">The default address number.</param>
/// <param name="ZipCode">The default address zip code.</param>
/// <param name="Province">The default address province.</param>
/// <param name="Country">The default address country.</param>
/// <param name="TypeOfRoad">The default address type of road.</param>
public sealed record Third(
    [property: JsonPropertyName("full_name")] string? FullName,
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("nif_country")] string? NifCountry,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("nif")] string? Nif,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("mobile")] string? Mobile,
    [property: JsonPropertyName("nif_type")] string? NifType,
    [property: JsonPropertyName("address")] string? Address,
    [property: JsonPropertyName("number")] string? Number,
    [property: JsonPropertyName("zip_code")] string? ZipCode,
    [property: JsonPropertyName("province")] string? Province,
    [property: JsonPropertyName("country")] string? Country,
    [property: JsonPropertyName("type_of_road")] string? TypeOfRoad);
