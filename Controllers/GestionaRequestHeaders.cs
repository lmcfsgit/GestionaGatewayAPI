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
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        accessToken = accessToken.Trim();
        return IsUnresolvedVariable(accessToken)
            ? null
            : accessToken;
    }

    private static bool IsUnresolvedVariable(string value)
    {
        return value.StartsWith("{{", StringComparison.Ordinal)
            && value.EndsWith("}}", StringComparison.Ordinal);
    }
}
