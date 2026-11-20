using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DynamicIsland;

/// <summary>Тонкий ползунок для острова (прогресс трека и громкость): клик и перетаскивание.</summary>
public sealed class MiniSlider : Grid
{
    private readonly Border _track = new()
    {
        Height = 4,
        CornerRadius = new CornerRadius(2),
        Background = new SolidColorBrush(Color.FromArgb(0x33, 255, 255, 255)),
        VerticalAlignment = VerticalAlignment.Center,
        IsHitTestVisible = false
    };

    private readonly Border _fill = new()
    {
        Height = 4,
        CornerRadius = new CornerRadius(2),
        Background = Brushes.White,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Center,
        IsHitTestVisible = false
    };

    private readonly Ellipse _thumb = new()
    {
        Width = 10,
        Height = 10,
        Fill = Brushes.White,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Center,
        Opacity = 0,
        IsHitTestVisible = false
    };

    private double _max = 1;
    private double _value;

    /// <summary>Меняется пользователем (клик, перетаскивание) — на каждом движении.</summary>
    public event Action<double>? ValueChanged;

    /// <summary>Пользователь отпустил кнопку мыши.</summary>
    public event Action<double>? DragCompleted;

    public bool IsDragging { get; private set; }

    public MiniSlider()
    {
        Background = Brushes.Transparent; // чтобы ловить мышь по всей высоте
        Height = 18;
        Cursor = Cursors.Hand;
        Children.Add(_track);
        Children.Add(_fill);
        Children.Add(_thumb);

        SizeChanged += (_, _) => Refresh();
        MouseEnter += (_, _) => UpdateThumb();
        MouseLeave += (_, _) => UpdateThumb();
        IsEnabledChanged += (_, _) => Opacity = IsEnabled ? 1 : 0.4;
    }

    public double Maximum
    {
        get => _max;
        set { _max = value > 0 ? value : 1; Refresh(); }
    }

    /// <summary>Значение, заданное программой. Пока пользователь тянет ползунок, игнорируется.</summary>
    public double Value
    {
        get => _value;
        set
        {
            if (IsDragging) return;
            _value = Math.Clamp(value, 0, _max);
            Refresh();
        }
    }

    public Brush Accent
    {
        set { _fill.Background = value; _thumb.Fill = value; }
    }

    private void UpdateThumb() => _thumb.Opacity = IsEnabled && (IsDragging || IsMouseOver) ? 1 : 0;

    private void Refresh()
    {
        double w = ActualWidth;
        double x = _max > 0 ? _value / _max * w : 0;
        _fill.Width = Math.Clamp(x, 0, w);
        _thumb.Margin = new Thickness(Math.Clamp(x - 5, 0, Math.Max(0, w - 10)), 0, 0, 0);
    }

    private void Apply(MouseEventArgs e)
    {
        double w = ActualWidth;
        if (w <= 0) return;
        _value = Math.Clamp(e.GetPosition(this).X / w, 0, 1) * _max;
        Refresh();
        ValueChanged?.Invoke(_value);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (!IsEnabled) return;
        IsDragging = true;
        CaptureMouse();
        UpdateThumb();
        Apply(e);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (IsDragging) Apply(e);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!IsDragging) return;
        Apply(e);
        IsDragging = false;
        ReleaseMouseCapture();
        UpdateThumb();
        DragCompleted?.Invoke(_value);
        e.Handled = true;
    }
}
