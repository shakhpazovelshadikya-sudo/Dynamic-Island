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

/// <summary>Вкладка «Таймер».</summary>
public partial class MainWindow
{
    private readonly CountdownTimer _timer = new();

    // ---------- Таймер ----------
    private void UpdateTimerUi()
    {
        TimerText.Text = CountdownTimer.Format(_timer.Remaining);
        TimerPlay.Content = _timer.State == TimerState.Running ? "\uE769" : "\uE768";
        TimerStateText.Text = _timer.State switch
        {
            TimerState.Running => Loc.T("Идёт"),
            TimerState.Paused => Loc.T("Пауза"),
            _ => Loc.T("Выбери время")
        };
    }

    private void OnTimerFinished()
    {
        try { System.Media.SystemSounds.Exclamation.Play(); } catch (Exception ex) { AppLog.Error("MainWindow.OnTimerFinished", ex); }
        EnqueueToast(new NotificationInfo(NotificationService.OwnName, Loc.T("Таймер"), Loc.F("Время вышло: {0} мин", (int)_timer.Duration.TotalMinutes), null));
    }

    private void TimerPlay_Click(object sender, RoutedEventArgs e)
    {
        if (_timer.State == TimerState.Running) _timer.Pause(); else _timer.Start();
        UpdateUi();
    }

    private void TimerReset_Click(object sender, RoutedEventArgs e)
    {
        _timer.Reset();
        UpdateUi();
    }

    private void TimerPreset_Click(object sender, RoutedEventArgs e)
    {
        _timer.SetMinutes(int.Parse((string)((Button)sender).Tag, CultureInfo.InvariantCulture));
        _timer.Start();
        UpdateUi();
    }

    private void TimerAdjust_Click(object sender, RoutedEventArgs e)
    {
        _timer.Adjust(int.Parse((string)((Button)sender).Tag, CultureInfo.InvariantCulture));
        UpdateUi();
    }
}
