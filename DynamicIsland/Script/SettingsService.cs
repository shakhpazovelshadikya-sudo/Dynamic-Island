using System.IO;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DynamicIsland;

public class AppSettings
{
    /// <summary>Язык интерфейса: «ru», «en» или пусто — как в Windows.</summary>
    public string Language { get; set; } = "";

    /// <summary>Отступ острова от верхнего края экрана, px.</summary>
    public double TopMargin { get; set; } = 6;

    /// <summary>Раскрывать остров на 3 секунды при смене трека.</summary>
    public bool ExpandOnTrackChange { get; set; } = true;

    /// <summary>Показывать уведомления других приложений под островом.</summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>Анимация «EN → RU» при смене раскладки клавиатуры.</summary>
    public bool AnimateLanguage { get; set; } = true;

    /// <summary>Прятать остров, пока на основном мониторе открыто полноэкранное окно (игра, видео).</summary>
    public bool HideInFullscreen { get; set; } = false;

    /// <summary>Хранить историю буфера обмена (только текст, в памяти) для вкладки «Буфер».</summary>
    public bool ClipboardHistory { get; set; } = true;

    /// <summary>VPN-профиль Windows, которым управляет плитка VPN на вкладке «Система». Пусто — первый найденный.</summary>
    public string VpnProfile { get; set; } = "";

    /// <summary>Имя монитора (Screen.DeviceName), на котором живёт остров. Пусто — основной монитор.</summary>
    public string MonitorDevice { get; set; } = "";

    /// <summary>Питомец в капсуле: id из <see cref="PetSprites"/> («cat», «slime», «bunny») или пусто — без питомца.</summary>
    public string Pet { get; set; } = "cat";

    /// <summary>Максимальная ширина капсулы, px: длинное название трека растягивает её до этого предела.</summary>
    public double CompactMaxWidth { get; set; } = 380;

    /// <summary>Максимальная ширина раскрытого острова, px.</summary>
    public double ExpandedMaxWidth { get; set; } = 640;

    /// <summary>Видимые вкладки острова в порядке показа (id из <see cref="TabCatalog"/>). Нет в списке — вкладка скрыта.</summary>
    /// <summary>Языки переводчика: с какого («auto» — определять) и на какой.</summary>
    public string TranslateFrom { get; set; } = "auto";
    public string TranslateTo { get; set; } = "en";

    public List<string> Tabs { get; set; } = TabCatalog.All.Select(t => t.Id).ToList();
}

/// <summary>Все вкладки острова. Порядок в <see cref="All"/> — внутренние номера (0…7), на них завязан код MainWindow.</summary>
public static class TabCatalog
{
    public static readonly (string Id, string Title)[] All =
    {
        ("main", "Главная"), ("weather", "Погода"), ("calendar", "Календарь"), ("shelf", "Полка"),
        ("timer", "Таймер"), ("clipboard", "Буфер обмена"), ("system", "Система"), ("translate", "Переводчик")
    };

    public static int IndexOf(string id) => Array.FindIndex(All, t => t.Id == id);

    /// <summary>Чистит список из настроек: убирает неизвестные и повторы, «Главная» есть всегда. null — все вкладки.</summary>
    public static List<string> Normalize(IEnumerable<string>? ids)
    {
        if (ids == null) return All.Select(t => t.Id).ToList();

        var result = ids.Where(id => IndexOf(id) >= 0).Distinct().ToList();
        if (!result.Contains("main")) result.Insert(0, "main");
        return result;
    }
}

public static class SettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DynamicIsland", "settings.json");

    /// <param name="path">Файл настроек. По умолчанию — %AppData%; подменяется в тестах.</param>
    public static AppSettings Load(string? path = null)
    {
        path ??= FilePath;
        try
        {
            if (!File.Exists(path)) Save(new AppSettings(), path);
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            AppLog.Error("SettingsService.Load", ex);
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings, string? path = null)
    {
        path ??= FilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, Options));
    }

    public static void OpenInEditor()
    {
        Load(); // гарантирует, что файл существует
        try { Process.Start("notepad.exe", $"\"{FilePath}\""); } catch (Exception ex) { AppLog.Error("SettingsService.OpenInEditor", ex); }
    }
}
