using Microsoft.Win32;
namespace DeskTip.Services;
public static class StartupService
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static string Executable => Environment.ProcessPath ?? throw new InvalidOperationException("无法确定程序路径，请从解压目录运行 DeskTip.exe。");
    private static string Command => "\"" + Executable + "\" --background";
    public static string? RegisteredCommand { get { using var key = Registry.CurrentUser.OpenSubKey(Key); return key?.GetValue("DeskTip") as string; } }
    public static bool IsEnabled() => string.Equals(RegisteredCommand, Command, StringComparison.OrdinalIgnoreCase);
    public static void SetEnabled(bool enabled)
    {
        if (enabled && !File.Exists(Executable)) throw new IOException("程序路径不存在，请重新解压并运行 DeskTip.exe。");
        using (var key = Registry.CurrentUser.CreateSubKey(Key))
        {
            if (enabled) key.SetValue("DeskTip", Command, RegistryValueKind.String); else key.DeleteValue("DeskTip", false);
        }
        if (enabled ? !IsEnabled() : RegisteredCommand != null) throw new IOException("启动项未成功保存，请检查当前用户的注册表写入权限。");
    }
}
