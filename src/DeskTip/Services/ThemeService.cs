using DeskTip.Core;
using Microsoft.Win32;

namespace DeskTip.Services;

public static class ThemeService
{
    public static ThemePreference Preference { get; private set; } = ThemePreference.Dark;
    public static ThemePreference Resolved { get; private set; } = ThemePreference.Dark;
    public static event Action? Changed;
    private static bool initialized;
    public static void Initialize(ThemePreference preference)
    {
        if (!initialized) { SystemEvents.UserPreferenceChanged += OnWindowsPreferenceChanged; initialized = true; }
        Apply(preference);
    }
    public static bool WindowsLight()
    {
        try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"); return key?.GetValue("AppsUseLightTheme") is not int value || value != 0; }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException) { return true; }
    }
    private static void OnWindowsPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (Preference != ThemePreference.System || Application.Current == null || Application.Current.Dispatcher.HasShutdownStarted) return;
        Application.Current.Dispatcher.BeginInvoke(() => { if (Preference == ThemePreference.System) Apply(ThemePreference.System); });
    }
    public static void Apply(ThemePreference preference)
    {
        var resolved = ThemeRules.Resolve(preference, preference == ThemePreference.System && WindowsLight());
        bool preferenceChanged = Preference != preference; Preference = preference;
        bool initial = Application.Current.Resources[ThemePalette.Key("#111827")] == null;
        if (!initial && resolved == Resolved) { if (preferenceChanged) Changed?.Invoke(); return; }
        Resolved = resolved;
        foreach (var pair in ThemePalette.Light)
        {
            var color = (Color)ColorConverter.ConvertFromString(resolved == ThemePreference.Dark ? pair.Key : pair.Value);
            var brush = new SolidColorBrush(color); brush.Freeze();
            Application.Current.Resources[ThemePalette.Key(pair.Key)] = brush;
            Application.Current.Resources[ThemePalette.Key(pair.Key) + ".Color"] = color;
        }
        Changed?.Invoke();
    }
    public static SolidColorBrush Accent(string original) => IconArt.Brush(ThemeRules.Accent(original, Resolved));
    public static void Dispose() { if (initialized) SystemEvents.UserPreferenceChanged -= OnWindowsPreferenceChanged; initialized = false; }
}
