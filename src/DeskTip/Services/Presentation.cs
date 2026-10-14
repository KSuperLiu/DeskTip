using DeskTip.Core;
namespace DeskTip.Services;

public static class IconArt
{
    private static readonly Dictionary<string, string> Shapes = new()
    {
        ["bell"] = "M 5,15 L 5,9 C 5,2 15,2 15,9 L 15,15 L 18,17 L 2,17 Z M 8,20 L 12,20",
        ["water"] = "M 10,1 C 8,6 3,10 3,14 C 3,23 17,23 17,14 C 17,10 12,6 10,1 Z M 6,14 C 6,17 8,18 10,18",
        ["move"] = "M 10,2 A 2,2 0 1 1 9.99,2 M 8,8 L 12,8 L 16,12 M 8,8 L 4,12 M 10,8 L 10,14 L 6,21 M 10,14 L 15,21",
        ["eye"] = "M 1,11 Q 10,-1 19,11 Q 10,23 1,11 Z M 13,11 A 3,3 0 1 1 7,11 A 3,3 0 1 1 13,11",
        ["focus"] = "M 5,1 L 1,1 L 1,5 M 15,1 L 19,1 L 19,5 M 1,17 L 1,21 L 5,21 M 19,17 L 19,21 L 15,21 M 15,11 A 5,5 0 1 1 5,11 A 5,5 0 1 1 15,11 M 10,8 L 10,11 L 13,13",
        ["list"] = "M 7,4 L 19,4 M 7,11 L 19,11 M 7,18 L 19,18 M 1,3 L 3,3 L 3,5 L 1,5 Z M 1,10 L 3,10 L 3,12 L 1,12 Z M 1,17 L 3,17 L 3,19 L 1,19 Z",
        ["cup"] = "M 2,7 L 15,7 L 15,15 C 15,21 2,21 2,15 Z M 15,9 L 18,9 C 23,9 23,16 15,16 M 4,2 L 4,4 M 9,1 L 9,4 M 14,2 L 14,4",
        ["check"] = "M 4,11 L 8,15 L 17,5 M 18,12 L 18,20 L 1,20 L 1,2 L 12,2",
        ["book"] = "M 10,5 Q 5,1 1,4 L 1,19 Q 6,16 10,20 Q 14,16 19,19 L 19,4 Q 14,1 10,5 Z M 10,5 L 10,20",
        ["folder"] = "M 1,5 L 7,5 L 10,8 L 20,8 L 20,20 L 1,20 Z M 1,9 L 20,9"
    };
    private static readonly Dictionary<string, Geometry> cache = new();
    public static Geometry Get(string key)
    {
        if (!Shapes.ContainsKey(key)) key = "bell";
        if (!cache.TryGetValue(key, out var shape)) { shape = Geometry.Parse(Shapes[key]); shape.Freeze(); cache[key] = shape; } return shape;
    }
    public static SolidColorBrush Brush(string hex) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }
    public static SolidColorBrush Tint(string hex) { var c = (Color)ColorConverter.ConvertFromString(hex); c.A = 27; var b = new SolidColorBrush(c); b.Freeze(); return b; }
}

public sealed class ReminderCard
{
    public ReminderDefinition Rule { get; }
    public string Id => Rule.Id;
    public string Name => Rule.Name;
    public string Message => Rule.Message;
    public string Category => Rule.Category;
    public bool Enabled => Rule.Enabled;
    public string Schedule => Rule.ScheduleLabel;
    public string Window => Rule.WindowLabel;
    public string Next { get; }
    public Geometry Icon => IconArt.Get(Rule.IconKey);
    public Brush Accent => IconArt.Brush(Rule.Accent);
    public Brush Tint => IconArt.Tint(Rule.Accent);
    public ReminderCard(ReminderDefinition rule, DateTime? next, DateTime now, bool paused, bool globallyEnabled)
    {
        Rule = rule;
        Next = !rule.Enabled ? "已停用" : !globallyEnabled ? "全局关闭" : paused ? "已暂停" : next == null ? rule.Mode == ScheduleMode.Once ? "已到期" : "暂无可用时刻" : next.Value.Date == now.Date ? "今天 " + next.Value.ToString("HH:mm") : next.Value.ToString("MM/dd HH:mm");
    }
}
public sealed class TemplateCard
{
    public ReminderTemplate Template { get; }
    public string Key => Template.Key;
    public string Name => Template.Name;
    public string Description => Template.Description;
    public string Category => Template.Category;
    public string Tag => Template.Tag;
    public Geometry Icon => IconArt.Get(Template.IconKey);
    public Brush Accent => IconArt.Brush(Template.Accent);
    public Brush Tint => IconArt.Tint(Template.Accent);
    public TemplateCard(ReminderTemplate template) { Template = template; }
}
