namespace Backend.Services;

/// <summary>
/// Opportunity-cost helper. "Business hours" = Mon-Fri 05:00-17:00 local wall-clock.
/// Timezone convention: we treat simulated/log timestamps as wall-clock in a single logical TZ
/// (Manila/Poland coverage window). No TZ conversion is performed.
/// </summary>
public static class BusinessHoursCalculator
{
    public const int BusinessStartHour = 5;  // 05:00
    public const int BusinessEndHour = 17;   // 17:00

    /// <summary>
    /// Total business hours between start and end (Mon-Fri 05:00-17:00 only).
    /// Weekends and nights count as zero.
    /// Returns 0 if end <= start or either is null.
    /// </summary>
    public static double BusinessHoursBetween(DateTime? start, DateTime? end)
    {
        if (start is null || end is null) return 0;
        if (end <= start) return 0;
        var s = start.Value;
        var e = end.Value;
        double totalMinutes = 0;
        var cursor = s.Date;
        while (cursor <= e.Date)
        {
            if (IsBusinessDay(cursor))
            {
                var dayStart = cursor.AddHours(BusinessStartHour);
                var dayEnd = cursor.AddHours(BusinessEndHour);
                var overlapStart = s > dayStart ? s : dayStart;
                var overlapEnd = e < dayEnd ? e : dayEnd;
                if (overlapEnd > overlapStart)
                {
                    totalMinutes += (overlapEnd - overlapStart).TotalMinutes;
                }
            }
            cursor = cursor.AddDays(1);
        }
        return totalMinutes / 60.0;
    }

    /// <summary>
    /// Advance a wall-clock timestamp forward by the given opportunity-cost hours,
    /// only counting Mon-Fri 05:00-17:00 minutes. Used by the simulator to place the next
    /// iteration's start time realistically (the clock "pauses" outside business hours).
    /// </summary>
    public static DateTime AddBusinessHours(DateTime start, double hours)
    {
        if (hours <= 0) return start;
        var remainingMinutes = hours * 60;
        var cursor = start;
        const int maxIters = 90; // safety cap: 90 days of advance is more than enough
        for (int i = 0; i < maxIters && remainingMinutes > 0; i++)
        {
            if (!IsBusinessDay(cursor.Date))
            {
                cursor = NextBusinessDayStart(cursor);
                continue;
            }
            var dayStart = cursor.Date.AddHours(BusinessStartHour);
            var dayEnd = cursor.Date.AddHours(BusinessEndHour);
            if (cursor < dayStart) cursor = dayStart;
            if (cursor >= dayEnd)
            {
                cursor = NextBusinessDayStart(cursor);
                continue;
            }
            var minutesLeftToday = (dayEnd - cursor).TotalMinutes;
            if (remainingMinutes <= minutesLeftToday)
            {
                cursor = cursor.AddMinutes(remainingMinutes);
                remainingMinutes = 0;
            }
            else
            {
                remainingMinutes -= minutesLeftToday;
                cursor = NextBusinessDayStart(cursor);
            }
        }
        return cursor;
    }

    /// <summary>
    /// Advance a wall-clock timestamp forward by the given number of working days,
    /// preserving the time-of-day. Weekends are skipped. Holidays not yet modeled.
    /// Fractional days are treated as a fraction of 24 calendar hours (so 0.5 WD = +12h).
    /// Example: Tuesday 09:00 + 1.5 WD = Wednesday 21:00.
    /// </summary>
    public static DateTime AddWorkingDays(DateTime start, double days)
    {
        if (days <= 0) return start;
        var fullDays = (int)Math.Floor(days);
        var fractional = days - fullDays;
        var cursor = start;
        for (int i = 0; i < fullDays; i++)
        {
            do { cursor = cursor.AddDays(1); } while (!IsBusinessDay(cursor));
        }
        if (fractional > 0)
        {
            cursor = cursor.AddHours(fractional * 24);
            while (!IsBusinessDay(cursor)) cursor = cursor.AddDays(1);
        }
        return cursor;
    }

    public static bool IsBusinessDay(DateTime date)
        => date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday;

    private static DateTime NextBusinessDayStart(DateTime from)
    {
        var d = from.Date.AddDays(1);
        while (!IsBusinessDay(d)) d = d.AddDays(1);
        return d.AddHours(BusinessStartHour);
    }
}
