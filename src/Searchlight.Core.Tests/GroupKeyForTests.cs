using System.Text.RegularExpressions;
using Searchlight.ViewModels;
using Xunit;

namespace Searchlight.Core.Tests;

/// <summary>
/// Verifies the recency-bucket ladder in <see cref="MainViewModel.GroupKeyFor"/>:
/// doubling windows (2/4/8/16/32h, strict upper bounds) then an absolute calendar-day
/// header for anything older. A fixed <c>now</c> makes every boundary deterministic.
/// </summary>
public sealed class GroupKeyForTests
{
    // Arbitrary fixed anchor so the tests never depend on the wall clock.
    private static readonly DateTimeOffset Now =
        new(2026, 7, 2, 12, 0, 0, TimeSpan.FromHours(-7));

    private static string KeyFor(TimeSpan age) => MainViewModel.GroupKeyFor(Now - age, Now);

    [Fact]
    public void JustNow_IsLast2Hours() =>
        Assert.Equal("Last 2 hours", KeyFor(TimeSpan.FromMinutes(1)));

    [Fact]
    public void OneMinuteUnderTwoHours_IsLast2Hours() =>
        Assert.Equal("Last 2 hours", KeyFor(TimeSpan.FromHours(2) - TimeSpan.FromMinutes(1)));

    [Fact]
    public void ExactlyTwoHours_RollsIntoLast4Hours() =>
        // Boundary is strict '<', so age == 2h is NOT in the 2h bucket.
        Assert.Equal("Last 4 hours", KeyFor(TimeSpan.FromHours(2)));

    [Fact]
    public void JustUnderFourHours_IsLast4Hours() =>
        Assert.Equal("Last 4 hours", KeyFor(TimeSpan.FromHours(4) - TimeSpan.FromMinutes(1)));

    [Fact]
    public void ExactlyFourHours_RollsIntoLast8Hours() =>
        Assert.Equal("Last 8 hours", KeyFor(TimeSpan.FromHours(4)));

    [Fact]
    public void JustUnderEightHours_IsLast8Hours() =>
        Assert.Equal("Last 8 hours", KeyFor(TimeSpan.FromHours(8) - TimeSpan.FromMinutes(1)));

    [Fact]
    public void ExactlyEightHours_RollsIntoLast16Hours() =>
        Assert.Equal("Last 16 hours", KeyFor(TimeSpan.FromHours(8)));

    [Fact]
    public void JustUnderSixteenHours_IsLast16Hours() =>
        Assert.Equal("Last 16 hours", KeyFor(TimeSpan.FromHours(16) - TimeSpan.FromMinutes(1)));

    [Fact]
    public void ExactlySixteenHours_RollsIntoLast32Hours() =>
        Assert.Equal("Last 32 hours", KeyFor(TimeSpan.FromHours(16)));

    [Fact]
    public void JustUnderThirtyTwoHours_IsLast32Hours() =>
        Assert.Equal("Last 32 hours", KeyFor(TimeSpan.FromHours(32) - TimeSpan.FromMinutes(1)));

    [Fact]
    public void ExactlyThirtyTwoHours_FallsBackToAbsoluteDate()
    {
        string key = KeyFor(TimeSpan.FromHours(32));

        Assert.DoesNotContain("Last", key);
        // Absolute-date fallback formats as "dddd, MMMM d, yyyy" — assert that shape,
        // locale-independently, rather than a hard-coded (culture-specific) string.
        Assert.Matches(new Regex(@"^\w+, \w+ \d{1,2}, \d{4}$"), key);
    }

    [Fact]
    public void MuchOlder_UsesTheSessionsOwnCalendarDay()
    {
        // A session updated 40h before the anchor should carry the anchor-minus-40h date,
        // not the "now" date — proving the header reflects the session, not the clock.
        DateTimeOffset updated = Now - TimeSpan.FromHours(40);
        string expected = updated.ToLocalTime().ToString("dddd, MMMM d, yyyy");

        Assert.Equal(expected, MainViewModel.GroupKeyFor(updated, Now));
    }
}
