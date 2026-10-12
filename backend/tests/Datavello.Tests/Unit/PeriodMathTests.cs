using Datavello.Api.Features.Dashboards;

namespace Datavello.Tests.Unit;

public sealed class PeriodMathTests
{
    [Theory]
    // Whole months compare with the same number of whole months before
    [InlineData("2026-03-01", "2026-03-31", "2026-02-01", "2026-02-28")]
    [InlineData("2026-01-01", "2026-06-30", "2025-07-01", "2025-12-31")]
    [InlineData("2024-03-01", "2024-03-31", "2024-02-01", "2024-02-29")]
    [InlineData("2026-01-01", "2026-12-31", "2025-01-01", "2025-12-31")]
    // Other ranges compare with the same number of days
    [InlineData("2026-03-10", "2026-04-08", "2026-02-08", "2026-03-09")]
    [InlineData("2026-03-15", "2026-03-15", "2026-03-14", "2026-03-14")]
    public void Previous_period(string from, string to, string expectedFrom, string expectedTo)
    {
        var (prevFrom, prevTo) = PeriodMath.Previous(DateOnly.Parse(from), DateOnly.Parse(to));

        Assert.Equal(DateOnly.Parse(expectedFrom), prevFrom);
        Assert.Equal(DateOnly.Parse(expectedTo), prevTo);
    }

    [Fact]
    public void Months_in_range_include_partial_months()
    {
        var months = PeriodMath.MonthsIn(new DateOnly(2025, 11, 15), new DateOnly(2026, 2, 3)).ToList();

        Assert.Equal(
            [new DateOnly(2025, 11, 1), new DateOnly(2025, 12, 1), new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1)],
            months);
    }
}
