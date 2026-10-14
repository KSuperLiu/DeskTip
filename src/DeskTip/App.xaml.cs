using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskTip.Core;
using DeskTip.Services;
using Forms = System.Windows.Forms;
namespace DeskTip;

public partial class App : Application
{
    private Mutex? instance;
    private EventWaitHandle? showSignal, quitSignal;
    private RegisteredWaitHandle? showWait, quitWait;
    private Forms.NotifyIcon? tray;
    private MainWindow? settingsWindow;
    public ReminderService Reminders { get; private set; } = null!;
    private const string Prefix = @"Local\DeskTip.Wellness.";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--quit"))
        {
            try { using var quit = EventWaitHandle.OpenExisting(Prefix + "Quit"); quit.Set(); } catch (WaitHandleCannotBeOpenedException) { }
            Shutdown(); return;
        }
        instance = new Mutex(true, Prefix + "Instance", out bool created);
        if (!created) { try { using var show = EventWaitHandle.OpenExisting(Prefix + "Show"); show.Set(); } catch (WaitHandleCannotBeOpenedException) { } Shutdown(); return; }
        showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, Prefix + "Show");
        quitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, Prefix + "Quit");
        showWait = ThreadPool.RegisterWaitForSingleObject(showSignal, (_, _) => Dispatcher.BeginInvoke(ShowSettings), null, -1, false);
        quitWait = ThreadPool.RegisterWaitForSingleObject(quitSignal, (_, _) => Dispatcher.BeginInvoke(() => Shutdown()), null, -1, false);
        DispatcherUnhandledException += (_, args) =>
        {
            Directory.CreateDirectory(SettingsStore.Folder); File.AppendAllText(Path.Combine(SettingsStore.Folder, "error.log"), DateTime.Now + " " + args.Exception + Environment.NewLine);
            MessageBox.Show("操作未完成：" + args.Exception.Message, "DeskTip", MessageBoxButton.OK, MessageBoxImage.Warning); args.Handled = true;
        };
        Reminders = new ReminderService(SettingsStore.Load());
        CreateTray();
        if (e.Args.Contains("--capture")) { _ = Capture(e.Args); return; }
        if (!e.Args.Contains("--background")) ShowSettings();
    }
    private void CreateTray()
    {
        using var iconStream = GetResourceStream(new Uri("pack://application:,,,/Assets/desktip.ico")).Stream;
        tray = new Forms.NotifyIcon { Text = "DeskTip · 小憩", Visible = true, Icon = new System.Drawing.Icon(iconStream, 32, 32) };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开 DeskTip", null, (_, _) => ShowSettings());
        menu.Items.Add("预览休息提醒", null, (_, _) => Reminders.Preview());
        var pause = menu.Items.Add("暂停提醒", null, (_, _) => Reminders.TogglePause());
        menu.Opening += (_, _) => pause.Text = Reminders.Paused ? "继续提醒" : "暂停提醒";
        menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add("退出", null, (_, _) => Shutdown());
        tray.ContextMenuStrip = menu; tray.DoubleClick += (_, _) => ShowSettings();
    }
    public void ShowSettings()
    {
        if (settingsWindow == null)
        {
            settingsWindow = new MainWindow(Reminders); MainWindow = settingsWindow;
            settingsWindow.Closed += (_, _) => { settingsWindow = null; MainWindow = null; Dispatcher.BeginInvoke(() => GC.Collect(), DispatcherPriority.ApplicationIdle); };
        }
        settingsWindow.Show(); if (settingsWindow.WindowState == WindowState.Minimized) settingsWindow.WindowState = WindowState.Normal; settingsWindow.Activate();
    }
    private static void SaveVisual(Window window, string path)
    {
        window.UpdateLayout();
        var bmp = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bmp)); using var file = File.Create(path); encoder.Save(file);
    }
    private async Task Capture(string[] args)
    {
        try
        {
            int index = Array.IndexOf(args, "--capture"); string folder = index + 1 < args.Length ? args[index + 1] : "artifacts/previews"; Directory.CreateDirectory(folder);
            ShowSettings(); var window = settingsWindow!; await Task.Delay(700);
            string checks = window.VerifyDraftValidation();
            for (int p = 0; p < 3; p++) { window.SelectPage(p); await Task.Delay(200); SaveVisual(window, Path.Combine(folder, new[] { "overview.png", "rules.png", "settings.png" }[p])); }
            window.Activate(); await Task.Delay(100);
            bool wasActive = window.IsActive;
            if (!wasActive) throw new Exception("焦点验证需要以正常可见窗口运行 --capture，请勿使用隐藏启动。");
            var pet = new PetWindow(Reminders.Settings.Reminders[0], true); pet.Show(); await Task.Delay(250); SaveVisual(pet, Path.Combine(folder, "reminder.png"));
            if (wasActive && !window.IsActive) throw new Exception("提醒抢走了主窗口焦点。");
            pet.Close();
            var longPet = new PetWindow(new ReminderRule { Title = string.Concat(Enumerable.Repeat("休息一下", 10)), Message = string.Concat(Enumerable.Repeat("起来走走，接水看看远处。", 12)) }, true);
            longPet.Show(); await Task.Delay(200); SaveVisual(longPet, Path.Combine(folder, "reminder-long.png")); longPet.Close();
            File.WriteAllText(Path.Combine(folder, "capture-ok.txt"), "All WPF views rendered successfully.\n" + checks + "\nPASS reminder does not steal focus."); Shutdown();
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "capture-error.txt"), ex.ToString()); Shutdown(1); }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        showWait?.Unregister(null); quitWait?.Unregister(null); showSignal?.Dispose(); quitSignal?.Dispose();
        Reminders?.Dispose(); if (tray != null) { tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Icon?.Dispose(); tray.Dispose(); }
        instance?.Dispose(); base.OnExit(e);
    }
}
