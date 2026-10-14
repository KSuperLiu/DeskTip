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
        var settings = SettingsStore.Load(); ThemeService.Initialize(settings.Theme);
        Reminders = new ReminderService(settings);
        CreateTray();
        if (e.Args.Contains("--capture")) { _ = Capture(e.Args); return; }
        if (!e.Args.Contains("--background")) { ShowSettings(); if (e.Args.Contains("--settings")) settingsWindow!.SelectPage(2); }
    }
    private void CreateTray()
    {
        using var iconStream = GetResourceStream(new Uri("pack://application:,,,/Assets/desktip.ico")).Stream;
        tray = new Forms.NotifyIcon { Text = "DeskTip · 提醒中心", Visible = true, Icon = new System.Drawing.Icon(iconStream, 32, 32) };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开 DeskTip", null, (_, _) => ShowSettings());
        menu.Items.Add("预览桌面提醒", null, (_, _) => Reminders.Preview());
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
            for (int p = 0; p < 3; p++) { window.SelectPage(p); await Task.Delay(200); SaveVisual(window, Path.Combine(folder, new[] { "overview.png", "templates.png", "settings.png" }[p])); }
            var editor = new ReminderEditorWindow(TemplateCatalog.Create("backup"), Reminders, false) { Owner = window };
            editor.Show(); await Task.Delay(200); SaveVisual(editor, Path.Combine(folder, "editor.png")); editor.Close();
            window.Activate(); await Task.Delay(100);
            bool wasActive = window.IsActive;
            if (!wasActive) throw new Exception("焦点验证需要以正常可见窗口运行 --capture，请勿使用隐藏启动。");
            var sample = Reminders.Settings.Reminders.FirstOrDefault() ?? TemplateCatalog.Create("water");
            var pet = new PetWindow(new[] { new ReminderOccurrence(sample, Reminders.Now, sample.Message) }, true); pet.Show(); await Task.Delay(250); SaveVisual(pet, Path.Combine(folder, "reminder.png"));
            if (wasActive && !window.IsActive) throw new Exception("提醒抢走了主窗口焦点。");
            pet.Close();
            var longRule = TemplateCatalog.Create("focus"); longRule.Name = string.Concat(Enumerable.Repeat("重要提醒", 10)); longRule.Message = string.Concat(Enumerable.Repeat("请按照自己的计划开始这件事。", 15));
            var longPet = new PetWindow(new[] { new ReminderOccurrence(longRule, Reminders.Now, longRule.Message) }, true);
            longPet.Show(); await Task.Delay(200); SaveVisual(longPet, Path.Combine(folder, "reminder-long.png")); longPet.Close();
            var batch = new PetWindow(new[] { new ReminderOccurrence(TemplateCatalog.Create("water"), Reminders.Now, "喝水提醒"), new ReminderOccurrence(TemplateCatalog.Create("plan"), Reminders.Now, "规划今天的事项") }, true);
            batch.Show(); await Task.Delay(200); SaveVisual(batch, Path.Combine(folder, "reminder-batch.png")); batch.Close();
            var compactRule = TemplateCatalog.Create("read"); compactRule.ShowCompanion = false;
            var compact = new PetWindow(new[] { new ReminderOccurrence(compactRule, Reminders.Now, compactRule.Message) }, true); compact.Show(); await Task.Delay(200); SaveVisual(compact, Path.Combine(folder, "reminder-compact.png")); compact.Close();
            checks += await CaptureThemes(window, folder);
            File.WriteAllText(Path.Combine(folder, "capture-ok.txt"), "All WPF views rendered successfully.\n" + checks + "\nPASS reminder does not steal focus."); Shutdown();
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "capture-error.txt"), ex.ToString()); Shutdown(1); }
    }
    private async Task<string> CaptureThemes(MainWindow window, string folder)
    {
        var original = ThemeService.Preference;
        string before = System.Text.Json.JsonSerializer.Serialize(Reminders.Settings, SettingsCodec.Json);
        DateTime? due = Reminders.DisplayDue;
        var editor = new ReminderEditorWindow(TemplateCatalog.Create("water"), Reminders, false) { Owner = window };
        var rule = TemplateCatalog.Create("water");
        var pet = new PetWindow(new[] { new ReminderOccurrence(rule, Reminders.Now, rule.Message) }, true);
        editor.Show(); pet.Show();
        try
        {
            foreach (var theme in new[] { ThemePreference.Dark, ThemePreference.Light })
            {
                ThemeService.Apply(theme); await Task.Delay(200);
                var expected = (Color)ColorConverter.ConvertFromString(theme == ThemePreference.Dark ? "#111827" : "#F6F7FC");
                if (((SolidColorBrush)window.Background).Color != expected || ((SolidColorBrush)editor.Background).Color != expected) throw new Exception("现有窗口没有跟随主题更新。");
                if (!pet.IsVisible) throw new Exception("切换主题关闭了提醒。");
                string suffix = theme.ToString().ToLowerInvariant();
                for (int p = 0; p < 3; p++) { window.SelectPage(p); await Task.Delay(100); SaveVisual(window, Path.Combine(folder, new[] { "overview-", "templates-", "settings-" }[p] + suffix + ".png")); }
                SaveVisual(editor, Path.Combine(folder, "editor-" + suffix + ".png")); SaveVisual(pet, Path.Combine(folder, "reminder-" + suffix + ".png"));
            }
            ThemeService.Apply(ThemePreference.System);
            if (ThemeService.Resolved != ThemeRules.Resolve(ThemePreference.System, ThemeService.WindowsLight())) throw new Exception("跟随系统主题解析错误。");
            if (before != System.Text.Json.JsonSerializer.Serialize(Reminders.Settings, SettingsCodec.Json) || due != Reminders.DisplayDue) throw new Exception("主题预览改变了规则或计时。");
            return "; PASS live dark/light windows; system theme resolution; no config/timer mutation";
        }
        finally { editor.Close(); pet.Close(); ThemeService.Apply(original); }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        ThemeService.Dispose();
        showWait?.Unregister(null); quitWait?.Unregister(null); showSignal?.Dispose(); quitSignal?.Dispose();
        Reminders?.Dispose(); if (tray != null) { tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Icon?.Dispose(); tray.Dispose(); }
        instance?.Dispose(); base.OnExit(e);
    }
}
