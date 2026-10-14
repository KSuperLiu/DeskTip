using Microsoft.Win32;
namespace DeskTip.Services;
public static class StartupService
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private static string Command => "\"" + Environment.ProcessPath + "\" --background";
    public static bool IsEnabled() { using var key = Registry.CurrentUser.OpenSubKey(Key); return string.Equals(key?.GetValue("DeskTip") as string, Command, StringComparison.OrdinalIgnoreCase); }
    public static void SetEnabled(bool enabled) { using var key = Registry.CurrentUser.CreateSubKey(Key); if (enabled) key.SetValue("DeskTip", Command); else key.DeleteValue("DeskTip", false); }
}
