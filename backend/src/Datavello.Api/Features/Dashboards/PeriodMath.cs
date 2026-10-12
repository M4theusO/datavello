namespace Datavello.Api.Features.Dashboards;

public static class PeriodMath
{
    /// <summary>
    /// The comparison period immediately before [from, to].
    /// Whole calendar months compare with the same number of whole months before
    /// (Mar 1–Mar 31 → Feb 1–Feb 28); any other range compares with the same number of days.
    /// </summary>
    public static (DateOnly From, DateOnly To) Previous(DateOnly from, DateOnly to)
    {
        var previousTo = from.AddDays(-1);

        if (IsWholeMonths(from, to))
        {
            var months = MonthIndex(to) - MonthIndex(from) + 1;
            return (from.AddMonths(-months), previousTo);
        }

        var days = to.DayNumber - from.DayNumber + 1;
        return (previousTo.AddDays(-(days - 1)), previousTo);
    }

    /// <summary>First day of every month touched by [from, to], in order.</summary>
    public static IEnumerable<DateOnly> MonthsIn(DateOnly from, DateOnly to)
    {
        for (var month = new DateOnly(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
        {
            yield return month;
        }
    }

    public static int MonthIndex(DateOnly date) => date.Year * 12 + date.Month - 1;

    private static bool IsWholeMonths(DateOnly from, DateOnly to) =>
        from.Day == 1 && to.Day == DateTime.DaysInMonth(to.Year, to.Month);
}
