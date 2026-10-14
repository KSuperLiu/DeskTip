using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;

namespace DeskTip.Core;

public sealed class WorkPeriod
{
    public string Name { get; set; } = "工作时段";
    public string Start { get; set; } = "10:00";
    public string End { get; set; } = "11:45";
    [JsonIgnore] public TimeSpan StartTime => TimeSpan.ParseExact(Start, @"hh\:mm", CultureInfo.InvariantCulture);
    [JsonIgnore] public TimeSpan EndTime => TimeSpan.ParseExact(End, @"hh\:mm", CultureInfo.InvariantCulture);
}

public sealed class ReminderRule
{
    public bool Enabled { get; set; } = true;
    public string Title { get; set; } = "去接一杯水吧";
    public string Message { get; set; } = "起身走走，给自己补充一点水分。";
}

public sealed class CalendarData
{
    public List<int> Years { get; set; } = new();
    public Dictionary<string, bool> Overrides { get; set; } = new();
    public string Source { get; set; } = "";
    public bool IsWorkday(DateTime day, bool chinese)
    {
        if (chinese && Overrides.TryGetValue(day.ToString("yyyy-MM-dd"), out var work)) return work;
        return day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday;
    }
    public void Validate()
    {
        if (Years == null || Overrides == null || Years.Count > 100 || Overrides.Count > 10000)
            throw new ArgumentException("日历数据格式无效或过大。");
        foreach (var key in Overrides.Keys)
            if (!DateTime.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                throw new ArgumentException("日历日期必须使用 yyyy-MM-dd 格式。");
        if (Years.Any(y => y < 1900 || y > 9998)) throw new ArgumentException("日历年份需为 1900–9998。");
    }
}

public sealed class AppSettings
{
    public int IntervalMinutes { get; set; } = 50;
    public bool ChinaCalendar { get; set; } = true;
    public bool BeijingTime { get; set; } = true;
    public bool FullScreenQuiet { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public List<WorkPeriod> Periods { get; set; } = new()
    {
        new() { Name = "上午", Start = "10:00", End = "11:45" },
        new() { Name = "下午", Start = "13:50", End = "18:30" }
    };
    public List<ReminderRule> Reminders { get; set; } = new()
    {
        new() { Title = "去接一杯水吧", Message = "让眼睛休息一下，顺便给自己补充一点水分。" },
        new() { Title = "起来走一走吧", Message = "去趟洗手间，或者离开座位活动一下。" },
        new() { Title = "给自己一点休息", Message = "放下手头的事，舒展肩颈，看看远处。" }
    };
    public CalendarData Calendar { get; set; } = new();

    public void Validate()
    {
        if (IntervalMinutes < 1 || IntervalMinutes > 480) throw new ArgumentException("提醒间隔需为 1–480 分钟。");
        if (Periods == null || Periods.Count == 0 || Periods.Count > 12) throw new ArgumentException("请保留 1–12 个工作时段。");
        foreach (var p in Periods)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 20) throw new ArgumentException("时段名称需为 1–20 个字。");
            if (!TimeSpan.TryParseExact(p.Start, @"hh\:mm", CultureInfo.InvariantCulture, out var start) ||
                !TimeSpan.TryParseExact(p.End, @"hh\:mm", CultureInfo.InvariantCulture, out var end) ||
                start < TimeSpan.Zero || end >= TimeSpan.FromDays(1) || end <= start)
                throw new ArgumentException("时间请填写 HH:mm，结束必须晚于开始；跨午夜请拆分为两个时段。");
        }
        var sorted = Periods.OrderBy(p => p.StartTime).ToList();
        for (int i = 1; i < sorted.Count; i++)
            if (sorted[i].StartTime < sorted[i - 1].EndTime) throw new ArgumentException("工作时段不能重叠。");
        if (Reminders == null || Reminders.Count == 0 || Reminders.Count > 20 || !Reminders.Any(r => r != null && r.Enabled))
            throw new ArgumentException("请至少开启一条提醒，最多支持 20 条。");
        foreach (var r in Reminders)
            if (r == null || string.IsNullOrWhiteSpace(r.Title) || r.Title.Length > 40 || string.IsNullOrWhiteSpace(r.Message) || r.Message.Length > 150)
                throw new ArgumentException("提醒标题需为 1–40 个字，文案需为 1–150 个字。");
        if (Calendar == null) throw new ArgumentException("缺少日历数据。");
        Calendar.Validate();
    }
}
