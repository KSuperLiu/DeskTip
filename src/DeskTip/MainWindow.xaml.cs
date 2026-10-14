using DeskTip.Core;
using DeskTip.Services;
using Microsoft.Win32;
using System.Text.Json;
using System.Windows.Input;

namespace DeskTip;

public partial class MainWindow : Window
{
    private readonly ReminderService service;
    private CalendarData calendar;
    private string category = "全部";
    private bool ready;
    private int page;
    private string lastUiSignature = "";
    public MainWindow(ReminderService service)
    {
        this.service = service; calendar = SettingsStore.Clone(service.Settings).Calendar;
        InitializeComponent(); Width = Math.Min(1260, SystemParameters.WorkArea.Width - 30); Height = Math.Min(850, SystemParameters.WorkArea.Height - 30);
        HeroImage.Source = AssetService.Pet; ready = true; LoadPreferences(); SelectPage(0); Refresh();
        if (SettingsStore.LoadWarning != null) Feedback.Text = SettingsStore.LoadWarning;
        service.Changed += Refresh; Closed += (_, _) => service.Changed -= Refresh;
        ThemeService.Changed += RefreshTheme; Closed += (_, _) => ThemeService.Changed -= RefreshTheme;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }
    private void LoadPreferences()
    {
        EnabledInput.IsChecked = service.Settings.Enabled; QuietInput.IsChecked = service.Settings.FullScreenQuiet;
        BeijingInput.IsChecked = service.Settings.BeijingTime; StartupInput.IsChecked = StartupService.IsEnabled();
        calendar = SettingsStore.Clone(service.Settings).Calendar; CalendarStatus();
        UpdateThemeChoice();
    }
    private void CalendarStatus()
    {
        CalendarInfo.Text = "已覆盖年份：" + (calendar.Years.Count == 0 ? "暂无" : string.Join("、", calendar.Years.OrderBy(x => x))) + "。\n仅对选择「中国工作日」的规则生效；未覆盖年份按周一至周五运行，请导入新日历。";
    }
    public void Refresh()
    {
        if (!ready) return;
        var now = service.Now; var settings = service.Settings;
        int enabled = settings.Reminders.Count(r => r.Enabled);
        StatusText.Text = !settings.Enabled ? "全部提醒已关闭" : service.Paused ? "已暂停 · 等待手动继续" : $"安静运行 · {enabled} 条提醒已开启";
        if (settings.Reminders.Any(r => r.Enabled && r.Days == DayPolicy.ChinaWorkdays && r.Mode != ScheduleMode.Once) && !settings.Calendar.Years.Contains(now.Year)) StatusText.Text += " · 工作日历待更新";
        ClockText.Text = now.ToString("MM/dd  HH:mm") + (settings.BeijingTime ? "  北京时间" : "  本机时间");
        ActiveCountText.Text = enabled.ToString("00"); TotalCountText.Text = "已开启 / 共 " + settings.Reminders.Count + " 条";
        var next = service.DisplayDue;
        var nextRule = settings.Reminders.FirstOrDefault(r => next != null && service.NextFor(r.Id) == next);
        NextReminderText.Text = !settings.Enabled ? "全部关闭" : service.Paused ? "暂停中" : next is { } due ? (due.Date == now.Date ? due.ToString("HH:mm") : due.ToString("MM/dd HH:mm")) + "  ·  " + nextRule?.Name : "暂无待触发提醒";
        RunStateText.Text = !settings.Enabled ? "已关闭" : service.Paused ? "暂停中" : "轻量运行";
        PauseButton.Content = service.Paused ? "继续" : "暂停"; PauseButton.IsEnabled = settings.Enabled;
        string signature = JsonSerializer.Serialize(settings, SettingsCodec.Json) + now.ToString("yyyyMMddHHmm") + service.Paused + next;
        if (lastUiSignature != signature) { lastUiSignature = signature; RefreshReminders(); }
    }
    private void RefreshReminders()
    {
        string query = ReminderSearch.Text.Trim(); var now = service.Now;
        var cards = service.Settings.Reminders.Where(r => query.Length == 0 || (r.Name + r.Category + r.Message).Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(r => new ReminderCard(r, service.NextFor(r.Id), now, service.Paused, service.Settings.Enabled)).ToArray();
        ReminderItems.ItemsSource = cards; EmptyReminders.Visibility = cards.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void RefreshTemplates()
    {
        string query = TemplateSearch.Text.Trim();
        var cards = TemplateCatalog.All.Where(t => (category == "全部" || t.Category == category) && (query.Length == 0 || (t.Name + t.Category + t.Description).Contains(query, StringComparison.OrdinalIgnoreCase))).Select(t => new TemplateCard(t)).ToArray();
        TemplateItems.ItemsSource = cards; EmptyTemplates.Visibility = cards.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (Button button in CategoryFilters.Children) button.SetResourceReference(BackgroundProperty, ThemePalette.Key((string)button.Tag == category ? "#484064" : "#202A40"));
    }
    public void SelectPage(int index)
    {
        page = index; RemindersPage.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        TemplatesPage.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed; SettingsPage.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        SavePreferences.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed; PageScroll.ScrollToTop();
        var navs = new[] { RemindersNav, TemplatesNav, SettingsNav }; for (int i = 0; i < navs.Length; i++) { if (i == index) navs[i].SetResourceReference(BackgroundProperty, ThemePalette.Key("#292744")); else navs[i].Background = Brushes.Transparent; navs[i].SetResourceReference(ForegroundProperty, ThemePalette.Key(i == index ? "#C0B3FF" : "#8293B1")); }
        RefreshReminders(); RefreshTemplates();
    }
    private void Reminders_Click(object sender, RoutedEventArgs e) => SelectPage(0);
    private void Templates_Click(object sender, RoutedEventArgs e) => SelectPage(1);
    private void Settings_Click(object sender, RoutedEventArgs e) => SelectPage(2);
    private void Search_Changed(object sender, TextChangedEventArgs e) { if (ready) { RefreshReminders(); RefreshTemplates(); } }
    private void Category_Click(object sender, RoutedEventArgs e) { category = (string)((Button)sender).Tag; RefreshTemplates(); }
    private void Pause_Click(object sender, RoutedEventArgs e) => service.TogglePause();
    private void Preview_Click(object sender, RoutedEventArgs e) => service.Preview();
    private void PreviewRule_Click(object sender, RoutedEventArgs e) { var rule = RuleFor(sender); if (rule != null) service.Preview(rule); }
    private ReminderDefinition? RuleFor(object sender) => service.Settings.Reminders.FirstOrDefault(r => r.Id == (string)((FrameworkElement)sender).Tag);
    private void New_Click(object sender, RoutedEventArgs e) => Edit(new() { Mode = ScheduleMode.FixedTimes, Times = new() { service.Now.AddHours(1).ToString("HH:mm") } }, false);
    private void UseTemplate_Click(object sender, RoutedEventArgs e)
    {
        var rule = TemplateCatalog.Create((string)((Button)sender).Tag);
        if (rule.Mode == ScheduleMode.Once) rule.OnceAt = service.Now.Date.AddDays(1).AddHours(10).ToString("yyyy-MM-dd HH:mm"); Edit(rule, false);
    }
    private void EditRule_Click(object sender, RoutedEventArgs e) { var rule = RuleFor(sender); if (rule != null) Edit(rule, true); }
    private void Edit(ReminderDefinition rule, bool editing)
    {
        if (!editing && service.Settings.Reminders.Count >= 60) { Feedback.Text = "最多支持 60 条提醒，请先整理已有规则。"; return; }
        var editor = new ReminderEditorWindow(rule, service, editing) { Owner = this }; editor.ShowDialog();
        if (editor.Result == null && !editor.DeleteRequested) return;
        try
        {
            var next = SettingsStore.Clone(service.Settings); next.Reminders.RemoveAll(r => r.Id == rule.Id);
            if (!editor.DeleteRequested)
            {
                int index = service.Settings.Reminders.FindIndex(r => r.Id == rule.Id);
                if (index >= 0) next.Reminders.Insert(Math.Min(index, next.Reminders.Count), editor.Result!); else next.Reminders.Add(editor.Result!);
            }
            Save(next); Feedback.Text = editor.DeleteRequested ? "提醒已删除。" : "提醒已保存 ✓ 其他提醒的规则与稍后状态保持不变。"; SelectPage(0);
        }
        catch (Exception ex) { Feedback.Text = "保存失败：" + ex.Message; }
    }
    private void ToggleRule_Click(object sender, RoutedEventArgs e)
    {
        try { var next = SettingsStore.Clone(service.Settings); var rule = next.Reminders.First(r => r.Id == (string)((FrameworkElement)sender).Tag); rule.Enabled = !rule.Enabled; Save(next); Feedback.Text = rule.Name + (rule.Enabled ? "已开启。" : "已停用。"); }
        catch (Exception ex) { Feedback.Text = ex.Message; RefreshReminders(); }
    }
    private void Save(AppSettings settings) { settings.Validate(); var copy = SettingsStore.Clone(settings); SettingsStore.Save(copy); service.Apply(copy); ThemeService.Apply(copy.Theme); lastUiSignature = ""; Refresh(); }
    private void RefreshTheme() { if (!ready) return; UpdateThemeChoice(); RefreshReminders(); RefreshTemplates(); }
    private void UpdateThemeChoice()
    {
        var buttons = new[] { ThemeDarkButton, ThemeLightButton, ThemeSystemButton };
        foreach (var button in buttons)
        {
            bool selected = Enum.Parse<ThemePreference>((string)button.Tag) == ThemeService.Preference;
            button.SetResourceReference(BorderBrushProperty, ThemePalette.Key(selected ? "#A99CFF" : "#303B55"));
            button.SetResourceReference(BackgroundProperty, ThemePalette.Key(selected ? "#292744" : "#202A40"));
            button.BorderThickness = new Thickness(selected ? 2 : 1);
            System.Windows.Automation.AutomationProperties.SetItemStatus(button, selected ? "已选择" : "未选择");
        }
        ThemeStatus.Text = ThemeService.Preference == ThemePreference.System ? "跟随系统 · 当前" + (ThemeService.Resolved == ThemePreference.Light ? "浅色" : "深色") : (ThemeService.Preference == ThemePreference.Light ? "浅色主题" : "深色主题");
    }
    private void ThemeChoice_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var theme = Enum.Parse<ThemePreference>((string)((Button)sender).Tag);
            var next = SettingsStore.Clone(service.Settings); next.Theme = theme; SettingsStore.Save(next);
            service.SetTheme(theme); ThemeService.Apply(theme); Feedback.Text = "主题已切换并自动保存 ✓";
        }
        catch (Exception ex) { Feedback.Text = "主题保存失败：" + ex.Message; }
    }
    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var next = SettingsStore.Clone(service.Settings); next.Enabled = EnabledInput.IsChecked == true; next.BeijingTime = BeijingInput.IsChecked == true; next.FullScreenQuiet = QuietInput.IsChecked == true; next.Calendar = calendar; next.Validate();
            bool before = StartupService.IsEnabled(); StartupService.SetEnabled(StartupInput.IsChecked == true);
            try { Save(next); } catch { StartupService.SetEnabled(before); throw; } Feedback.Text = "全局偏好已保存 ✓";
        }
        catch (Exception ex) { Feedback.Text = "保存失败：" + ex.Message; }
    }
    private static string ReadJson(string path) { if (new FileInfo(path).Length > 3_000_000) throw new ArgumentException("配置文件不能超过 3 MB。"); return File.ReadAllText(path); }
    private void ImportCalendar_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "工作日历 (*.json)|*.json" }; if (dialog.ShowDialog() != true) return;
        try
        {
            var value = JsonSerializer.Deserialize<CalendarData>(ReadJson(dialog.FileName)) ?? throw new ArgumentException("日历为空。"); value.Validate();
            foreach (var year in value.Years) foreach (var key in calendar.Overrides.Keys.Where(k => k.StartsWith(year + "-", StringComparison.Ordinal)).ToList()) calendar.Overrides.Remove(key);
            foreach (var pair in value.Overrides) calendar.Overrides[pair.Key] = pair.Value;
            calendar.Years = calendar.Years.Concat(value.Years).Distinct().OrderBy(y => y).ToList(); calendar.Source = value.Source; CalendarStatus(); Feedback.Text = "日历已载入，保存偏好后生效。";
        }
        catch (Exception ex) { Feedback.Text = "导入失败：" + ex.Message; }
    }
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "DeskTip 配置 (*.json)|*.json", FileName = "DeskTip-config-v2.json" }; if (dialog.ShowDialog() != true) return;
        try { File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(service.Settings, SettingsCodec.Json)); Feedback.Text = "已导出全部生效规则和日历。"; } catch (Exception ex) { Feedback.Text = ex.Message; }
    }
    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "DeskTip 配置 (*.json)|*.json" }; if (dialog.ShowDialog() != true) return;
        try { Save(SettingsCodec.Read(ReadJson(dialog.FileName))); LoadPreferences(); Feedback.Text = "配置已验证并导入 ✓ 支持 v1 自动迁移。"; } catch (Exception ex) { Feedback.Text = "导入失败：" + ex.Message; }
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Title_MouseDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) { if (e.ClickCount == 2) Maximize_Click(sender, e); else DragMove(); } }
    internal string VerifyDraftValidation()
    {
        string before = JsonSerializer.Serialize(service.Settings, SettingsCodec.Json);
        ReminderSearch.Text = "__没有匹配的提醒__"; if (ReminderItems.Items.Count != 0) throw new Exception("提醒搜索失效。"); ReminderSearch.Text = "";
        category = "健康"; RefreshTemplates(); if (TemplateItems.Items.Count != 4) throw new Exception("模板分类过滤失效。");
        category = "全部"; TemplateSearch.Text = "备份"; RefreshTemplates(); if (TemplateItems.Items.Count != 1) throw new Exception("模板搜索失效。"); TemplateSearch.Text = ""; RefreshTemplates();
        var rule = TemplateCatalog.Create("water"); var editor = new ReminderEditorWindow(rule, service, false); string result = editor.VerifyDraftValidation(); editor.Close();
        if (before != JsonSerializer.Serialize(service.Settings, SettingsCodec.Json)) throw new Exception("界面验证修改了生效规则。"); return result + "; reminder search; template category/search; no data mutation";
    }
}
