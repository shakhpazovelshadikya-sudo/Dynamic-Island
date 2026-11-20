using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace DynamicIsland;

/// <summary>Язык для переводчика: код (как в Google Translate) и название.</summary>
public sealed record Lang(string Code, string Name);

/// <summary>
/// Переводчик: бесплатная конечная точка translate.googleapis.com (без API-ключа).
/// Нужен интернет. Длинные тексты ограничены <see cref="MaxChars"/> символами.
/// </summary>
public static class TranslateService
{
    public const int MaxChars = 1800;
    public const string Auto = "auto";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 DynamicIsland/1.0");
        return c;
    }

    public static readonly Lang[] Languages =
    {
        new("en", "Английский"), new("ru", "Русский"), new("uk", "Украинский"), new("be", "Белорусский"),
        new("de", "Немецкий"), new("fr", "Французский"), new("es", "Испанский"), new("it", "Итальянский"),
        new("pt", "Португальский"), new("zh-CN", "Китайский (упрощ.)"), new("zh-TW", "Китайский (трад.)"),
        new("ja", "Японский"), new("ko", "Корейский"), new("ar", "Арабский"), new("tr", "Турецкий"),
        new("pl", "Польский"), new("cs", "Чешский"), new("sk", "Словацкий"), new("bg", "Болгарский"),
        new("sr", "Сербский"), new("hr", "Хорватский"), new("el", "Греческий"), new("iw", "Иврит"),
        new("hi", "Хинди"), new("vi", "Вьетнамский"), new("th", "Тайский"), new("id", "Индонезийский"),
        new("nl", "Нидерландский"), new("sv", "Шведский"), new("no", "Норвежский"), new("da", "Датский"),
        new("fi", "Финский"), new("hu", "Венгерский"), new("ro", "Румынский"), new("kk", "Казахский"),
        new("uz", "Узбекский"), new("az", "Азербайджанский"), new("hy", "Армянский"), new("ka", "Грузинский"),
        new("lv", "Латышский"), new("lt", "Литовский"), new("et", "Эстонский"), new("fa", "Персидский"),
        new("tt", "Татарский"), new("la", "Латынь"), new("eo", "Эсперанто")
    };

    public static string NameOf(string code) =>
        code == Auto ? Loc.T("Авто") : Loc.T(Languages.FirstOrDefault(l => l.Code == code)?.Name ?? code);

    /// <summary>Разбирает ответ сервиса: склеивает куски перевода и берёт определённый язык.</summary>
    public static (string Text, string? Detected) ParseResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var sb = new StringBuilder();

        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0 && root[0].ValueKind == JsonValueKind.Array)
            foreach (var seg in root[0].EnumerateArray())
                if (seg.ValueKind == JsonValueKind.Array && seg.GetArrayLength() > 0 && seg[0].ValueKind == JsonValueKind.String)
                    sb.Append(seg[0].GetString());

        string? detected = root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 2 && root[2].ValueKind == JsonValueKind.String
            ? root[2].GetString() : null;
        return (sb.ToString(), detected);
    }

    public static async Task<(string? Text, string? Detected, string? Error)> TranslateAsync(
        string text, string from, string to, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return ("", null, null);
        if (text.Length > MaxChars) text = text[..MaxChars];

        try
        {
            string url = "https://translate.googleapis.com/translate_a/single?client=gtx&dt=t"
                + $"&sl={Uri.EscapeDataString(from)}&tl={Uri.EscapeDataString(to)}&q={Uri.EscapeDataString(text)}";
            string json = await Http.GetStringAsync(url, ct);
            var (res, det) = ParseResponse(json);
            return (res, det, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return (null, null, null); }
        catch (Exception ex)
        {
            AppLog.Error("TranslateService.TranslateAsync", ex);
            return (null, null, Loc.T("Нет соединения с сервисом перевода"));
        }
    }
}
