using DeskTip.Core;
using DeskTip.Services;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace DeskTip;

public partial class ReminderEditorWindow : Window
{
    private readonly ReminderDefinition draft;
    private readonly ReminderService service;
    private readonly ObservableCollection<WorkPeriod> periods;
    private readonly Dictionary<DayOfWeek, CheckBox> dayInputs = new();
    private readonly (string Key, string Label)[] icons = { ("bell", "通用提醒"), ("water", "饮水"), ("move", "活动"), ("eye", "护眼"), ("focus", "专注"), ("list", "计划"), ("cup", "用餐"), ("check", "完成"), ("book", "阅读"), ("folder", "文件") };
    private bool loaded;
    public ReminderDefinition? Result { get; private set; }
    public bool DeleteRequested { get; private set; }
    public ReminderEditorWindow(ReminderDefinition rule, ReminderService service, bool editing)
    {
        this.service = service; draft = JsonSerializer.Deserialize<ReminderDefinition>(JsonSerializer.Serialize(rule, SettingsCodec.Json), SettingsCodec.Json)!;
        periods = new(draft.Periods); InitializeComponent();
        Height = Math.Min(790, SystemParameters.WorkArea.Height - 30);
        EditorTitle.Text = editing ? "编辑提醒" : "创建自己的提醒";
        OriginLabel.Text = draft.TemplateKey == "custom" ? "完全自定义 · 按自己的方式安排" : draft.TemplateKey == "migrated-v1" ? "旧版规则已保留 · 可以自由调整" : "来自模板 · " + (TemplateCatalog.All.FirstOrDefault(t => t.Key == draft.TemplateKey)?.Name ?? "自定义");
        DeleteButton.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        NameInput.Text = draft.Name; MessageInput.Text = draft.Message; CategoryInput.Text = draft.Category;
        foreach (var icon in icons) IconInput.Items.Add(icon.Label);
        IconInput.SelectedIndex = Math.Max(0, Array.FindIndex(icons, i => i.Key == draft.IconKey));
        ModeInput.SelectedIndex = (int)draft.Mode; DaysInput.SelectedIndex = (int)draft.Days;
        IntervalInput.Text = draft.IntervalMinutes.ToString(); TimesInput.Text = string.Join(", ", draft.Times);
        var once = Clock.ValidDate(draft.OnceAt) ? draft.OnceTime : service.Now.Date.AddDays(1).AddHours(10);
        OnceDateInput.Text = once.ToString("yyyy-MM-dd"); OnceTimeInput.Text = once.ToString("HH:mm");
        EnabledInput.IsChecked = draft.Enabled; CompanionInput.IsChecked = draft.ShowCompanion; QuietInput.IsChecked = draft.QuietDuringFullscreen; SnoozeInput.Text = draft.SnoozeMinutes.ToString();
        RotationInput.Text = string.Join(Environment.NewLine, draft.RotationMessages); PeriodsGrid.ItemsSource = periods;
        for (int i = 0; i < 7; i++) { var day = (DayOfWeek)((i + 1) % 7); var input = new CheckBox { Content = "周" + "日一二三四五六"[(int)day], IsChecked = draft.SelectedDays.Contains(day), Margin = new Thickness(0, 8, 15, 4) }; dayInputs[day] = input; WeekDaysPanel.Children.Add(input); }
        loaded = true; UpdateModes(); UpdatePreview();
        IconInput.SelectionChanged += (_, _) => UpdatePreview();
    }
    private void UpdateModes()
    {
        IntervalPanel.Visibility = ModeInput.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        FixedPanel.Visibility = ModeInput.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        OncePanel.Visibility = ModeInput.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        DaysPanel.Visibility = ModeInput.SelectedIndex == 2 ? Visibility.Collapsed : Visibility.Visible;
        WeekDaysPanel.Visibility = DaysInput.SelectedIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void Mode_Changed(object sender, SelectionChangedEventArgs e) { if (loaded) UpdateModes(); }
    private void Days_Changed(object sender, SelectionChangedEventArgs e) { if (loaded) UpdateModes(); }
    private void Content_Changed(object sender, TextChangedEventArgs e) { if (loaded) UpdatePreview(); }
    private void UpdatePreview()
    {
        PreviewName.Text = string.IsNullOrWhiteSpace(NameInput.Text) ? "你的提醒名称" : NameInput.Text;
        PreviewMessage.Text = string.IsNullOrWhiteSpace(MessageInput.Text) ? "写下到时间后希望看到的内容。" : MessageInput.Text;
        PreviewIcon.Data = IconArt.Get(icons[Math.Max(0, IconInput.SelectedIndex)].Key); PreviewIcon.Stroke = IconArt.Brush(draft.Accent); PreviewIconTile.Background = IconArt.Tint(draft.Accent);
    }
    private ReminderDefinition Collect()
    {
        PeriodsGrid.CommitEdit(DataGridEditingUnit.Cell, true); PeriodsGrid.CommitEdit(DataGridEditingUnit.Row, true);
        var value = JsonSerializer.Deserialize<ReminderDefinition>(JsonSerializer.Serialize(draft, SettingsCodec.Json), SettingsCodec.Json)!;
        value.Name = NameInput.Text.Trim(); value.Message = MessageInput.Text.Trim(); value.Category = CategoryInput.Text.Trim(); value.IconKey = icons[Math.Max(0, IconInput.SelectedIndex)].Key;
        value.Mode = (ScheduleMode)ModeInput.SelectedIndex; value.Days = (DayPolicy)DaysInput.SelectedIndex;
        if (value.Mode == ScheduleMode.Interval && !int.TryParse(IntervalInput.Text, out _)) throw new ArgumentException("间隔请填写整数。");
        if (int.TryParse(IntervalInput.Text, out var interval)) value.IntervalMinutes = interval;
        if (!int.TryParse(SnoozeInput.Text, out var snooze)) throw new ArgumentException("稍后分钟数请填写整数。"); value.SnoozeMinutes = snooze;
        value.Periods = periods.ToList(); value.Times = Regex.Split(TimesInput.Text.Trim(), "[,，;；\\s]+").Where(t => t.Length > 0).ToList();
        value.OnceAt = OnceDateInput.Text.Trim() + " " + OnceTimeInput.Text.Trim(); value.SelectedDays = dayInputs.Where(d => d.Value.IsChecked == true).Select(d => d.Key).ToList();
        value.Enabled = EnabledInput.IsChecked == true; value.ShowCompanion = CompanionInput.IsChecked == true; value.QuietDuringFullscreen = QuietInput.IsChecked == true;
        value.RotationMessages = RotationInput.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
        value.Validate(); return value;
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try { var value = Collect(); if (value.Mode == ScheduleMode.Once && value.OnceTime <= service.Now && value.Enabled) throw new ArgumentException("请为已开启的一次性提醒选择未来时刻。"); Result = value; Close(); } catch (Exception ex) { Feedback.Text = ex.Message; }
    }
    private void Preview_Click(object sender, RoutedEventArgs e) { try { service.Preview(Collect()); Feedback.Text = "预览不会创建或改变正式提醒。"; } catch (Exception ex) { Feedback.Text = ex.Message; } }
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    private void Delete_Click(object sender, RoutedEventArgs e) { if (MessageBox.Show("删除「" + draft.Name + "」？", "删除提醒", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) { DeleteRequested = true; Close(); } }
    private void Title_MouseDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) DragMove(); }
    private void AddPeriod_Click(object sender, RoutedEventArgs e) { if (periods.Count < 12) periods.Add(new() { Name = "新时段", Start = "19:00", End = "22:00" }); }
    private void RemovePeriod_Click(object sender, RoutedEventArgs e) { if (PeriodsGrid.SelectedItem is WorkPeriod period) periods.Remove(period); }
    internal string VerifyDraftValidation()
    {
        string original = IntervalInput.Text; IntervalInput.Text = "0";
        try { Collect(); throw new Exception("未拒绝无效间隔。"); } catch (ArgumentException) { }
        IntervalInput.Text = original;
        int originalMode = ModeInput.SelectedIndex; string originalTimes = TimesInput.Text;
        ModeInput.SelectedIndex = 1; TimesInput.Text = "25:00";
        try { Collect(); throw new Exception("未拒绝错误固定时刻。"); } catch (ArgumentException) { }
        TimesInput.Text = "09:30, 18:20"; if (Collect().Times.Count != 2) throw new Exception("多个固定时刻解析失败。");
        ModeInput.SelectedIndex = 2; string originalDate = OnceDateInput.Text; OnceDateInput.Text = "2026-99-99";
        try { Collect(); throw new Exception("未拒绝错误一次性日期。"); } catch (ArgumentException) { }
        OnceDateInput.Text = originalDate; if (Collect().Mode != ScheduleMode.Once) throw new Exception("一次性模式未生效。");
        ModeInput.SelectedIndex = originalMode; TimesInput.Text = originalTimes;
        var value = Collect(); if (value.Id != draft.Id) throw new Exception("编辑丢失规则标识。");
        value.Name = "隔离检查"; if (service.Settings.Reminders.Any(r => r.Name == "隔离检查")) throw new Exception("草稿泄漏。");
        return "PASS interval/fixed/oneoff editor validation; fixed-time parsing; draft isolation";
    }
}
