using System;
using System.Collections.Generic;
using System.Linq;

namespace DeskTip.Core;

public sealed record ReminderTemplate(string Key, string Name, string Description, string Category, string IconKey, string Accent, string Tag, Func<ReminderDefinition> Factory);
public static class TemplateCatalog
{
    private static List<WorkPeriod> WorkHours() => new() { new() { Name = "上午", Start = "10:00", End = "11:45" }, new() { Name = "下午", Start = "13:50", End = "18:30" } };
    private static ReminderDefinition Interval(string name, string message, int minutes) => new() { Name = name, Message = message, Mode = ScheduleMode.Interval, Days = DayPolicy.ChinaWorkdays, IntervalMinutes = minutes, Periods = WorkHours() };
    private static ReminderDefinition Fixed(string name, string message, string time, DayPolicy days = DayPolicy.ChinaWorkdays) => new() { Name = name, Message = message, Mode = ScheduleMode.FixedTimes, Times = new() { time }, Days = days };
    public static IReadOnlyList<ReminderTemplate> All { get; } = new ReminderTemplate[]
    {
        new("water", "饮水补给", "给忙碌留一点补水时间。", "健康", "water", "#75D6F3", "50 分钟 / 工作日", () => Interval("饮水补给", "去接一杯水，给自己一个短暂的休息。", 50)),
        new("move", "起身活动", "离开座位，让身体换个姿势。", "健康", "move", "#A9E39B", "60 分钟 / 工作日", () => Interval("起身活动", "起来走走，活动一下，或者去趟洗手间。", 60)),
        new("eyes", "眼睛放松", "从屏幕前抬头，看看远处。", "健康", "eye", "#9FBBFF", "20 分钟 / 工作日", () => Interval("眼睛放松", "暂时移开视线，看看窗外或远处。", 20)),
        new("focus", "专注间歇", "为一段专注，安排一次暂停。", "工作", "focus", "#B7A0FF", "50 分钟 / 工作日", () => Interval("专注间歇", "结束这一段专注，给自己几分钟休息。", 50)),
        new("plan", "晨间规划", "整理待办，选出今天最重要的事。", "工作", "list", "#F0C685", "09:50 / 工作日", () => Fixed("晨间规划", "花几分钟规划今天，确认优先事项。", "09:50")),
        new("lunch", "午餐时间", "按自己的安排，准时享用午餐。", "生活", "cup", "#F3AE8D", "11:45 / 工作日", () => Fixed("午餐时间", "到了午餐时间，先把手边的工作告一段落吧。", "11:45")),
        new("wrap", "工作收尾", "回顾进度，把未完成的事留给明天。", "工作", "check", "#8BD8C1", "18:20 / 工作日", () => Fixed("工作收尾", "整理今天的进度，保存文件，写下明天的第一件事。", "18:20")),
        new("read", "阅读学习", "为一本书或一项技能，留一段时间。", "生活", "book", "#CCAAF2", "21:00 / 每天", () => Fixed("阅读学习", "打开书或学习资料，开始今天的阅读时间。", "21:00", DayPolicy.EveryDay)),
        new("exercise", "运动时间", "给运动安排一个固定的位置。", "健康", "move", "#BEE09B", "19:30 / 每天", () => Fixed("运动时间", "按照自己的计划，开始今天的运动吧。", "19:30", DayPolicy.EveryDay)),
        new("backup", "文件备份", "在一周结束前，整理并备份重要文件。", "工作", "folder", "#88BDFF", "17:45 / 每周五", () => { var r = Fixed("文件备份", "保存并备份重要文件，确认备份已经完成。", "17:45", DayPolicy.SelectedDays); r.SelectedDays = new() { DayOfWeek.Friday }; return r; }),
        new("once", "一次性待办", "为会议、取件或其他事情指定一个时刻。", "生活", "bell", "#F2CC8F", "指定日期 / 一次", () => new() { Name = "一次性待办", Message = "这件事到约定时间了。", Mode = ScheduleMode.Once, OnceAt = DateTime.Today.AddDays(1).AddHours(10).ToString("yyyy-MM-dd HH:mm") })
    };
    public static ReminderDefinition Create(string key)
    {
        var template = All.FirstOrDefault(t => t.Key == key);
        if (template == null) return new ReminderDefinition();
        var r = template.Factory(); r.TemplateKey = key; r.Category = template.Category; r.IconKey = template.IconKey; r.Accent = template.Accent; return r;
    }
}
