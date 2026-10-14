using System.Text.Json;
using DeskTip.Core;
namespace DeskTip.Services;
public static class SettingsStore
{
    public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskTip");
    public static readonly string FilePath = Path.Combine(Folder, "settings-v2.json");
    private static readonly string LegacyPath = Path.Combine(Folder, "settings.json");
    public static readonly JsonSerializerOptions Json = SettingsCodec.Json;
    public static string? LoadWarning { get; private set; }
    public static AppSettings Load()
    {
        var loadPath = File.Exists(FilePath) ? FilePath : LegacyPath;
        if (File.Exists(loadPath))
        {
            try
            {
                if (new FileInfo(loadPath).Length > 3_000_000) throw new ArgumentException("配置过大。");
                var text = File.ReadAllText(loadPath); var value = SettingsCodec.Read(text);
                using var document = JsonDocument.Parse(text);
                if (!document.RootElement.TryGetProperty("SchemaVersion", out _)) LoadWarning = "旧版规则已迁移，原工作时段与文案已保留。v2 单独保存配置，原文件保留。";
                return value;
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or IOException or UnauthorizedAccessException or InvalidDataException)
            { LoadWarning = "原设置无法读取，已使用默认规则。原文件保留；保存前会备份。"; }
        }
        var result = new AppSettings(); var path = Path.Combine(AppContext.BaseDirectory, "Assets", "calendar-2026.json");
        try { if (File.Exists(path)) { result.Calendar = JsonSerializer.Deserialize<CalendarData>(File.ReadAllText(path)) ?? new(); result.Calendar.Validate(); } }
        catch (Exception ex) when (ex is JsonException or ArgumentException or IOException or UnauthorizedAccessException)
        { result.Calendar = new(); LoadWarning = "内置工作日历不可读，请导入新日历；暂按周一至周五运行。"; }
        result.Validate(); return result;
    }
    public static AppSettings Clone(AppSettings value) => SettingsCodec.Clone(value);
    public static void Save(AppSettings value)
    {
        value.Validate(); Directory.CreateDirectory(Folder);
        var temp = FilePath + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(value, Json));
        if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak"); else File.Move(temp, FilePath);
        LoadWarning = null;
    }
}
