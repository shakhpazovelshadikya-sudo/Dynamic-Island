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

public partial class MainWindow : Window
{
    private const double CompactBaseW = 160, CompactH = 34;
    private const double ExpandedBaseW = 460, ExpandedH = 178;

    // Верхние пределы ширины берутся из настроек (раздел «Размер»)
    private double CompactMaxW => Math.Max(CompactBaseW, _settings.CompactMaxWidth);
    private double ExpandedMaxW => Math.Max(ExpandedBaseW, _settings.ExpandedMaxWidth);

    // Текущая ширина острова: растёт, если название трека не помещается (см. MainWindow.Layout.cs)
    private double _compactW = CompactBaseW, _expandedW = ExpandedBaseW;
    private const double CompactRadius = 17, ExpandedRadius = 24;
    private const double LangW = 210;

    private static CultureInfo Ru => Loc.Culture;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _collapseTimer = new();
    private AppSettings _settings = SettingsService.Load();
    private SettingsWindow? _settingsWindow;
    private bool _expanded;
    private int _animVersion;
    private TrayService? _tray;
    private int _tab;

    // Радиус скругления острова (анимируется: капсула -> прямоугольник)
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(
        nameof(Radius), typeof(double), typeof(MainWindow),
        new PropertyMetadata(CompactRadius, (d, e) => ((MainWindow)d).Island.CornerRadius = new CornerRadius((double)e.NewValue)));

    public double Radius
    {
        get => (double)GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    public MainWindow()
    {
        InitializeComponent();
        Loc.Apply(this);
        ApplyTabs();
        InitNotifications();
        InitPet();

        Width = _compactW;
        Height = CompactH;
        PositionTop();

        _clock.Tick += (_, _) => UpdateUi();
        _collapseTimer.Tick += (_, _) =>
        {
            _collapseTimer.Stop();
            if (Island.IsMouseOver || _menusOpen > 0 || _trTyping) return; // курсор над островом или открыто меню — не сворачиваем
            Collapse();
        };
        _langTimer.Tick += (_, _) => CheckLanguage();
        _fsTimer.Tick += (_, _) => CheckFullscreen();
        _weatherTimer.Tick += async (_, _) => await RefreshWeatherAsync();

        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;

        _clip.Changed += () => { if (_tab == 5) BuildClipboard(); };
        _armTimer.Tick += (_, _) => DisarmPower();
        InitMediaControls();
        InitUpdates();

        Loaded += async (_, _) =>
        {
            AutoStart.Init();
            _tray = new TrayService(this, () => Dispatcher.Invoke(() => { _ = RefreshWeatherAsync(); }), () => Dispatcher.Invoke(OpenSettings));

            SelectTab(0, false);
            UpdateUi();
            _clock.Start();
            _langTimer.Start();
            _fsTimer.Start();

            _weatherTimer.Start();
            _ = RefreshWeatherAsync();

            _notifier.Received += info => Dispatcher.Invoke(() => EnqueueToast(info));
            _notifier.AccessProblem += msg => Dispatcher.Invoke(() =>
                EnqueueToast(new NotificationInfo(NotificationService.OwnName, Loc.T("Нет доступа к уведомлениям"), Loc.T(msg), null)));
            _ = _notifier.RunAsync(() => _settings.ShowNotifications);

            _media.Changed += info => Dispatcher.Invoke(() => OnMediaChanged(info));
            try { await _media.InitAsync(); } catch (Exception ex) { AppLog.Error("MediaService.InitAsync", ex); }
        };
    }

    private void OnDisplayChanged(object? sender, EventArgs e) => Dispatcher.Invoke(PositionTop);

    protected override void OnClosed(EventArgs e)
    {
        _volume.Dispose();
        SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        try { RemoveClipboardFormatListener(new WindowInteropHelper(this).Handle); } catch { }
        _tray?.Dispose();
        base.OnClosed(e);
    }

    // ---------- Позиция: сверху по центру выбранного монитора ----------
    private void PositionTop()
    {
        var b = ScreenHelper.GetBoundsDip(this, _settings.MonitorDevice);
        Left = b.Left + (b.Width - Width) / 2;
        Top = b.Top + _settings.TopMargin;
    }

    // ---------- Скрыть из Alt+Tab и не забирать фокус ----------
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int style = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);

        // слушаем изменения буфера обмена
        try
        {
            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
            AddClipboardFormatListener(hwnd);
        }
        catch (Exception ex) { AppLog.Error("MainWindow.ClipboardListener", ex); }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE && _settings.ClipboardHistory) _clip.NotifyUpdate();
        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    // ---------- Анимации ----------
    private static DoubleAnimation Anim(double to, int ms = 380, int delayMs = 0) => new(to, TimeSpan.FromMilliseconds(ms))
    {
        BeginTime = TimeSpan.FromMilliseconds(delayMs),
        EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
    };

    private void Expand()
    {
        _animVersion++;
        _langToken++; // прерываем анимацию смены языка
        _langActive = false;
        _uploadActive = false;
        PillUpload.BeginAnimation(OpacityProperty, Anim(0, 100));
        LangPanel.BeginAnimation(OpacityProperty, Anim(0, 100));
        _expanded = true;
        _collapseTimer.Stop();
        CancelNotification(); // раскрытый остров показывает уведомления карточкой под собой

        Width = _expandedW;
        Height = ExpandedH;
        PositionTop();

        Island.BeginAnimation(WidthProperty, Anim(_expandedW));
        Island.BeginAnimation(HeightProperty, Anim(ExpandedH));
        BeginAnimation(RadiusProperty, Anim(ExpandedRadius));
        CompactRoot.BeginAnimation(OpacityProperty, Anim(0, 120));
        ExpandedView.BeginAnimation(OpacityProperty, Anim(1, 250, 160));
    }

    private void Collapse()
    {
        if (!_expanded) return;
        DisarmPower();
        _expanded = false;
        int version = ++_animVersion;

        ExpandedView.BeginAnimation(OpacityProperty, Anim(0, 120));
        CompactRoot.BeginAnimation(OpacityProperty, Anim(1, 250, 150));
        BeginAnimation(RadiusProperty, Anim(CompactRadius));

        var w = Anim(_compactW);
        w.Completed += (_, _) =>
        {
            if (version != _animVersion) return;
            Width = _compactW;
            Height = CompactH;
            PositionTop();
            PetReact(); // остров свернулся — питомец снова на виду и радуется
        };

        Island.BeginAnimation(HeightProperty, Anim(CompactH));
        Island.BeginAnimation(WidthProperty, w);
    }

    private void Schedule(int ms)
    {
        _collapseTimer.Stop();
        _collapseTimer.Interval = TimeSpan.FromMilliseconds(ms);
        _collapseTimer.Start();
    }

    // ---------- Мышь ----------
    private void Island_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _petLastActivity = DateTime.Now; // наведение будит питомца
        if (_notifActive) { _notifTimer.Stop(); return; } // пока курсор на уведомлении — не прячем и не раскрываем
        Expand();
    }

    private void Island_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_notifActive) { _notifTimer.Interval = TimeSpan.FromSeconds(2); _notifTimer.Start(); return; }
        Schedule(500);
    }

    private void Island_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_notifActive) HideNotification(); // клик по уведомлению закрывает его
    }
    // ---------- Настройки (отдельное окно) ----------
    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();

    public void OpenSettings()
    {
        if (_settingsWindow != null)
        {
            if (_settingsWindow.WindowState == WindowState.Minimized) _settingsWindow.WindowState = WindowState.Normal;
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(ApplySettings, () => EnqueueToast(new NotificationInfo(
            NotificationService.OwnName, Loc.T("Тестовое уведомление"), Loc.T("Так будут выглядеть сообщения из других приложений"), null)));
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    /// <summary>Вызывается после сохранения настроек: перечитывает файл и применяет изменения.</summary>
    private void ApplySettings()
    {
        _settings = SettingsService.Load();
        if (!_settings.ClipboardHistory) _clip.Clear();
        if (_tab == 5) BuildClipboard();
        PositionTop();
        ApplyTabs();
        ApplyPet();
        UpdateIslandSize();
        CheckFullscreen();
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    // ---------- Обновление интерфейса ----------
    private void UpdateUi()
    {
        string time = DateTime.Now.ToString("HH:mm");
        if (_tab == 2 && _calBuiltFor != DateTime.Today) { _calMonth = DateTime.Today; BuildCalendar(); }

        if (_timer.Poll()) OnTimerFinished();
        if (_tab == 6 && DateTime.Now - _sysRefreshedAt > TimeSpan.FromSeconds(5)) _ = RefreshSystemAsync();

        bool hasTrack = _info.HasSession && !string.IsNullOrEmpty(_info.Title);

        string compact = hasTrack && _info.IsPlaying ? "♪ " + _info.Title : time;
        if (_timer.State == TimerState.Running) compact = "⏱ " + CountdownTimer.Format(_timer.Remaining);
        else if (_timer.State == TimerState.Paused) compact = "⏸ " + CountdownTimer.Format(_timer.Remaining);
        CompactText.Text = compact;
        UpdateTimerUi();
        UpdateMediaUi();

        TitleText.Text = hasTrack ? _info.Title : Loc.T("Ничего не играет");
        ArtistText.Text = hasTrack ? _info.Artist : "";
        PlayButton.Content = _info.IsPlaying ? "\uE769" : "\uE768";

        string battery = GetBatteryText();
        StatusText.Text = string.IsNullOrEmpty(battery) ? time : $"{time}  ·  {battery}";

        UpdateIslandSize();
        UpdateMarquees();
    }
}
