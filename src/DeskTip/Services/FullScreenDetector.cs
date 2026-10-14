using System.Runtime.InteropServices;
namespace DeskTip.Services;
public static class FullScreenDetector
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle, out Rect rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr handle, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] private static extern int GetClassName(IntPtr handle, System.Text.StringBuilder name, int length);
    public static bool IsFullScreen()
    {
        var window = GetForegroundWindow(); if (window == IntPtr.Zero || window == GetShellWindow()) return false;
        var name = new System.Text.StringBuilder(256); GetClassName(window, name, name.Capacity); if (name.ToString() is "Progman" or "WorkerW") return false;
        if (!GetWindowRect(window, out var rect)) return false;
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() }; if (!GetMonitorInfo(MonitorFromWindow(window, 2), ref info)) return false;
        return rect.Left <= info.Monitor.Left && rect.Top <= info.Monitor.Top && rect.Right >= info.Monitor.Right && rect.Bottom >= info.Monitor.Bottom;
    }
}
