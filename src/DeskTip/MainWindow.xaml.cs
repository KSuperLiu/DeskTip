using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using DeskTip.Core;
using DeskTip.Services;
using Microsoft.Win32;
namespace DeskTip;

public partial class MainWindow : Window
{
    private readonly ReminderService service;
    private ObservableCollection<WorkPeriod> periods = new();
    private ObservableCollection<ReminderRule> rules = new();
    private CalendarData calendar = new();
    private int page;
    public MainWindow(ReminderService service)
    {
        this.service = service; InitializeComponent();
        HeroPet.Source = AssetService.Pet;
        LoadEditors(); SelectPage(0); Refresh();
        service.Changed += Refresh;
        Closed += (_, _) => service.Changed -= Refresh;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        if (SettingsStore.LoadWarning != null) SettingsFeedback.Text = SettingsStore.LoadWarning;
    }
    private void LoadEditors()
    {
        var draft = SettingsStore.Clone(service.Settings); periods = new(draft.Periods); rules = new(draft.Reminders); calendar = draft.Calendar;
        PeriodsGrid.ItemsSource = periods; RemindersEditor.ItemsSource = rules; IntervalInput.Text = draft.IntervalMinutes.ToString();
        EnabledInput.IsChecked = draft.Enabled; StartupInput.IsChecked = StartupService.IsEnabled(); QuietInput.IsChecked = draft.FullScreenQuiet;
        BeijingInput.IsChecked = draft.BeijingTime; ChinaInput.IsChecked = draft.ChinaCalendar; UpdateCalendarInfo();
    }
    private void UpdateCalendarInfo()
    {
        CalendarInfoText.Text = "已覆盖年份：" + (calendar.Years.Count > 0 ? string.Join("、", calendar.Years.OrderBy(x => x)) : "暂无") + "。\n未覆盖年份按周一至周五运行，并显示日历待更新提示。可导入 JSON 日历补充新年份或公司专属安排。";
    }
    public void Refresh()
    {
        var now = service.Now; var settings = service.Settings;
        var active = ScheduleEngine.ActivePeriod(settings, now);
        StatusText.Text = !settings.Enabled ? "提醒已关闭" : service.Paused ? "已暂停 · 手动恢复后继续" : active != null ? "正在陪伴 · " + active.Name + "工作时段" : "安静等待 · 工作时段外";
        ClockText.Text = now.ToString("MM 月 dd 日  HH:mm") + (settings.BeijingTime ? "  北京时间" : "  本机时间");
        var next = service.DisplayDue;
        NextTimeText.Text = !settings.Enabled ? "已关闭" : service.Paused ? "已暂停" : next?.ToString("HH:mm") ?? "暂无";
        NextDetailText.Text = !settings.Enabled ? "可在偏好设置中开启" : service.Paused ? "点击「继续提醒」恢复" : next == null ? "时段内没有可用的提醒时间" : next.Value.Date == now.Date ? "今天 · 到时间轻轻提醒" : next.Value.ToString("MM 月 dd 日") + " · 下个工作日";
        IntervalText.Text = settings.IntervalMinutes.ToString();
        CalendarModeText.Text = settings.ChinaCalendar ? "中国工作日" : "周一至周五";
        CalendarDetailText.Text = settings.ChinaCalendar && !settings.Calendar.Years.Contains(now.Year) ? now.Year + " 日历待更新" : settings.ChinaCalendar ? "节假日休息 · 调休日提醒" : "周末安静休息";
        PeriodsSummary.ItemsSource = settings.Periods; PauseButton.Content = service.Paused ? "继续提醒" : "暂停提醒";
        PauseButton.IsEnabled = settings.Enabled;
    }
    public void SelectPage(int index)
    {
        page = index; OverviewPage.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        RulesPage.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed; SettingsPage.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        FooterHint.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        FooterSave.Visibility = index == 0 ? Visibility.Collapsed : Visibility.Visible;
        FooterSave.Content = index == 1 ? "保存规则  ✓" : "保存偏好  ✓";
        RulesFeedback.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        SettingsFeedback.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        ContentScroll.ScrollToTop();
        var buttons = new[] { OverviewNav, RulesNav, SettingsNav };
        for (int i = 0; i < buttons.Length; i++) { buttons[i].Background = new SolidColorBrush(i == index ? Color.FromRgb(218, 233, 208) : Colors.Transparent); buttons[i].FontWeight = i == index ? FontWeights.SemiBold : FontWeights.Normal; }
    }
    private void Overview_Click(object sender, RoutedEventArgs e) => SelectPage(0);
    private void Rules_Click(object sender, RoutedEventArgs e) => SelectPage(1);
    private void Settings_Click(object sender, RoutedEventArgs e) => SelectPage(2);
    private void FooterSave_Click(object sender, RoutedEventArgs e) { if (page == 1) SaveRules_Click(sender, e); else SaveSettings_Click(sender, e); }
    private void Preview_Click(object sender, RoutedEventArgs e) => service.Preview();
    private void Pause_Click(object sender, RoutedEventArgs e) => service.TogglePause();
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Title_MouseDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) { if (e.ClickCount == 2) Maximize_Click(sender, e); else DragMove(); } }
    private void AddPeriod_Click(object sender, RoutedEventArgs e) { if (periods.Count < 12) periods.Add(new() { Name = "新时段", Start = "19:00", End = "20:00" }); }
    private void RemovePeriod_Click(object sender, RoutedEventArgs e) { if (PeriodsGrid.SelectedItem is WorkPeriod p) periods.Remove(p); }
    private void AddReminder_Click(object sender, RoutedEventArgs e) { if (rules.Count < 20) rules.Add(new() { Title = "新提醒", Message = "起身休息一下吧。" }); }
    private void RemoveReminder_Click(object sender, RoutedEventArgs e) { if (sender is Button { Tag: ReminderRule r }) rules.Remove(r); }
    private void SaveRules_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            PeriodsGrid.CommitEdit(DataGridEditingUnit.Cell, true); PeriodsGrid.CommitEdit(DataGridEditingUnit.Row, true);
            if (!int.TryParse(IntervalInput.Text, out var interval)) throw new ArgumentException("提醒间隔请填写整数。");
            var value = SettingsStore.Clone(service.Settings); value.Periods = periods.ToList(); value.Reminders = rules.ToList(); value.IntervalMinutes = interval;
            value.Validate(); var saved = SettingsStore.Clone(value); SettingsStore.Save(saved); service.Apply(saved); RulesFeedback.Text = "已保存 ✓ 新规则从当前时刻开始计算。";
        }
        catch (Exception ex) { RulesFeedback.Text = ex.Message; }
    }
    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var value = SettingsStore.Clone(service.Settings); value.Enabled = EnabledInput.IsChecked == true;
            value.FullScreenQuiet = QuietInput.IsChecked == true; value.BeijingTime = BeijingInput.IsChecked == true; value.ChinaCalendar = ChinaInput.IsChecked == true; value.Calendar = calendar;
            value.Validate();
            var originalStartup = StartupService.IsEnabled(); StartupService.SetEnabled(StartupInput.IsChecked == true);
            try { SettingsStore.Save(value); } catch { StartupService.SetEnabled(originalStartup); throw; }
            service.Apply(SettingsStore.Clone(value)); SettingsFeedback.Text = "偏好已保存 ✓";
        }
        catch (Exception ex) { SettingsFeedback.Text = "保存失败：" + ex.Message; }
    }
    private static string ReadJson(string path) { if (new FileInfo(path).Length > 2_000_000) throw new ArgumentException("配置文件不能超过 2 MB。"); return File.ReadAllText(path); }
    private void ImportCalendar_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "工作日历 (*.json)|*.json", Title = "导入日历：Years / Overrides / Source" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var value = JsonSerializer.Deserialize<CalendarData>(ReadJson(dialog.FileName)) ?? throw new ArgumentException("日历为空。"); value.Validate();
            foreach (var year in value.Years) { foreach (var key in calendar.Overrides.Keys.Where(k => k.StartsWith(year + "-", StringComparison.Ordinal)).ToList()) calendar.Overrides.Remove(key); }
            foreach (var entry in value.Overrides) calendar.Overrides[entry.Key] = entry.Value;
            calendar.Years = calendar.Years.Concat(value.Years).Distinct().OrderBy(x => x).ToList(); calendar.Source = value.Source;
            UpdateCalendarInfo(); SettingsFeedback.Text = "日历已载入，点击「保存偏好」生效。";
        }
        catch (Exception ex) { SettingsFeedback.Text = "导入失败：" + ex.Message; }
    }
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "DeskTip 配置 (*.json)|*.json", FileName = "DeskTip-config.json" }; if (dialog.ShowDialog() != true) return;
        try { File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(service.Settings, SettingsStore.Json)); SettingsFeedback.Text = "已导出当前生效配置。"; } catch (Exception ex) { SettingsFeedback.Text = ex.Message; }
    }
    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "DeskTip 配置 (*.json)|*.json" }; if (dialog.ShowDialog() != true) return;
        try { var value = JsonSerializer.Deserialize<AppSettings>(ReadJson(dialog.FileName)) ?? throw new ArgumentException("配置为空。"); value.Validate(); SettingsStore.Save(value); service.Apply(value); LoadEditors(); SettingsFeedback.Text = "配置已导入并生效 ✓"; } catch (Exception ex) { SettingsFeedback.Text = "导入失败：" + ex.Message; }
    }
    internal string VerifyDraftValidation()
    {
        int interval = service.Settings.IntervalMinutes; string originalInput = IntervalInput.Text;
        string originalName = periods[0].Name; string activeName = service.Settings.Periods[0].Name;
        string? before = File.Exists(SettingsStore.FilePath) ? File.ReadAllText(SettingsStore.FilePath) : null;
        periods[0].Name = "验证草稿隔离";
        if (service.Settings.Periods[0].Name != activeName) throw new Exception("草稿修改影响了生效配置。");
        IntervalInput.Text = "0"; SaveRules_Click(this, new());
        if (service.Settings.IntervalMinutes != interval || !RulesFeedback.Text.Contains("1–480")) throw new Exception("错误间隔未被拒绝。");
        string? after = File.Exists(SettingsStore.FilePath) ? File.ReadAllText(SettingsStore.FilePath) : null;
        if (before != after) throw new Exception("错误配置写入了磁盘。");
        periods[0].Name = originalName; IntervalInput.Text = originalInput; RulesFeedback.Text = "";
        return "PASS draft isolation; invalid UI input rejected; invalid input did not write settings; startup status readable=" + StartupService.IsEnabled();
    }
}
