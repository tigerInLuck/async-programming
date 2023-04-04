using TimeZoneConverter;

namespace Async.Programming;

/// <summary>
/// The date time helper.
/// </summary>
public static class TimeHelper
{
    /// <summary>
    /// Convert UTC time to local time(China Standard Time).
    /// </summary>
    public static DateTime UtcToLocalTime(DateTime? utcTime = null)
    {
        timeZone ??= TZConvert.GetTimeZoneInfo("China Standard Time");

        return TimeZoneInfo.ConvertTimeFromUtc(utcTime ?? DateTime.UtcNow, timeZone);
    }

    static TimeZoneInfo timeZone;
}