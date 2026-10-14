using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DeskTip.Core;

public enum ScheduleMode { Interval, FixedTimes, Once }
public enum DayPolicy { ChinaWorkdays, Weekdays, EveryDay, SelectedDays }

public sealed class WorkPeriod
{
    public string Name { get; set; } = "工作时段";
    public string Start { get; set; } = "10:00";
    public string End { get; set; } = "11:45";
    [JsonIgnore] public TimeSpan StartTime => Clock.Parse(Start);
    [JsonIgnore] public TimeSpan EndTime => End == "24:00" ? TimeSpan.FromDays(1) : Clock.Parse(End);
}

public static class Clock
{
    public static TimeSpan Parse(string time) => TimeSpan.ParseExact(time, @"hh\:mm", CultureInfo.InvariantCulture);
    public static bool Valid(string? time) => time != null && TimeSpan.TryParseExact(time, @"hh\:mm", CultureInfo.InvariantCulture, out var value) && value >= TimeSpan.Zero && value < TimeSpan.FromDays(1);
    public static bool ValidEnd(string? time) => time == "24:00" || Valid(time);
    public static bool ValidDate(string? date) => DateTime.TryParseExact(date, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}

public sealed class ReminderDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "自定义提醒";
    public string Message { get; set; } = "这件事现在可以开始了。";
    public string Category { get; set; } = "自定义";
    public string IconKey { get; set; } = "bell";
    public string Accent { get; set; } = "#A99EFF";
    public string TemplateKey { get; set; } = "custom";
    public bool Enabled { get; set; } = true;
    public bool ShowCompanion { get; set; } = true;
    public bool QuietDuringFullscreen { get; set; } = true;
    public int SnoozeMinutes { get; set; } = 5;
    public ScheduleMode Mode { get; set; } = ScheduleMode.Interval;
    public DayPolicy Days { get; set; } = DayPolicy.EveryDay;
    public List<DayOfWeek> SelectedDays { get; set; } = new() { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
    public int IntervalMinutes { get; set; } = 50;
    public List<WorkPeriod> Periods { get; set; } = new() { new() { Name = "白天", Start = "09:00", End = "18:00" } };
    public List<string> Times { get; set; } = new() { "09:50" };
    public string OnceAt { get; set; } = "";
    public List<string> RotationMessages { get; set; } = new();
    [JsonIgnore] public DateTime OnceTime => DateTime.ParseExact(OnceAt, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    [JsonIgnore] public string DayLabel => Days switch { DayPolicy.ChinaWorkdays => "中国工作日", DayPolicy.Weekdays => "周一至周五", DayPolicy.EveryDay => "每天", _ => string.Join("、", SelectedDays.OrderBy(d => ((int)d + 6) % 7).Select(d => "周" + "日一二三四五六"[(int)d])) };
    [JsonIgnore] public string ScheduleLabel => Mode switch { ScheduleMode.Interval => $"每 {IntervalMinutes} 分钟 · {DayLabel}", ScheduleMode.FixedTimes => string.Join(" / ", Times.OrderBy(t => t)) + " · " + DayLabel, _ => OnceAt + " · 一次性" };
    [JsonIgnore] public string WindowLabel => Mode == ScheduleMode.Interval ? string.Join("  /  ", Periods.Select(p => p.Start + "–" + p.End)) : Mode == ScheduleMode.Once ? "到期提醒一次" : "在设定时刻提醒";
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || Id.Length > 80) throw new ArgumentException("提醒标识无效。");
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 40) throw new ArgumentException("提醒名称需为 1–40 个字。");
        if (string.IsNullOrWhiteSpace(Message) || Message.Length > 240) throw new ArgumentException("提醒内容需为 1–240 个字。");
        if (string.IsNullOrWhiteSpace(Category) || Category.Length > 12) throw new ArgumentException("分类需为 1–12 个字。");
        if (IconKey == null || IconKey.Length > 30 || TemplateKey == null || TemplateKey.Length > 80 || Accent == null || !Regex.IsMatch(Accent, "^#[0-9a-fA-F]{6}$")) throw new ArgumentException("提醒样式无效。");
        if (!Enum.IsDefined(typeof(ScheduleMode), Mode) || !Enum.IsDefined(typeof(DayPolicy), Days)) throw new ArgumentException("提醒时间类型无效。");
        if (SnoozeMinutes < 1 || SnoozeMinutes > 60) throw new ArgumentException("稍后提醒需为 1–60 分钟。");
        if (SelectedDays == null || SelectedDays.Count > 7 || SelectedDays.Distinct().Count() != SelectedDays.Count || SelectedDays.Any(d => !Enum.IsDefined(typeof(DayOfWeek), d))) throw new ArgumentException("星期选项无效。");
        if (Mode != ScheduleMode.Once && Days == DayPolicy.SelectedDays && SelectedDays.Count == 0) throw new ArgumentException("请至少选择一天。");
        if (Periods == null || Periods.Count > 12 || Times == null || Times.Count > 24 || RotationMessages == null || RotationMessages.Count > 20 || RotationMessages.Any(m => string.IsNullOrWhiteSpace(m) || m.Length > 240)) throw new ArgumentException("时段、时刻或轮换文案格式无效。");
        if (Mode == ScheduleMode.Interval)
        {
            if (IntervalMinutes < 1 || IntervalMinutes > 480) throw new ArgumentException("提醒间隔需为 1–480 分钟。");
            if (Periods.Count == 0 || Periods.Count > 12) throw new ArgumentException("请设置 1–12 个有效时段。");
            foreach (var p in Periods)
                if (p == null || string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 20 || !Clock.Valid(p.Start) || !Clock.ValidEnd(p.End) || p.EndTime <= p.StartTime)
                    throw new ArgumentException("时间需为 HH:mm，结束必须晚于开始；跨午夜请拆分时段。");
            var sorted = Periods.OrderBy(p => p.StartTime).ToArray();
            for (int i = 1; i < sorted.Length; i++) if (sorted[i].StartTime < sorted[i - 1].EndTime) throw new ArgumentException("同一提醒的时段不能重叠。");
        }
        if (Mode == ScheduleMode.FixedTimes && (Times.Count == 0 || Times.Count > 24 || Times.Any(t => !Clock.Valid(t)) || Times.Distinct().Count() != Times.Count)) throw new ArgumentException("请设置 1–24 个不重复的 HH:mm 时刻。");
        if (Mode == ScheduleMode.Once && !Clock.ValidDate(OnceAt)) throw new ArgumentException("一次性日期需为 yyyy-MM-dd HH:mm。");
    }
}

public sealed class CalendarData
{
    public List<int> Years { get; set; } = new();
    public Dictionary<string, bool> Overrides { get; set; } = new();
    public string Source { get; set; } = "";
    public bool IsWorkday(DateTime day)
    {
        if (Overrides.TryGetValue(day.ToString("yyyy-MM-dd"), out var work)) return work;
        return day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday;
    }
    public void Validate()
    {
        if (Years == null || Overrides == null || Years.Count > 100 || Overrides.Count > 10000 || Years.Any(y => y < 1900 || y > 9998)) throw new ArgumentException("日历数据无效或过大。");
        foreach (var key in Overrides.Keys)
            if (!DateTime.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) throw new ArgumentException("日历日期必须使用 yyyy-MM-dd。");
    }
}

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 2;
    public ThemePreference Theme { get; set; } = ThemePreference.Dark;
    public bool BeijingTime { get; set; } = true;
    public bool FullScreenQuiet { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public List<ReminderDefinition> Reminders { get; set; } = new() { TemplateCatalog.Create("water") };
    public CalendarData Calendar { get; set; } = new();
    public void Validate()
    {
        if (SchemaVersion != 2) throw new ArgumentException("此版本暂不支持该配置格式。");
        if (!Enum.IsDefined(typeof(ThemePreference), Theme)) throw new ArgumentException("主题选项无效。");
        if (Reminders == null || Reminders.Count > 60) throw new ArgumentException("最多支持 60 条独立提醒。");
        if (Reminders.Any(r => r == null) || Reminders.Select(r => r.Id).Distinct().Count() != Reminders.Count) throw new ArgumentException("提醒标识不能重复。");
        foreach (var rule in Reminders) rule.Validate();
        if (Calendar == null) throw new ArgumentException("缺少日历数据。"); Calendar.Validate();
    }
}

public sealed record ReminderOccurrence(ReminderDefinition Reminder, DateTime Due, string Message);
