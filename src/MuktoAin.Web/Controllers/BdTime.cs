using System.Globalization;

namespace MuktoAin.Web.Controllers;

/// <summary>
/// Bangladesh time for display and date-range filters. Timestamps are stored
/// in UTC; users read and pick dates on the Dhaka calendar. Bangladesh has no
/// daylight saving, so a fixed UTC+6 offset is exact and needs no time-zone
/// database (identical on Windows and Linux hosts). Presentation mapping only.
/// </summary>
public static class BdTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(6);

    // UTC instant -> Dhaka wall-clock time.
    public static DateTime FromUtc(DateTime utc) => DateTime.SpecifyKind(utc + Offset, DateTimeKind.Unspecified);

    // Accepts only the yyyy-MM-dd an <input type="date"> posts, independent of server culture.
    public static bool TryParseDay(string? value, out DateOnly day) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

    // First and last UTC instants of a Dhaka calendar day (inclusive bounds).
    public static DateTime DayStartUtc(DateOnly day) => day.ToDateTime(TimeOnly.MinValue) - Offset;

    public static DateTime DayEndUtc(DateOnly day) => DayStartUtc(day).AddDays(1).AddTicks(-1);
}
