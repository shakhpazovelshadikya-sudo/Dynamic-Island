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

/// <summary>Смена раскладки: «EN → RU» в капсуле.</summary>
public partial class MainWindow
{
    private readonly DispatcherTimer _langTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private string? _lastLang;
    private IntPtr _lastHwnd;
    private int _langToken;
    private bool _langActive; // сейчас показана панель «EN → RU»

    // ---------- Смена языка: «EN → RU» ----------
    private void CheckLanguage()
    {
        var hwnd = GetForegroundWindow();
        string? lang = CurrentLang(hwnd);
        if (lang == null) return;

        // Бейдж с текущим языком в самой пилюле
        if (lang != CompactLang.Text)
        {
            CompactLang.Text = lang;
            CompactLangBadge.Visibility = Visibility.Visible;
        }

        // Другое окно в фокусе — раскладка у него своя, это не «смена»; просто запоминаем
        if (hwnd != _lastHwnd)
        {
            _lastHwnd = hwnd;
            _lastLang = lang;
            return;
        }

        if (lang == _lastLang) return;
        string? from = _lastLang;
        _lastLang = lang;

        if (_settings.AnimateLanguage && from != null)
            ShowLanguage(from, lang);
    }

    private static string? CurrentLang(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return null;
        try
        {
            uint thread = GetWindowThreadProcessId(hwnd, out _);
            int langId = (int)((long)GetKeyboardLayout(thread) & 0xFFFF);
            return new CultureInfo(langId).TwoLetterISOLanguageName.ToUpperInvariant();
        }
        catch
        {
            return null;
        }
    }

    private async void ShowLanguage(string from, string to)
    {
        if (_expanded || _notifActive || _uploadActive) return;
        int token = ++_langToken;
        _langActive = true;

        LangFrom.Text = from;
        LangTo.Text = to;

        // капсула чуть расширяется, текст времени/трека гаснет
        Width = LangW;
        PositionTop();
        Island.BeginAnimation(WidthProperty, Anim(LangW, 320));
        CompactRoot.BeginAnimation(OpacityProperty, Anim(0, 120));
        LangPanel.BeginAnimation(OpacityProperty, Anim(1, 200));

        // новый язык «выкатывается» снизу
        LangShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(380))
        {
            BeginTime = TimeSpan.FromMilliseconds(120),
            EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
        });
        LangTo.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260))
        {
            BeginTime = TimeSpan.FromMilliseconds(120)
        });

        await Task.Delay(1700);
        if (token != _langToken || _expanded) return;

        _langActive = false;
        LangPanel.BeginAnimation(OpacityProperty, Anim(0, 150));
        CompactRoot.BeginAnimation(OpacityProperty, Anim(1, 250, 100));

        var w = Anim(_compactW, 320);
        w.Completed += (_, _) =>
        {
            if (token != _langToken || _expanded) return;
            Width = _compactW;
            PositionTop();
        };
        Island.BeginAnimation(WidthProperty, w);
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] private static extern IntPtr GetKeyboardLayout(uint idThread);
}
