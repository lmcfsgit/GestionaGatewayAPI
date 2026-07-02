using GestionaGateway.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace GestionaGateway.Core.Services;

internal static class GestionaAccessTokenResolver
{
    public static string? Resolve(
        GestionaOptions gestionaOptions,
        string? accessTokenOverride,
        ILogger logger)
    {
        var accessToken = string.IsNullOrWhiteSpace(accessTokenOverride)
            ? gestionaOptions.AccessToken
            : accessTokenOverride;

        logger.LogDebug("Resolved Gestiona access token: {AccessToken}", MaskAccessToken(accessToken));

        return accessToken;
    }

    private static string MaskAccessToken(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return "<none>";
        }

        return accessToken.Length <= 8
            ? "********"
            : $"{accessToken[..4]}...{accessToken[^4..]}";
    }
}
