namespace GestionaGatewayAPI.Controllers;

internal static class GestionaRequestHeaders
{
    public const string AccessToken = "X-User-Access-Token";

    public static string? GetAccessToken(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(AccessToken, out var values))
        {
            return null;
        }

        var accessToken = values.ToString();
        return string.IsNullOrWhiteSpace(accessToken)
            ? null
            : accessToken;
    }
}
