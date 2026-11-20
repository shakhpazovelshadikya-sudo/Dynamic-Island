using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DynamicIsland;

/// <summary>Бегущая строка: название, которое не влезло даже в максимальную ширину, медленно прокручивается.</summary>
public partial class MainWindow
{
    private const double MarqueeSpeed = 40; // px/с
    private const double MarqueePause = 1.6; // с — пауза в начале и в конце

    private sealed class MarqueeState
    {
        public TranslateTransform Shift = new();
        public string? Key; // что сейчас крутится; null — строка стоит
    }

    private readonly Dictionary<TextBlock, MarqueeState> _marquees = new();

    /// <summary>На сколько px надо сдвинуть текст, чтобы показать его конец. 0 — текст помещается.</summary>
    public static double MarqueeDistance(double textWidth, double availableWidth)
    {
        double over = textWidth - availableWidth;
        return over <= 1 ? 0 : Math.Ceiling(over) + 6; // +6 — чтобы последняя буква не упиралась в край
    }

    /// <summary>Проверяет, нужна ли прокрутка у строк капсулы и вкладки «Главная». Вызывается из UpdateUi.</summary>
    private void UpdateMarquees()
    {
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        var m = CompactTextHost.Margin;
        ApplyMarquee(CompactText, _compactW - m.Left - m.Right - 4, dpi);

        double expandedAvail = _expandedW - ExpandedFixedPart;
        ApplyMarquee(TitleText, expandedAvail, dpi);
        ApplyMarquee(ArtistText, expandedAvail, dpi);
    }

    private void ApplyMarquee(TextBlock tb, double available, double dpi)
    {
        if (!_marquees.TryGetValue(tb, out var st))
        {
            st = new MarqueeState();
            tb.RenderTransform = st.Shift;
            _marquees[tb] = st;
        }

        double distance = MarqueeDistance(MeasureText(tb, tb.Text, dpi), available);
        if (distance <= 0)
        {
            if (st.Key == null) return;
            st.Key = null;
            st.Shift.BeginAnimation(TranslateTransform.XProperty, null);
            st.Shift.X = 0;
            tb.TextTrimming = TextTrimming.CharacterEllipsis;
            return;
        }

        string key = $"{tb.Text}|{Math.Round(available)}";
        if (key == st.Key) return; // уже крутится — не перезапускаем каждую секунду
        st.Key = key;

        tb.TextTrimming = TextTrimming.None; // иначе вместо прокрутки появится «…»

        double scroll = distance / MarqueeSpeed;
        double end = MarqueePause + scroll;
        var anim = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(MarqueePause))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(-distance, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(end))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(-distance, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(end + MarqueePause))));
        anim.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(end + MarqueePause + 0.35)),
            new QuadraticEase { EasingMode = EasingMode.EaseOut }));

        st.Shift.BeginAnimation(TranslateTransform.XProperty, anim);
    }
}
