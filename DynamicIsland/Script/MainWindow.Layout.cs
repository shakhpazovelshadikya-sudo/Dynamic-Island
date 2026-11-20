using System.Windows;
using System.Windows.Media;

namespace DynamicIsland;

/// <summary>Размер острова подстраивается под длинное название трека: если текст не влезает — остров плавно расширяется.</summary>
public partial class MainWindow
{
    private static double MeasureText(System.Windows.Controls.TextBlock tb, string text, double dpi)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var typeface = new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch);
        var ft = new FormattedText(text, Ru, FlowDirection.LeftToRight, typeface, tb.FontSize, Brushes.White, dpi);
        return ft.WidthIncludingTrailingWhitespace;
    }

    /// <summary>Ширина острова под текст: текст + поля, но не меньше min и не больше max.</summary>
    public static double FitWidth(double textWidth, double margins, double min, double max) =>
        Math.Clamp(Math.Ceiling(textWidth + margins), min, max);

    // обложка 76 + отступы текста 12+8 + кнопки управления ≈ 3×34 + поля ExpandedView 28 + запас
    private const double ExpandedFixedPart = 76 + 12 + 8 + 3 * 34 + 28 + 8;

    private double ComputeCompactW()
    {
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var m = CompactTextHost.Margin;
        return FitWidth(MeasureText(CompactText, CompactText.Text, dpi), m.Left + m.Right + 4, CompactBaseW, CompactMaxW);
    }

    private double ComputeExpandedW()
    {
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double text = Math.Max(MeasureText(TitleText, TitleText.Text, dpi), MeasureText(ArtistText, ArtistText.Text, dpi));

        return FitWidth(text, ExpandedFixedPart, ExpandedBaseW, ExpandedMaxW);
    }

    /// <summary>Пересчитывает целевую ширину и плавно меняет её. Вызывается из UpdateUi.</summary>
    private void UpdateIslandSize()
    {
        double c = ComputeCompactW(), e = ComputeExpandedW();
        bool compactChanged = Math.Abs(c - _compactW) >= 0.5;
        bool expandedChanged = Math.Abs(e - _expandedW) >= 0.5;
        _compactW = c;
        _expandedW = e;

        if (_expanded) { if (expandedChanged) ResizeIsland(_expandedW); }
        else if (!_langActive && !_uploadActive && !_notifActive && compactChanged) ResizeIsland(_compactW);
    }

    private void ResizeIsland(double target)
    {
        int version = ++_animVersion;
        var anim = Anim(target, 320);

        if (target > Width)
        {
            // растём: сначала расширяем окно, чтобы остров не обрезался
            Width = target;
            PositionTop();
        }
        else
        {
            // сжимаемся: окно уменьшаем после анимации
            anim.Completed += (_, _) =>
            {
                if (version != _animVersion || _langActive) return;
                Width = target;
                PositionTop();
            };
        }

        Island.BeginAnimation(WidthProperty, anim);
    }
}
