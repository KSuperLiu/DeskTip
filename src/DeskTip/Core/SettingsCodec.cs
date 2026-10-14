using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeskTip.Core;

public static class SettingsCodec
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public static AppSettings Clone(AppSettings value) => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(value, Json), Json)!;
    public static AppSettings Read(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("配置必须是 JSON 对象。");
        if (doc.RootElement.TryGetProperty("SchemaVersion", out var version))
        {
            if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != 2) throw new ArgumentException("配置版本不受支持。");
            var current = JsonSerializer.Deserialize<AppSettings>(json, Json) ?? throw new ArgumentException("配置为空。"); current.Validate(); return current;
        }
        var old = JsonSerializer.Deserialize<LegacySettings>(json, Json) ?? throw new ArgumentException("旧配置为空。");
        if (old.Reminders == null || old.Periods == null || old.Reminders.Any(r => r == null) || old.Periods.Any(p => p == null)) throw new ArgumentException("旧配置不完整。");
        var enabled = old.Reminders.Where(r => r.Enabled).ToArray();
        var result = new AppSettings { Enabled = old.Enabled, BeijingTime = old.BeijingTime, FullScreenQuiet = old.FullScreenQuiet, Calendar = old.Calendar ?? new(), Reminders = new() };
        if (enabled.Length > 0)
        {
            var profile = new ReminderDefinition { Name = "原有工作间歇", Message = enabled[0].Message, Category = "自定义", Enabled = true, TemplateKey = "migrated-v1", IntervalMinutes = old.IntervalMinutes, Periods = old.Periods, Days = old.ChinaCalendar ? DayPolicy.ChinaWorkdays : DayPolicy.Weekdays };
            profile.RotationMessages = enabled.Select(r => r.Title + " · " + r.Message).ToList(); result.Reminders.Add(profile);
        }
        foreach (var disabled in old.Reminders.Where(r => !r.Enabled))
            result.Reminders.Add(new() { Name = disabled.Title, Message = disabled.Message, Enabled = false, Category = "自定义", TemplateKey = "migrated-v1", IntervalMinutes = old.IntervalMinutes, Periods = old.Periods.Select(p => new WorkPeriod { Name = p.Name, Start = p.Start, End = p.End }).ToList(), Days = old.ChinaCalendar ? DayPolicy.ChinaWorkdays : DayPolicy.Weekdays });
        result.Validate(); return result;
    }
    private sealed class LegacySettings
    {
        public int IntervalMinutes { get; set; } = 50;
        public bool ChinaCalendar { get; set; } = true;
        public bool BeijingTime { get; set; } = true;
        public bool FullScreenQuiet { get; set; } = true;
        public bool Enabled { get; set; } = true;
        public List<WorkPeriod>? Periods { get; set; }
        public List<LegacyReminder>? Reminders { get; set; }
        public CalendarData? Calendar { get; set; }
    }
    private sealed class LegacyReminder { public bool Enabled { get; set; } = true; public string Title { get; set; } = ""; public string Message { get; set; } = ""; }
}
