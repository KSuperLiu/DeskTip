using System.Text.Json;
using DeskTip.Core;
namespace DeskTip.Services;
public static class SettingsStore
{
    public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskTip");
    public static readonly string FilePath = Path.Combine(Folder, "settings.json");
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static string? LoadWarning { get; private set; }
    public static AppSettings Load()
    {
        if (File.Exists(FilePath))
        {
            try { var value = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? throw new InvalidDataException("设置为空"); value.Validate(); return value; }
            catch (Exception ex) when (ex is JsonException or ArgumentException or IOException or UnauthorizedAccessException or InvalidDataException)
            { LoadWarning = "原设置无法读取，已使用默认规则。原文件保留；保存前会备份。"; }
        }
        var result = new AppSettings(); var path = Path.Combine(AppContext.BaseDirectory, "Assets", "calendar-2026.json");
        if (File.Exists(path)) result.Calendar = JsonSerializer.Deserialize<CalendarData>(File.ReadAllText(path)) ?? new();
        result.Validate(); return result;
    }
    public static AppSettings Clone(AppSettings value) => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(value))!;
    public static void Save(AppSettings value)
    {
        value.Validate(); Directory.CreateDirectory(Folder);
        var temp = FilePath + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(value, Json));
        if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak"); else File.Move(temp, FilePath);
        LoadWarning = null;
    }
}
