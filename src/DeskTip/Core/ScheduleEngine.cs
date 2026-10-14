using System;
using System.Collections.Generic;
using System.Linq;

namespace DeskTip.Core;

public static class ScheduleEngine
{
    public static bool IsDayAllowed(ReminderDefinition r, CalendarData calendar, DateTime day) => r.Days switch
    {
        DayPolicy.ChinaWorkdays => calendar.IsWorkday(day),
        DayPolicy.Weekdays => day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday,
        DayPolicy.SelectedDays => r.SelectedDays.Contains(day.DayOfWeek),
        _ => true
    };
    public static IEnumerable<DateTime> Slots(ReminderDefinition r, CalendarData calendar, DateTime day)
    {
        if (!r.Enabled) yield break;
        if (r.Mode == ScheduleMode.Once) { if (r.OnceTime.Date == day.Date) yield return r.OnceTime; yield break; }
        if (!IsDayAllowed(r, calendar, day)) yield break;
        if (r.Mode == ScheduleMode.FixedTimes)
        {
            foreach (var time in r.Times.OrderBy(t => t)) yield return day.Date.Add(Clock.Parse(time));
            yield break;
        }
        foreach (var p in r.Periods.OrderBy(p => p.StartTime))
            for (var time = day.Date.Add(p.StartTime).AddMinutes(r.IntervalMinutes); time < day.Date.Add(p.EndTime); time = time.AddMinutes(r.IntervalMinutes)) yield return time;
    }
    public static DateTime? Next(ReminderDefinition r, CalendarData calendar, DateTime after)
    {
        if (!r.Enabled) return null;
        if (r.Mode == ScheduleMode.Once) return r.OnceTime > after ? r.OnceTime : null;
        for (int d = 0; d < 370; d++)
        {
            var day = after.Date.AddDays(d);
            if (!IsDayAllowed(r, calendar, day)) continue;
            if (r.Mode == ScheduleMode.FixedTimes) { foreach (var slot in Slots(r, calendar, day)) if (slot > after) return slot; continue; }
            foreach (var period in r.Periods.OrderBy(p => p.StartTime))
            {
                var start = day.Add(period.StartTime); var end = day.Add(period.EndTime);
                if (end <= after) continue;
                var step = Math.Max(1, Math.Floor((after - start).TotalMinutes / r.IntervalMinutes) + 1);
                var candidate = start.AddMinutes(step * r.IntervalMinutes);
                if (candidate < end) return candidate;
            }
        }
        return null;
    }
    public static WorkPeriod? ActivePeriod(ReminderDefinition r, CalendarData calendar, DateTime time) => IsDayAllowed(r, calendar, time) ? r.Periods.FirstOrDefault(p => time.TimeOfDay >= p.StartTime && time.TimeOfDay < p.EndTime) : null;
    public static bool CanDeliver(ReminderDefinition r, CalendarData calendar, DateTime due, DateTime now)
    {
        if (!r.Enabled) return false;
        if (r.Mode == ScheduleMode.Once) return now >= due;
        if (due.Date != now.Date || !IsDayAllowed(r, calendar, now)) return false;
        if (r.Mode == ScheduleMode.FixedTimes) return true;
        var period = ActivePeriod(r, calendar, due);
        return period != null && ReferenceEquals(period, ActivePeriod(r, calendar, now));
    }
    public static bool CanSnooze(ReminderDefinition r, CalendarData calendar, DateTime now) => CanDeliver(r, calendar, now, now.AddMinutes(r.SnoozeMinutes));
}
