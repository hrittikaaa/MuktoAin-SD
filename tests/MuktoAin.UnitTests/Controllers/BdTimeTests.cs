using MuktoAin.Web.Controllers;

namespace MuktoAin.UnitTests.Controllers;

public class BdTimeTests
{
    [Fact]
    public void FromUtc_ShiftsToDhakaWallClock()
    {
        var utc = new DateTime(2026, 9, 1, 20, 30, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 9, 2, 2, 30, 0), BdTime.FromUtc(utc));
    }

    [Fact]
    public void DayBoundsUtc_CoverTheWholeDhakaCalendarDay()
    {
        Assert.True(BdTime.TryParseDay("2026-09-01", out var day));

        // Dhaka 2026-09-01 00:00 .. 23:59:59.9999999 == UTC 08-31 18:00 .. 09-01 17:59:59.9999999
        Assert.Equal(new DateTime(2026, 8, 31, 18, 0, 0), BdTime.DayStartUtc(day));
        Assert.Equal(new DateTime(2026, 9, 1, 18, 0, 0).AddTicks(-1), BdTime.DayEndUtc(day));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-date")]
    [InlineData("01/09/2026")]
    public void TryParseDay_RejectsAnythingButIsoDate(string? value)
    {
        Assert.False(BdTime.TryParseDay(value, out _));
    }
}
