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

/// <summary>Скрытие острова, когда на мониторе открыто полноэкранное окно.</summary>
public partial class MainWindow
{
    private readonly DispatcherTimer _fsTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private bool _fsHidden; // остров спрятан из-за полноэкранного окна

    // ---------- Скрытие в полноэкранном режиме ----------
    private void CheckFullscreen()
    {
        bool want = _settings.HideInFullscreen && IsForegroundFullscreen();
        if (want == _fsHidden) return;

        if (want)
        {
            if (Visibility != Visibility.Visible) return; // уже скрыт через трей — не трогаем
            _fsHidden = true;
            _collapseTimer.Stop();
            Visibility = Visibility.Hidden;
        }
        else
        {
            _fsHidden = false;
            if (Visibility != Visibility.Visible) Visibility = Visibility.Visible;
            if (_expanded && !Island.IsMouseOver) Schedule(300); // вернулся — свернуть, если остался раскрытым
        }
    }

    /// <summary>Окно переднего плана без рамки закрывает весь монитор острова — игра, видео, F11 в браузере.</summary>
    private bool IsForegroundFullscreen()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;

            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == (uint)Environment.ProcessId) return false; // свои окна (настройки, карточки) не считаем

            var cls = new StringBuilder(64);
            GetClassName(hwnd, cls, cls.Capacity);
            string c = cls.ToString();
            if (c is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false; // рабочий стол и панель задач

            if (!GetWindowRect(hwnd, out RECT r)) return false;

            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref mi)) return false;
            var target = ScreenHelper.Resolve(_settings.MonitorDevice).Bounds;
            if (mi.rcMonitor.Left != target.Left || mi.rcMonitor.Top != target.Top) return false; // остров живёт на выбранном мониторе

            bool covers = r.Left <= mi.rcMonitor.Left && r.Top <= mi.rcMonitor.Top
                       && r.Right >= mi.rcMonitor.Right && r.Bottom >= mi.rcMonitor.Bottom;
            if (!covers) return false;

            // развёрнутое обычное окно имеет заголовок, полноэкранное — нет
            return (GetWindowLong(hwnd, GWL_STYLE) & WS_CAPTION) != WS_CAPTION;
        }
        catch (Exception ex)
        {
            AppLog.Error("MainWindow.IsForegroundFullscreen", ex);
            return false;
        }
    }

    private const int GWL_STYLE = -16;
    private const int WS_CAPTION = 0x00C00000;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const uint MONITORINFOF_PRIMARY = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder text, int maxCount);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);
}
