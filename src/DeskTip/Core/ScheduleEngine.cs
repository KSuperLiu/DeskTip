using System;
using System.Collections.Generic;
using System.Linq;

namespace DeskTip.Core;

public static class ScheduleEngine
{
    // Pure wall-clock scheduling: each work period owns its own interval grid.
    public static DateTime? Next(AppSettings settings, DateTime after)
    {
        if (!settings.Enabled) return null;
        for (int d = 0; d < 370; d++)
        {
            var day = after.Date.AddDays(d);
            if (!settings.Calendar.IsWorkday(day, settings.ChinaCalendar)) continue;
            foreach (var slot in Slots(settings, day)) if (slot > after) return slot;
        }
        return null;
    }

    public static IEnumerable<DateTime> Slots(AppSettings settings, DateTime day)
    {
        if (!settings.Calendar.IsWorkday(day, settings.ChinaCalendar)) yield break;
        foreach (var p in settings.Periods.OrderBy(p => p.StartTime))
            for (var time = day.Date.Add(p.StartTime).AddMinutes(settings.IntervalMinutes);
                 time < day.Date.Add(p.EndTime); time = time.AddMinutes(settings.IntervalMinutes)) yield return time;
    }

    public static WorkPeriod? ActivePeriod(AppSettings settings, DateTime time)
    {
        if (!settings.Calendar.IsWorkday(time, settings.ChinaCalendar)) return null;
        return settings.Periods.FirstOrDefault(p => time.TimeOfDay >= p.StartTime && time.TimeOfDay < p.EndTime);
    }

    public static bool SamePeriod(AppSettings settings, DateTime a, DateTime b)
    {
        if (a.Date != b.Date) return false;
        var pa = ActivePeriod(settings, a);
        return pa != null && ReferenceEquals(pa, ActivePeriod(settings, b));
    }
}
