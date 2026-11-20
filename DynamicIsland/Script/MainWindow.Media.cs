using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DynamicIsland;

/// <summary>Вкладка «Главная»: обложка, прогресс с перемоткой, громкость, выбор плеера.</summary>
public partial class MainWindow
{
    private readonly MediaService _media = new();
    private string _lastTrack = "";
    private MediaInfo _info = new();
    private readonly VolumeService _volume = new();
    private readonly DispatcherTimer _mediaTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private byte[]? _coverBytes;
    private int _menusOpen;
    private int _volTick;

    private void InitMediaControls()
    {
        // скругляем обложки (ClipToBounds у Border скругление не учитывает)
        RoundClip(CoverBorder, 12);
        RoundClip(CompactCover, 6);

        SeekSlider.ValueChanged += v => TimeCur.Text = FormatTime(TimeSpan.FromSeconds(v));
        SeekSlider.DragCompleted += async v =>
        {
            var target = TimeSpan.FromSeconds(v);
            // сразу запоминаем новую позицию, чтобы ползунок не прыгал назад до ответа плеера
            _info.Position = target;
            _info.CapturedAt = DateTime.UtcNow;
            await _media.SeekAsync(target);
        };

        VolSlider.ValueChanged += v =>
        {
            _volume.SetLevel((float)v);
            VolMute.Content = v <= 0.001 ? "\uE74F" : "\uE767";
        };

        _mediaTimer.Tick += (_, _) => { if (_expanded && _tab == 0) UpdateMediaUi(); };
        _mediaTimer.Start();

        TrackMenu(VpnMenu);
    }

    private static void RoundClip(FrameworkElement el, double radius) =>
        el.SizeChanged += (_, e) =>
            el.Clip = new RectangleGeometry(new Rect(0, 0, e.NewSize.Width, e.NewSize.Height), radius, radius);

    /// <summary>Пока открыто контекстное меню, остров не сворачивается.</summary>
    private void TrackMenu(ContextMenu menu)
    {
        menu.Opened += (_, _) => { _menusOpen++; _collapseTimer.Stop(); };
        menu.Closed += (_, _) =>
        {
            _menusOpen = Math.Max(0, _menusOpen - 1);
            if (_menusOpen == 0 && !Island.IsMouseOver) Schedule(600);
        };
    }

    // ---------- Обложка ----------
    private void ApplyCover(byte[]? data)
    {
        if (ReferenceEquals(data, _coverBytes)) return; // та же обложка — не декодируем заново
        _coverBytes = data;

        BitmapSource? bmp = null;
        if (data is { Length: > 0 })
        {
            try
            {
                var img = new BitmapImage();
                using var ms = new MemoryStream(data);
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = ms;
                img.EndInit();
                img.Freeze();
                bmp = img;
            }
            catch (Exception ex) { AppLog.Error("MainWindow.ApplyCover", ex); }
        }

        CoverImage.Source = bmp;
        CompactCoverImage.Source = bmp;
        CoverPlaceholder.Visibility = bmp == null ? Visibility.Visible : Visibility.Collapsed;
        CompactCoverPlaceholder.Visibility = bmp == null ? Visibility.Visible : Visibility.Collapsed;

        var brush = new SolidColorBrush(bmp != null ? AverageColor(bmp) : Colors.White);
        brush.Freeze();
        SeekSlider.Accent = brush;
    }

    /// <summary>Средний цвет обложки; тёмные оттенки подсвечиваем, чтобы полоска была видна на чёрном.</summary>
    private static Color AverageColor(BitmapSource src)
    {
        try
        {
            var small = new TransformedBitmap(src, new ScaleTransform(8.0 / src.PixelWidth, 8.0 / src.PixelHeight));
            var conv = new FormatConvertedBitmap(small, PixelFormats.Bgra32, null, 0);
            int w = conv.PixelWidth, h = conv.PixelHeight;
            var px = new byte[w * h * 4];
            conv.CopyPixels(px, w * 4, 0);

            long r = 0, g = 0, b = 0, n = 0;
            for (int i = 0; i + 3 < px.Length; i += 4)
            {
                if (px[i + 3] < 128) continue;
                b += px[i]; g += px[i + 1]; r += px[i + 2];
                n++;
            }
            if (n == 0) return Colors.White;

            double R = r / (double)n, G = g / (double)n, B = b / (double)n;
            double lum = (0.2126 * R + 0.7152 * G + 0.0722 * B) / 255;
            if (lum < 0.6)
            {
                double t = (0.6 - lum) / (1 - lum); // подмешиваем белый до комфортной яркости
                R += (255 - R) * t; G += (255 - G) * t; B += (255 - B) * t;
            }
            return Color.FromRgb((byte)R, (byte)G, (byte)B);
        }
        catch (Exception ex)
        {
            AppLog.Error("MainWindow.AverageColor", ex);
            return Colors.White;
        }
    }

    // ---------- Прогресс и громкость ----------
    private static string FormatTime(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{(int)t.TotalMinutes}:{t.Seconds:00}";

    private void UpdateMediaUi()
    {
        bool timeline = _info.HasSession && _info.Duration > TimeSpan.Zero;
        SeekSlider.IsEnabled = timeline && _info.CanSeek;

        if (timeline)
        {
            SeekSlider.Maximum = _info.Duration.TotalSeconds;
            if (!SeekSlider.IsDragging)
            {
                var p = _info.CurrentPosition;
                SeekSlider.Value = p.TotalSeconds;
                TimeCur.Text = FormatTime(p);
            }
            TimeDur.Text = FormatTime(_info.Duration);
        }
        else
        {
            SeekSlider.Value = 0;
            TimeCur.Text = "0:00";
            TimeDur.Text = _info.HasSession ? "live" : "0:00";
        }

        // громкость читаем раз в секунду, пока остров раскрыт на главной
        if (_expanded && _tab == 0 && !VolSlider.IsDragging && _volTick++ % 4 == 0)
        {
            var (level, muted) = _volume.Read();
            VolSlider.IsEnabled = _volume.Available;
            VolSlider.Value = muted ? 0 : level;
            VolMute.Content = muted || level <= 0.001 ? "\uE74F" : "\uE767";
        }

        // плеер и обложка в капсуле
        SourceChip.Visibility = _info.HasSession ? Visibility.Visible : Visibility.Collapsed;
        SourceChip.Content = _info.Source + (_info.PlayerCount > 1 ? " ▾" : "");

        bool showCover = _info.HasSession && _info.IsPlaying; // без картинки — значок ♫
        CompactCover.Visibility = showCover ? Visibility.Visible : Visibility.Collapsed;
        CompactTextHost.Margin = new Thickness(CompactLeftMargin, 0, showCover ? 36 : 14, 0);
    }

    private void VolMute_Click(object sender, RoutedEventArgs e)
    {
        _volume.ToggleMute();
        _volTick = 0; // сразу перечитать
        UpdateMediaUi();
    }

    // ---------- Выбор плеера ----------
    private void SourceChip_Click(object sender, RoutedEventArgs e)
    {
        var players = _media.GetPlayers();
        var menu = new ContextMenu { PlacementTarget = SourceChip, Placement = PlacementMode.Bottom };
        TrackMenu(menu);

        var auto = new MenuItem { Header = Loc.T("Авто (как выбирает Windows)"), IsCheckable = true, IsChecked = _media.PinnedId == null };
        auto.Click += (_, _) => _media.SelectPlayer(null);
        menu.Items.Add(auto);

        if (players.Count > 0) menu.Items.Add(new Separator());
        foreach (var p in players)
        {
            var item = new MenuItem
            {
                Header = p.Name + (p.IsPlaying ? "  ▶" : ""),
                IsCheckable = true,
                IsChecked = p.Id == _media.PinnedId
            };
            string id = p.Id;
            item.Click += (_, _) => _media.SelectPlayer(id);
            menu.Items.Add(item);
        }

        menu.IsOpen = true;
    }

    // ---------- Медиа ----------
    private async void PlayPause_Click(object sender, RoutedEventArgs e) => await _media.PlayPauseAsync();
    private async void Next_Click(object sender, RoutedEventArgs e) => await _media.NextAsync();
    private async void Prev_Click(object sender, RoutedEventArgs e) => await _media.PrevAsync();

    private void OnMediaChanged(MediaInfo info)
    {
        _info = info;

        string track = info.Title + "|" + info.Artist;
        bool newTrack = info.HasSession && info.IsPlaying && !string.IsNullOrEmpty(info.Title) && track != _lastTrack;
        if (!string.IsNullOrEmpty(info.Title)) _lastTrack = track;

        ApplyCover(info.Cover);
        UpdateUi();

        // При смене трека остров сам раскрывается на 3 секунды (на вкладке «Главная»)
        if (newTrack && _settings.ExpandOnTrackChange)
        {
            if (!Island.IsMouseOver) SelectTab(0, false);
            PetReact();
            Expand();
            if (!Island.IsMouseOver) Schedule(3000);
        }
    }
}
