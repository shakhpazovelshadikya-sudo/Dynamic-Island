using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace DynamicIsland;

public sealed record UpdateInfo(Version Version, string Tag, string DownloadUrl);

/// <summary>Проверка новой версии на GitHub (Releases) и тихая установка: скачивает установщик и запускает его.</summary>
public static class UpdateService
{
    private const string Repo = "shakhpazovelshadikya-sudo/Dynamic-Island";
    private static readonly HttpClient Http = CreateClient();

    /// <summary>Найденное обновление; null — установлена последняя версия (или ещё не проверяли).</summary>
    public static UpdateInfo? Available { get; private set; }

    /// <summary>Сработало ли хотя бы одно успешное обращение к GitHub.</summary>
    public static bool Checked { get; private set; }

    public static event Action? Changed;

    public static Version Current
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
        }
    }

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("DynamicIsland-Updater");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    /// <summary>Разбор ответа GitHub «latest release»: возвращает обновление, если оно новее <paramref name="current"/>.</summary>
    public static UpdateInfo? Parse(string json, Version current)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("tag_name", out var tagEl)) return null;
        string tag = tagEl.GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version) || version <= current) return null;
        if (!root.TryGetProperty("assets", out var assets)) return null;

        foreach (var a in assets.EnumerateArray())
        {
            string name = a.GetProperty("name").GetString() ?? "";
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && name.Contains("setap", StringComparison.OrdinalIgnoreCase))
                return new UpdateInfo(version, tag, a.GetProperty("browser_download_url").GetString() ?? "");
        }
        return null;
    }

    public static async Task CheckAsync()
    {
        try
        {
            string json = await Http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest");
            var found = Parse(json, Current);
            Checked = true;
            if (found?.Tag != Available?.Tag)
            {
                Available = found;
                Changed?.Invoke();
            }
        }
        catch (Exception ex) { AppLog.Error("UpdateService.CheckAsync", ex); }
    }

    /// <summary>Скачивает установщик и запускает его тихо. null — успех (приложению нужно закрыться), иначе текст ошибки.</summary>
    public static async Task<string?> InstallAsync()
    {
        var update = Available;
        if (update == null || update.DownloadUrl.Length == 0) return Loc.T("Нет доступных обновлений");
        try
        {
            string file = Path.Combine(Path.GetTempPath(), "DinamicIslandsetap-" + update.Tag + ".exe");
            using (var response = await Http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                await using var input = await response.Content.ReadAsStreamAsync();
                await using var output = File.Create(file);
                await input.CopyToAsync(output);
            }

            Process.Start(new ProcessStartInfo(file, "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS") { UseShellExecute = true });
            return null;
        }
        catch (Exception ex)
        {
            AppLog.Error("UpdateService.InstallAsync", ex);
            return Loc.T("Не удалось скачать обновление");
        }
    }
}
