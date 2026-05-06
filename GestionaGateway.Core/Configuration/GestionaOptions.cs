namespace GestionaGateway.Core.Configuration;

public sealed class GestionaOptions
{
    public const string SectionName = "Gestiona";

    public string? GestionaApiBaseUrl { get; init; }
    public string? AccessToken { get; init; }
}
