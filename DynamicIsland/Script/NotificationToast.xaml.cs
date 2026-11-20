using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DynamicIsland;

/// <summary>Карточка уведомления, которая «выезжает» из-под острова, показывает приложение, заголовок и текст.</summary>
public partial class NotificationToast : Window
{
    private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool _closing;

    public NotificationToast(NotificationInfo info, double top, double centerX)
    {
        InitializeComponent();

        AppText.Text = info.App;
        TitleText.Text = info.Title;
        BodyText.Text = info.Body;
        BodyText.Visibility = string.IsNullOrWhiteSpace(info.Body) ? Visibility.Collapsed : Visibility.Visible;
        SetIcon(info);

        Left = centerX - Width / 2;
        Top = top;

        _hideTimer.Tick += (_, _) => Dismiss();
        Loaded += (_, _) => { PlayShow(); _hideTimer.Start(); };
    }

    // ---------- Иконка ----------
    private void SetIcon(NotificationInfo info)
    {
        ImageSource? src = null;

        if (info.Icon is { Length: > 0 })
        {
            try
            {
                var bmp = new BitmapImage();
                using var ms = new MemoryStream(info.Icon);
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                src = bmp;
            }
            catch (Exception ex) { AppLog.Error("NotificationToast.SetIcon", ex); }
        }
        else if (info.App == NotificationService.OwnName)
        {
            try { src = BitmapFrame.Create(new Uri("pack://application:,,,/assets/DynamicIsland.ico")); } catch (Exception ex) { AppLog.Error("NotificationToast.SetIcon", ex); }
        }

        IconImage.Source = src;
        IconLetter.Text = src == null && info.App.Length > 0 ? info.App.Substring(0, 1).ToUpperInvariant() : "";
    }

    // ---------- Анимации ----------
    private static DoubleAnimation Anim(double to, int ms) => new(to, TimeSpan.FromMilliseconds(ms))
    {
        EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
    };

    private void PlayShow()
    {
        Card.BeginAnimation(OpacityProperty, Anim(1, 260));
        Scale.BeginAnimation(ScaleTransform.ScaleXProperty, Anim(1, 460));
        Scale.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(1, 460));
        Shift.BeginAnimation(TranslateTransform.YProperty, Anim(0, 460));
    }

    private void Dismiss()
    {
        if (_closing) return;
        _closing = true;
        _hideTimer.Stop();

        var fade = Anim(0, 240);
        fade.Completed += (_, _) => Close();
        Card.BeginAnimation(OpacityProperty, fade);
        Scale.BeginAnimation(ScaleTransform.ScaleXProperty, Anim(0.6, 240));
        Scale.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(0.7, 240));
        Shift.BeginAnimation(TranslateTransform.YProperty, Anim(-16, 240));
    }

    // ---------- Мышь ----------
    private void Card_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) => _hideTimer.Stop();

    private void Card_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_closing) return;
        _hideTimer.Interval = TimeSpan.FromSeconds(2);
        _hideTimer.Start();
    }

    private void Card_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => Dismiss();

    // ---------- Не забирать фокус и не светиться в Alt+Tab ----------
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int style = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
