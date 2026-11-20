using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DynamicIsland;

/// <summary>Вкладка «Погода».</summary>
public partial class MainWindow
{
    private readonly WeatherService _weather = new();
    private readonly DispatcherTimer _weatherTimer = new() { Interval = TimeSpan.FromMinutes(20) };
    private WeatherInfo? _weatherInfo;
    private DateTime _weatherLoadedAt = DateTime.MinValue;
    private bool _weatherBusy;

    // ---------- Погода ----------
    private async Task RefreshWeatherAsync()
    {
        if (_weatherBusy) return;
        _weatherBusy = true;
        try
        {
            if (_weatherInfo == null) ShowWeatherStatus(Loc.T("Загрузка…"));

            var (info, error) = await _weather.GetAsync();
            if (info != null)
            {
                _weatherInfo = info;
                _weatherLoadedAt = DateTime.Now;
                ShowWeather(info);
            }
            else if (_weatherInfo == null)
            {
                ShowWeatherStatus(Loc.T(error ?? "Нет данных"));
            }
        }
        finally
        {
            _weatherBusy = false;
        }
    }

    private void ShowWeatherStatus(string text)
    {
        WeatherIcon.Text = "🌐";
        WeatherTemp.Text = "";
        WeatherCity.Text = text;
        WeatherDesc.Text = "";
        WeatherDetails.Text = "";
        WeatherRange.Text = "";
        ForecastPanel.Children.Clear();
    }

    private void ShowWeather(WeatherInfo w)
    {
        var (emoji, text) = WeatherCodes.Describe(w.Code);
        text = Loc.T(text);

        WeatherIcon.Text = emoji;
        WeatherTemp.Text = Deg(w.Temp);
        WeatherCity.Text = w.City;
        WeatherDesc.Text = text;
        WeatherDetails.Text = Loc.F("Ощущается {0}  ·  ветер {1} м/с", Deg(w.Feels), w.Wind.ToString("0"));

        var today = w.Days.FirstOrDefault();
        WeatherRange.Text = today != null
            ? Loc.F("Сегодня {0} / {1}  ·  влажн. {2}%", Deg(today.Max), Deg(today.Min), w.Humidity)
            : Loc.F("Влажность {0}%", w.Humidity);

        ForecastPanel.Children.Clear();
        foreach (var d in w.Days.Skip(1).Take(3))
        {
            var sp = new StackPanel { Width = 50, Margin = new Thickness(2, 0, 0, 0) };
            sp.Children.Add(Label(d.Date.ToString("ddd", Ru), 11, "#AAAAAA"));
            sp.Children.Add(new TextBlock
            {
                Text = WeatherCodes.Describe(d.Code).Emoji,
                FontFamily = new FontFamily("Segoe UI Emoji"),
                FontSize = 18,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            sp.Children.Add(Label(Deg(d.Max), 12, "#FFFFFF"));
            sp.Children.Add(Label(Deg(d.Min), 11, "#777777"));
            ForecastPanel.Children.Add(sp);
        }
    }

    private static TextBlock Label(string text, double size, string hex) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
        HorizontalAlignment = HorizontalAlignment.Center
    };

    private static string Deg(double t)
    {
        int r = (int)Math.Round(t);
        return r > 0 ? $"+{r}°" : $"{r}°";
    }
}
