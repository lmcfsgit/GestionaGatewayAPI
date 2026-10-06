using System.Globalization;

namespace GestionaGateway.Core;

public static class DateTimeHelpers
{
    /// <summary>
    /// Converts a Unix timestamp in seconds to Portugal local time using the gateway date-time format.
    /// </summary>
    /// <param name="unixTimestamp">The Unix timestamp in seconds, represented as text.</param>
    /// <param name="format">The output date-time format.</param>
    /// <returns>The formatted Portugal local date-time, or the original value when it is not a valid Unix timestamp.</returns>
    public static string FormatUnixTimestamp(
        string unixTimestamp,
        string format = "yyyy-MM-dd HH:mm:ss")
    {
        if (!long.TryParse(unixTimestamp, out var unixSeconds))
        {
            return unixTimestamp;
        }

        var portugalTimeZone = ResolvePortugalTimeZone();
        var portugalTime = TimeZoneInfo.ConvertTime(
            DateTimeOffset.FromUnixTimeSeconds(unixSeconds),
            portugalTimeZone);

        return portugalTime.ToString(format, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Resolves the Portugal time zone using Linux and Windows time zone identifiers.
    /// </summary>
    /// <returns>The Portugal time zone when available; otherwise, UTC.</returns>
    public static TimeZoneInfo ResolvePortugalTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }

        return TimeZoneInfo.Utc;
    }
}
