using System;

namespace DeskTip.Core;

public enum ThemePreference { Dark, Light, System }

public static class ThemeRules
{
    public static ThemePreference Resolve(ThemePreference preference, bool systemLight) => preference switch
    {
        ThemePreference.Dark => ThemePreference.Dark,
        ThemePreference.Light => ThemePreference.Light,
        ThemePreference.System => systemLight ? ThemePreference.Light : ThemePreference.Dark,
        _ => throw new ArgumentException("主题选项无效。")
    };
    public static string Accent(string hex, ThemePreference resolved)
    {
        if (resolved == ThemePreference.Dark) return hex;
        int r = Convert.ToInt32(hex.Substring(1, 2), 16), g = Convert.ToInt32(hex.Substring(3, 2), 16), b = Convert.ToInt32(hex.Substring(5, 2), 16);
        return $"#{(int)(r * 0.54):X2}{(int)(g * 0.54):X2}{(int)(b * 0.54):X2}";
    }
}
