using System.IO;
using System.Diagnostics;
using System.Text;

namespace DynamicIsland;

/// <summary>
/// Простой журнал: %AppData%\DynamicIsland\log.txt. Когда файл вырастает больше 512 КБ,
/// старый уходит в log.old.txt. Одинаковые ошибки чаще раза в минуту не пишутся (циклы опроса).
/// </summary>
public static class AppLog
{
    private const long MaxBytes = 512 * 1024;
    private static readonly object Gate = new();
    private static readonly Dictionary<string, DateTime> Recent = new();

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DynamicIsland", "log.txt");

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string where, Exception ex)
    {
        string key = $"{where}|{ex.GetType().FullName}|{ex.Message}";
        lock (Gate)
        {
            if (Recent.TryGetValue(key, out var last) && DateTime.UtcNow - last < TimeSpan.FromMinutes(1)) return;
            Recent[key] = DateTime.UtcNow;
            if (Recent.Count > 200) Recent.Clear();
        }

        Write("ERROR", $"{where}: {ex}");
    }

    /// <summary>Открывает журнал в программе по умолчанию (Блокнот).</summary>
    public static void Open()
    {
        try
        {
            if (!File.Exists(FilePath)) Info("Журнал создан");
            Process.Start(new ProcessStartInfo(FilePath) { UseShellExecute = true });
        }
        catch { }
    }

    private static void Write(string level, string text)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

                var fi = new FileInfo(FilePath);
                if (fi.Exists && fi.Length > MaxBytes)
                    File.Move(FilePath, Path.Combine(fi.DirectoryName!, "log.old.txt"), true);

                File.AppendAllText(FilePath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {text}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch { } // сам журнал не должен ронять приложение
    }
}
