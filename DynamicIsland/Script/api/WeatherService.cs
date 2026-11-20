using System.Threading;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace DynamicIsland;

public class DayForecast
{
    public DateTime Date { get; set; }
    public double Max { get; set; }
    public double Min { get; set; }
    public int Code { get; set; }
}

public class WeatherInfo
{
    public string City { get; set; } = "";
    public double Temp { get; set; }
    public double Feels { get; set; }
    public int Humidity { get; set; }
    public double Wind { get; set; }
    public int Code { get; set; }
    public List<DayForecast> Days { get; set; } = new();
}

public static class WeatherCodes
{
    public static (string Emoji, string Text) Describe(int code) => code switch
    {
        0 => ("☀️", "Ясно"),
        1 => ("🌤️", "Преимущественно ясно"),
        2 => ("⛅", "Переменная облачность"),
        3 => ("☁️", "Пасмурно"),
        45 or 48 => ("🌫️", "Туман"),
        51 or 53 or 55 or 56 or 57 => ("🌦️", "Морось"),
        61 or 63 or 65 or 66 or 67 => ("🌧️", "Дождь"),
        71 or 73 or 75 or 77 => ("🌨️", "Снег"),
        80 or 81 or 82 => ("🌦️", "Ливень"),
        85 or 86 => ("🌨️", "Снегопад"),
        95 or 96 or 99 => ("⛈️", "Гроза"),
        _ => ("🌡️", "—")
    };
}

/// <summary>
/// Погода с сайта open-meteo.com (бесплатно, без API-ключа).
/// Город берётся из settings.json, а если он пустой — определяется по IP.
/// </summary>
public class WeatherService
{
    private static readonly HttpClient Http = CreateClient();

    private record GeoPoint(string Name, double Lat, double Lon);

    private GeoPoint? _loc;

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("DynamicIsland/1.0");
        return c;
    }

    public async Task<(WeatherInfo? Info, string? Error)> GetAsync()
    {
        try
        {
            // Город определяется автоматически по IP (один раз, дальше кэшируется)
            if (_loc == null)
            {
                var loc = await LocateByIpAsync();
                if (loc == null)
                    return (null, Loc.T("Не удалось определить город"));
                _loc = loc;
            }

            string url = "https://api.open-meteo.com/v1/forecast"
                + $"?latitude={_loc.Lat.ToString(CultureInfo.InvariantCulture)}"
                + $"&longitude={_loc.Lon.ToString(CultureInfo.InvariantCulture)}"
                + "&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m"
                + "&daily=weather_code,temperature_2m_max,temperature_2m_min"
                + "&timezone=auto&forecast_days=4&wind_speed_unit=ms";

            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
            var root = doc.RootElement;
            var cur = root.GetProperty("current");

            var info = new WeatherInfo
            {
                City = _loc.Name,
                Temp = cur.GetProperty("temperature_2m").GetDouble(),
                Feels = cur.GetProperty("apparent_temperature").GetDouble(),
                Humidity = (int)Math.Round(cur.GetProperty("relative_humidity_2m").GetDouble()),
                Wind = cur.GetProperty("wind_speed_10m").GetDouble(),
                Code = cur.GetProperty("weather_code").GetInt32()
            };

            var daily = root.GetProperty("daily");
            var times = daily.GetProperty("time");
            var codes = daily.GetProperty("weather_code");
            var max = daily.GetProperty("temperature_2m_max");
            var min = daily.GetProperty("temperature_2m_min");

            for (int i = 0; i < times.GetArrayLength(); i++)
            {
                info.Days.Add(new DayForecast
                {
                    Date = DateTime.ParseExact(times[i].GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Code = codes[i].GetInt32(),
                    Max = max[i].GetDouble(),
                    Min = min[i].GetDouble()
                });
            }

            return (info, null);
        }
        catch (Exception ex)
        {
            AppLog.Error("WeatherService.GetAsync", ex);
            return (null, Loc.T("Нет связи с сервером погоды"));
        }
    }

    private static async Task<GeoPoint?> GeocodeAsync(string city)
    {
        string url = "https://geocoding-api.open-meteo.com/v1/search"
            + $"?name={Uri.EscapeDataString(city)}&count=1&language={Loc.Code}&format=json";

        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
            return null;

        var r = results[0];
        return new GeoPoint(
            r.GetProperty("name").GetString() ?? city,
            r.GetProperty("latitude").GetDouble(),
            r.GetProperty("longitude").GetDouble());
    }

    private static async Task<GeoPoint?> LocateByIpAsync()
    {
        try
        {
            using var doc = JsonDocument.Parse(await Http.GetStringAsync($"https://ipwho.is/?lang={Loc.Code}"));
            var r = doc.RootElement;
            if (!(r.TryGetProperty("success", out var ok) && !ok.GetBoolean()))
            {
                return new GeoPoint(
                    r.GetProperty("city").GetString() ?? Loc.T("Мой город"),
                    r.GetProperty("latitude").GetDouble(),
                    r.GetProperty("longitude").GetDouble());
            }
        }
        catch (Exception ex) { AppLog.Error("WeatherService.LocateByIpAsync", ex); }

        // запасной вариант (тоже HTTPS)
        try
        {
            using var doc = JsonDocument.Parse(await Http.GetStringAsync("https://ipapi.co/json/"));
            var r = doc.RootElement;
            bool failed = r.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.True;
            if (!failed
                && r.TryGetProperty("latitude", out var lat) && lat.ValueKind == JsonValueKind.Number
                && r.TryGetProperty("longitude", out var lon) && lon.ValueKind == JsonValueKind.Number)
            {
                string city = r.TryGetProperty("city", out var c) && c.ValueKind == JsonValueKind.String
                    ? c.GetString() ?? Loc.T("Мой город") : Loc.T("Мой город");
                return new GeoPoint(city, lat.GetDouble(), lon.GetDouble());
            }
        }
        catch (Exception ex) { AppLog.Error("WeatherService.LocateByIpAsync", ex); }

        return null;
    }
}
