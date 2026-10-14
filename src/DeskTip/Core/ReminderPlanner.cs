using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace DeskTip.Core;

public sealed class ReminderPlanner
{
    private sealed class State
    {
        public ReminderDefinition Rule = null!;
        public DateTime? Next, Snoozed, Deferred;
        public int Rotation;
    }
    private Dictionary<string, State> states = new();
    private readonly HashSet<string> deliveredOnce = new();
    private static string OnceKey(ReminderDefinition r) => r.Id + ":" + r.OnceAt;
    private DateTime? ComputeNext(ReminderDefinition r, DateTime now) => r.Mode == ScheduleMode.Once && deliveredOnce.Contains(OnceKey(r)) ? null : ScheduleEngine.Next(r, Settings.Calendar, now);
    private DateTime lastTick;
    public AppSettings Settings { get; private set; }
    public bool Paused { get; private set; }
    public bool NeedsForegroundCheck(DateTime now) => !Paused && Settings.Enabled && states.Values.Any(s => s.Deferred != null || s.Snoozed is { } a && a <= now || s.Next is { } b && b <= now);
    private static DateTime? Earliest(State state) => new[] { state.Next, state.Snoozed, state.Deferred }.Min();
    public DateTime? NextFor(string id) => states.TryGetValue(id, out var state) ? Earliest(state) : null;
    public DateTime? NextDue => states.Values.Select(Earliest).Min();
    public ReminderPlanner(AppSettings settings, DateTime now) { Settings = settings; Reset(now); }
    public void Reset(DateTime now)
    {
        lastTick = now;
        states = Settings.Reminders.ToDictionary(r => r.Id, r => new State { Rule = r, Next = Settings.Enabled ? ComputeNext(r, now) : null });
    }
    public void Apply(AppSettings settings, DateTime now)
    {
        bool preserve = Settings.BeijingTime == settings.BeijingTime && Settings.Enabled == settings.Enabled;
        bool sameCalendar = Settings.Calendar.Overrides.Count == settings.Calendar.Overrides.Count && Settings.Calendar.Overrides.All(p => settings.Calendar.Overrides.TryGetValue(p.Key, out var value) && value == p.Value);
        var old = states; Settings = settings; lastTick = now;
        var liveOnceKeys = settings.Reminders.Where(r => r.Mode == ScheduleMode.Once).Select(OnceKey).ToHashSet(); deliveredOnce.RemoveWhere(key => !liveOnceKeys.Contains(key));
        states = settings.Reminders.ToDictionary(r => r.Id, r =>
        {
            if (preserve && (sameCalendar || r.Mode == ScheduleMode.Once || r.Days != DayPolicy.ChinaWorkdays) && old.TryGetValue(r.Id, out var state) && JsonSerializer.Serialize(state.Rule) == JsonSerializer.Serialize(r)) { state.Rule = r; return state; }
            return new State { Rule = r, Next = settings.Enabled ? ComputeNext(r, now) : null };
        });
    }
    public void TogglePause(DateTime now) { Paused = !Paused; Reset(now); }
    public bool Snooze(string id, DateTime now)
    {
        if (Paused || !Settings.Enabled || !states.TryGetValue(id, out var state) || !ScheduleEngine.CanSnooze(state.Rule, Settings.Calendar, now)) return false;
        state.Snoozed = now.AddMinutes(state.Rule.SnoozeMinutes); state.Deferred = null; return true;
    }
    public IReadOnlyList<ReminderOccurrence> Advance(DateTime now, bool fullscreen)
    {
        var result = new List<ReminderOccurrence>();
        if (now < lastTick || now - lastTick > TimeSpan.FromMinutes(2)) { Reset(now); return result; }
        lastTick = now; if (Paused || !Settings.Enabled) return result;
        foreach (var state in states.Values)
        {
            var rule = state.Rule; DateTime? due = null;
            if (state.Next is { } next && now >= next)
            {
                if (now - next <= TimeSpan.FromMinutes(1) && ScheduleEngine.CanDeliver(rule, Settings.Calendar, next, now)) due = next;
                state.Next = ComputeNext(rule, now);
            }
            if (state.Snoozed is { } snoozed && now >= snoozed) { if (now - snoozed <= TimeSpan.FromMinutes(1) && ScheduleEngine.CanDeliver(rule, Settings.Calendar, snoozed, now)) due = snoozed; state.Snoozed = null; }
            bool quiet = Settings.FullScreenQuiet && rule.QuietDuringFullscreen && fullscreen;
            if (state.Deferred is { } pending)
            {
                if (now - pending > TimeSpan.FromMinutes(10) || !ScheduleEngine.CanDeliver(rule, Settings.Calendar, pending, now)) state.Deferred = null;
                else if (!quiet) { due = pending; state.Deferred = null; }
            }
            if (due == null) continue;
            if (quiet) { state.Deferred ??= due; continue; }
            state.Deferred = state.Snoozed = null;
            if (rule.Mode == ScheduleMode.Once) deliveredOnce.Add(OnceKey(rule));
            string message = rule.RotationMessages.Count == 0 ? rule.Message : rule.RotationMessages[state.Rotation++ % rule.RotationMessages.Count];
            result.Add(new(rule, due.Value, message));
        }
        return result.OrderBy(r => r.Due).ToArray();
    }
}
