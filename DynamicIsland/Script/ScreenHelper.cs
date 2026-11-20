using System.Windows;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace DynamicIsland;

/// <summary>Выбор монитора для острова: настройка хранит имя устройства, пустая строка = основной монитор.</summary>
public static class ScreenHelper
{
    public static Forms.Screen[] All => Forms.Screen.AllScreens;

    /// <summary>Монитор по сохранённому имени; если он отключён — основной.</summary>
    public static Forms.Screen Resolve(string? device)
    {
        var all = All;
        if (!string.IsNullOrEmpty(device))
        {
            var found = all.FirstOrDefault(s => s.DeviceName == device);
            if (found != null) return found;
        }
        return Forms.Screen.PrimaryScreen ?? all[0];
    }

    /// <summary>Границы монитора в единицах WPF (с учётом масштаба).</summary>
    public static Rect GetBoundsDip(Visual visual, string? device)
    {
        var b = Resolve(device).Bounds;
        var dpi = VisualTreeHelper.GetDpi(visual);
        return new Rect(b.Left / dpi.DpiScaleX, b.Top / dpi.DpiScaleY,
                        b.Width / dpi.DpiScaleX, b.Height / dpi.DpiScaleY);
    }

    public static string Describe(Forms.Screen s, int index) =>
        $"{Loc.F("Монитор {0}", index + 1)}{(s.Primary ? Loc.T(" (основной)") : "")} — {s.Bounds.Width}×{s.Bounds.Height}";
}
