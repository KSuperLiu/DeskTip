using DeskTip.Core;
using Microsoft.Win32;
using System.Windows.Threading;
namespace DeskTip.Services;
public sealed class ReminderService : IDisposable
{
    private readonly DispatcherTimer timer = new(DispatcherPriority.Background);
    private readonly TimeZoneInfo china = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
    private readonly ReminderPlanner planner;
    private bool locked;
    private bool popupIsPreview;
    private int ruleIndex;
    private PetWindow? popup;
    public AppSettings Settings => planner.Settings;
    public DateTime Now => TimeFor(Settings);
    private DateTime TimeFor(AppSettings settings) => settings.BeijingTime ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, china) : DateTime.Now;
    public DateTime? DisplayDue => planner.DisplayDue;
    public bool Paused => planner.Paused;
    public event Action? Changed;
    public ReminderService(AppSettings settings)
    {
        planner = new(settings, TimeFor(settings));
        timer.Interval = TimeSpan.FromSeconds(15); timer.Tick += Tick; timer.Start();
        SystemEvents.SessionSwitch += OnSessionSwitch; SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }
    public void Apply(AppSettings settings) { planner.Apply(settings, TimeFor(settings)); popup?.Close(); Changed?.Invoke(); }
    public void TogglePause() { planner.TogglePause(Now); popup?.Close(); Changed?.Invoke(); }
    public void Preview() => ShowReminder(true);
    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.SessionUnlock)
            timer.Dispatcher.BeginInvoke(() => { locked = e.Reason == SessionSwitchReason.SessionLock; planner.Reset(Now); popup?.Close(); Changed?.Invoke(); });
    }
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) timer.Dispatcher.BeginInvoke(() => { planner.Reset(Now); popup?.Close(); Changed?.Invoke(); });
    }
    private void Tick(object? sender, EventArgs args)
    {
        if (locked) return;
        var now = Now;
        // Only inspect foreground windows when a reminder is due or deferred.
        bool relevant = planner.DeferredSince != null || planner.DisplayDue is { } due && now >= due;
        bool fullScreen = Settings.FullScreenQuiet && relevant && FullScreenDetector.IsFullScreen();
        if (planner.Advance(now, fullScreen)) ShowReminder(false);
        else if (!popupIsPreview && ScheduleEngine.ActivePeriod(Settings, now) == null) popup?.Close();
        Changed?.Invoke();
    }
    private void ShowReminder(bool preview)
    {
        popup?.Close(); var rules = Settings.Reminders.Where(r => r.Enabled).ToArray();
        var window = new PetWindow(rules[ruleIndex % rules.Length], preview); if (!preview) ruleIndex++;
        popup = window; popupIsPreview = preview;
        window.SnoozeRequested += () => { if (!preview) planner.Snooze(Now); Changed?.Invoke(); };
        window.Closed += (_, _) => { if (ReferenceEquals(popup, window)) popup = null; }; window.Show();
    }
    public void Dispose()
    {
        timer.Stop(); SystemEvents.SessionSwitch -= OnSessionSwitch; SystemEvents.PowerModeChanged -= OnPowerModeChanged; popup?.Close();
    }
}
