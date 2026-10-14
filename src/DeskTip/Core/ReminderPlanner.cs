using System;

namespace DeskTip.Core;

/// <summary>Deterministic reminder state; no timers, windows or OS dependencies.</summary>
public sealed class ReminderPlanner
{
    public AppSettings Settings { get; private set; }
    public DateTime? NextDue { get; private set; }
    public DateTime? SnoozedUntil { get; private set; }
    public DateTime? DeferredSince { get; private set; }
    public DateTime? DisplayDue => SnoozedUntil ?? DeferredSince ?? NextDue;
    public bool Paused { get; private set; }
    private DateTime lastTick;

    public ReminderPlanner(AppSettings settings, DateTime now) { Settings = settings; Reset(now); }
    public void Apply(AppSettings settings, DateTime now) { Settings = settings; Reset(now); }
    public void TogglePause(DateTime now) { Paused = !Paused; Reset(now); }
    public void Reset(DateTime now)
    {
        lastTick = now; SnoozedUntil = DeferredSince = null; NextDue = ScheduleEngine.Next(Settings, now);
    }
    public bool Snooze(DateTime now)
    {
        var target = now.AddMinutes(5);
        if (Paused || !Settings.Enabled || !ScheduleEngine.SamePeriod(Settings, now, target)) return false;
        SnoozedUntil = target; DeferredSince = null; return true;
    }
    public bool Advance(DateTime now, bool fullScreen)
    {
        if (now < lastTick || now - lastTick > TimeSpan.FromMinutes(2)) { Reset(now); return false; }
        lastTick = now;
        if (Paused || !Settings.Enabled) return false;
        if (ScheduleEngine.ActivePeriod(Settings, now) == null) SnoozedUntil = DeferredSince = null;
        var due = false;
        if (NextDue is { } next && now >= next)
        {
            due = now - next <= TimeSpan.FromMinutes(1) && ScheduleEngine.SamePeriod(Settings, next, now);
            NextDue = ScheduleEngine.Next(Settings, now);
        }
        if (SnoozedUntil is { } snoozed && now >= snoozed) { due = true; SnoozedUntil = null; }
        if (DeferredSince is { } pending)
        {
            if (!ScheduleEngine.SamePeriod(Settings, pending, now) || now - pending > TimeSpan.FromMinutes(10)) DeferredSince = null;
            else if (!fullScreen) { due = true; DeferredSince = null; }
        }
        if (!due || ScheduleEngine.ActivePeriod(Settings, now) == null) return false;
        if (Settings.FullScreenQuiet && fullScreen) { DeferredSince ??= now; return false; }
        SnoozedUntil = DeferredSince = null; return true;
    }
}
