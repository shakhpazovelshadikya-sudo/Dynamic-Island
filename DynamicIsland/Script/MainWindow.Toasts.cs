using System.IO;
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

/// <summary>Уведомления: капсула сама расширяется и показывает значок приложения и текст; раскрытый остров показывает карточку под собой.</summary>
public partial class MainWindow
{
    private const double NotifW = 360, NotifH = 88, NotifHShort = 58, NotifRadius = 24;

    private readonly NotificationService _notifier = new();
    private readonly Queue<NotificationInfo> _toastQueue = new();
    private bool _toastShowing;

    private readonly DispatcherTimer _notifTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool _notifActive;          // капсула сейчас показывает уведомление
    private NotificationInfo? _notifCurrent;

    private void EnqueueToast(NotificationInfo info)
    {
        if (_fsHidden) return; // в полноэкранном режиме уведомления острова не показываем
        while (_toastQueue.Count >= 5) _toastQueue.Dequeue(); // не копим бесконечную очередь
        _toastQueue.Enqueue(info);
        if (!_toastShowing) ShowNextToast();
    }

    private void ShowNextToast()
    {
        if (_toastQueue.Count == 0)
        {
            _toastShowing = false;
            return;
        }

        _toastShowing = true;
        var info = _toastQueue.Dequeue();

        if (_expanded) ShowToastWindow(info);   // остров раскрыт — карточка выезжает под ним
        else ShowNotification(info);            // иначе уведомление показывает сама капсула
    }

    // ---------- Карточка под раскрытым островом ----------
    private void ShowToastWindow(NotificationInfo info)
    {
        double top = Top + ExpandedH + 6;
        var toast = new NotificationToast(info, top, Left + Width / 2);
        toast.Closed += (_, _) => ShowNextToast();
        toast.Show();
    }

    // ---------- Уведомление в капсуле ----------
    private void InitNotifications() => _notifTimer.Tick += (_, _) => HideNotification();

    private void ShowNotification(NotificationInfo info)
    {
        _notifActive = true;
        _notifCurrent = info;
        _animVersion++;
        _langToken++; // прерываем анимацию смены языка
        _langActive = false;
        LangPanel.BeginAnimation(OpacityProperty, Anim(0, 100));

        NotifApp.Text = info.App;
        NotifTitle.Text = info.Title;
        NotifBody.Text = info.Body;
        NotifBody.Visibility = string.IsNullOrWhiteSpace(info.Body) ? Visibility.Collapsed : Visibility.Visible;
        SetNotifIcon(info);

        double h = string.IsNullOrWhiteSpace(info.Body) ? NotifHShort : NotifH;
        NotifPanel.Visibility = Visibility.Visible;

        Width = NotifW;
        Height = h;
        PositionTop();

        Island.BeginAnimation(WidthProperty, Anim(NotifW));
        Island.BeginAnimation(HeightProperty, Anim(h));
        BeginAnimation(RadiusProperty, Anim(NotifRadius));
        CompactRoot.BeginAnimation(OpacityProperty, Anim(0, 120));
        NotifPanel.BeginAnimation(OpacityProperty, Anim(1, 250, 160));

        _notifTimer.Interval = TimeSpan.FromSeconds(5);
        _notifTimer.Start();
    }

    /// <summary>Капсула сжимается обратно, затем показывается следующее уведомление из очереди.</summary>
    private void HideNotification()
    {
        if (!_notifActive) return;
        _notifActive = false;
        _notifTimer.Stop();
        int version = ++_animVersion;

        NotifPanel.BeginAnimation(OpacityProperty, Anim(0, 120));
        CompactRoot.BeginAnimation(OpacityProperty, Anim(1, 250, 150));
        BeginAnimation(RadiusProperty, Anim(CompactRadius));

        var w = Anim(_compactW);
        w.Completed += (_, _) =>
        {
            if (version != _animVersion) return; // за это время началась другая анимация
            Width = _compactW;
            Height = CompactH;
            PositionTop();
            NotifPanel.Visibility = Visibility.Collapsed;
            PetReact();
            ShowNextToast();
        };

        Island.BeginAnimation(HeightProperty, Anim(CompactH));
        Island.BeginAnimation(WidthProperty, w);
    }

    /// <summary>Остров раскрылся посреди уведомления: показываем его обычной карточкой под островом.</summary>
    private void CancelNotification()
    {
        if (!_notifActive) return;
        _notifActive = false;
        _notifTimer.Stop();

        NotifPanel.BeginAnimation(OpacityProperty, null);
        NotifPanel.Opacity = 0;
        NotifPanel.Visibility = Visibility.Collapsed;

        if (_notifCurrent != null) ShowToastWindow(_notifCurrent);
        else { _toastShowing = false; ShowNextToast(); }
    }

    private void SetNotifIcon(NotificationInfo info)
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
            catch (Exception ex) { AppLog.Error("MainWindow.SetNotifIcon", ex); }
        }
        else if (info.App == NotificationService.OwnName)
        {
            try { src = BitmapFrame.Create(new Uri("pack://application:,,,/assets/DynamicIsland.ico")); }
            catch (Exception ex) { AppLog.Error("MainWindow.SetNotifIcon", ex); }
        }

        NotifIcon.Source = src;
        NotifLetter.Text = src == null && info.App.Length > 0 ? info.App.Substring(0, 1).ToUpperInvariant() : "";
    }
}
