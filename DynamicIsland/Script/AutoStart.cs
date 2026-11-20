using System.IO;
using Microsoft.Win32;

namespace DynamicIsland;

/// <summary>Автозапуск через HKCU\...\Run (без прав администратора).</summary>
public static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppKey = @"Software\DynamicIsland";
    private const string ValueName = "DynamicIsland";

    /// <summary>Автозапуск доступен только для собранного exe (при «dotnet run» процесс — dotnet.exe).</summary>
    public static bool IsAvailable => GetExePath() != null;

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) != null;
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true) ?? Registry.CurrentUser.CreateSubKey(RunKey);

        if (enabled)
        {
            var exe = GetExePath();
            if (exe != null) key.SetValue(ValueName, $"\"{exe}\"");
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }
    }

    /// <summary>
    /// При первом запуске включает автозапуск; дальше уважает выбор пользователя из меню трея.
    /// Если автозапуск включён — обновляет путь (на случай, если exe перенесли).
    /// </summary>
    public static void Init()
    {
        try
        {
            using var app = Registry.CurrentUser.CreateSubKey(AppKey);
            if (app.GetValue("FirstRunDone") == null)
            {
                Set(true);
                app.SetValue("FirstRunDone", 1);
            }
            else if (IsEnabled())
            {
                Set(true);
            }
        }
        catch (Exception ex) { AppLog.Error("AutoStart.Init", ex); }
    }

    private static string? GetExePath()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return null;

        // при запуске через "dotnet run" процесс — это dotnet.exe, его в автозапуск класть нельзя
        if (string.Equals(Path.GetFileNameWithoutExtension(exe), "dotnet", StringComparison.OrdinalIgnoreCase))
            return null;

        return exe;
    }
}
