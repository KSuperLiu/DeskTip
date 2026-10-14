using DeskTip.Core;
using Microsoft.Win32;
using System.Windows.Threading;
using System.Text.Json;
namespace DeskTip.Services;

public sealed class ReminderService : IDisposable
{
    private readonly DispatcherTimer timer = new(DispatcherPriority.Background);
    private readonly TimeZoneInfo china = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
    private readonly ReminderPlanner planner;
    private bool locked;
    private PetWindow? popup, previewWindow;
    public AppSettings Settings => planner.Settings;
    public DateTime Now => TimeFor(Settings);
    private DateTime TimeFor(AppSettings settings) => settings.BeijingTime ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, china) : DateTime.Now;
    public DateTime? DisplayDue => planner.NextDue;
    public DateTime? NextFor(string id) => planner.NextFor(id);
    public bool Paused => planner.Paused;
    public event Action? Changed;
    public void SetTheme(ThemePreference theme) { Settings.Theme = theme; Changed?.Invoke(); }
    public ReminderService(AppSettings settings)
    {
        planner = new(settings, TimeFor(settings)); timer.Interval = TimeSpan.FromSeconds(15); timer.Tick += Tick; timer.Start();
        SystemEvents.SessionSwitch += OnSessionSwitch; SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }
    public void Apply(AppSettings settings)
    {
        bool timeChanged = Settings.BeijingTime != settings.BeijingTime;
        planner.Apply(settings, TimeFor(settings)); previewWindow?.Close();
        if (timeChanged || !settings.Enabled) popup?.Close();
        else popup?.Prune(o => settings.Reminders.Any(r => r.Id == o.Reminder.Id && r.Enabled && JsonSerializer.Serialize(r) == JsonSerializer.Serialize(o.Reminder) && ScheduleEngine.CanDeliver(r, settings.Calendar, o.Due, Now)));
        Changed?.Invoke();
    }
    public void TogglePause() { planner.TogglePause(Now); popup?.Close(); previewWindow?.Close(); Changed?.Invoke(); }
    public bool CanSnooze(ReminderDefinition rule) => Settings.Enabled && !Paused && ScheduleEngine.CanSnooze(rule, Settings.Calendar, Now);
    public void Preview(ReminderDefinition? rule = null)
    {
        rule ??= Settings.Reminders.FirstOrDefault() ?? TemplateCatalog.Create("focus");
        Show(new[] { new ReminderOccurrence(rule, Now, rule.RotationMessages.FirstOrDefault() ?? rule.Message) }, true);
    }
    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.SessionUnlock)
            timer.Dispatcher.BeginInvoke(() => { locked = e.Reason == SessionSwitchReason.SessionLock; planner.Reset(Now); popup?.Close(); previewWindow?.Close(); Changed?.Invoke(); });
    }
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) timer.Dispatcher.BeginInvoke(() => { planner.Reset(Now); popup?.Close(); previewWindow?.Close(); Changed?.Invoke(); });
    }
    private void Tick(object? sender, EventArgs args)
    {
        if (locked) return;
        var now = Now;
        bool fullScreen = Settings.FullScreenQuiet && planner.NeedsForegroundCheck(now) && FullScreenDetector.IsFullScreen();
        var occurrences = planner.Advance(now, fullScreen);
        if (occurrences.Count > 0)
        {
            if (popup != null) popup.Add(occurrences);
            else Show(occurrences, false);
        }
        popup?.Prune(r => ScheduleEngine.CanDeliver(r.Reminder, Settings.Calendar, r.Due, now));
        Changed?.Invoke();
    }
    private void Show(IReadOnlyList<ReminderOccurrence> occurrences, bool preview)
    {
        if (preview)
        {
            previewWindow?.Close(); var sample = new PetWindow(occurrences, true); previewWindow = sample;
            sample.Closed += (_, _) => { if (ReferenceEquals(previewWindow, sample)) previewWindow = null; }; sample.Show(); return;
        }
        popup?.Close(); var window = new PetWindow(occurrences, false); popup = window;
        window.SnoozeRequested += id => { if (!preview) planner.Snooze(id, Now); Changed?.Invoke(); };
        window.Closed += (_, _) => { if (ReferenceEquals(popup, window)) popup = null; }; window.Show();
    }
    public void Dispose() { timer.Stop(); SystemEvents.SessionSwitch -= OnSessionSwitch; SystemEvents.PowerModeChanged -= OnPowerModeChanged; popup?.Close(); previewWindow?.Close(); }
}
