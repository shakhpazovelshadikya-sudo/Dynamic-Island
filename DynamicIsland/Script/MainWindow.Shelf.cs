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

/// <summary>Вкладка «Полка»: файлы перетаскиванием.</summary>
public partial class MainWindow
{
    private readonly ShelfService _shelf = new();
    private static readonly Brush ShelfIdle = Frozen(Color.FromArgb(0x1A, 255, 255, 255));
    private static readonly Brush ShelfHover = Frozen(Color.FromArgb(0x38, 255, 255, 255));

    private static bool HasFiles(DragEventArgs e) => e.Data.GetDataPresent(DataFormats.FileDrop);

    private void Island_DragEnter(object sender, DragEventArgs e) => OnDragActive(e);
    private void Island_DragOver(object sender, DragEventArgs e) => OnDragActive(e);

    private void OnDragActive(DragEventArgs e)
    {
        e.Handled = true;
        if (!HasFiles(e) || !IsTabVisible(3)) // вкладка «Полка» может быть скрыта в настройках
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        e.Effects = DragDropEffects.Link; // файл не копируется — на полке только ссылка на него
        _collapseTimer.Stop();
        if (_expanded)
        {
            if (_tab != 3) SelectTab(3);
            ShelfDropHint.Visibility = Visibility.Visible;
        }
        else if (!_uploadActive)
            PillShow(Loc.T("Отпусти файл"), null, 0);
    }

    private void Island_DragLeave(object sender, DragEventArgs e)
    {
        ShelfDropHint.Visibility = Visibility.Collapsed;
        if (_uploadActive && !_uploadBusy) PillHide();
        Schedule(700);
    }

    private void Island_Drop(object sender, DragEventArgs e)
    {
        ShelfDropHint.Visibility = Visibility.Collapsed;
        if (!HasFiles(e)) return;
        e.Handled = true;

        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            _collapseTimer.Stop();
            _ = AddToShelfWithProgressAsync(paths);
        }
        else
            Schedule(3000);
    }

    private int _shelfRun; // номер текущей загрузки: новая отменяет предыдущую
    private bool _uploadActive, _uploadBusy;
    private int _pillToken;

    /// <summary>Показывает таблетку-загрузку в самой капсуле (расширяет её, как при смене языка).</summary>
    private void PillShow(string text, ImageSource? icon, double progress)
    {
        PillText.Text = text;
        PillIcon.Source = icon;
        PillIcon.Visibility = icon == null ? Visibility.Collapsed : Visibility.Visible;
        PillFillScale.ScaleX = progress;
        PillBar.Visibility = icon == null ? Visibility.Collapsed : Visibility.Visible;
        PillBadge.Visibility = Visibility.Collapsed;
        PillFill.Background = new SolidColorBrush(Color.FromRgb(0x4D, 0xA3, 0xFF));
        if (_uploadActive) return;

        _pillToken++;
        _uploadActive = true;
        Width = Math.Max(LangW, _compactW);
        PositionTop();
        Island.BeginAnimation(WidthProperty, Anim(Math.Max(LangW, _compactW), 320));
        CompactRoot.BeginAnimation(OpacityProperty, Anim(0, 120));
        PillUpload.BeginAnimation(OpacityProperty, Anim(1, 200));
    }

    private void PillHide()
    {
        if (!_uploadActive) return;
        _uploadActive = false;
        int token = ++_pillToken;
        PillUpload.BeginAnimation(OpacityProperty, Anim(0, 150));
        if (_expanded) return;
        CompactRoot.BeginAnimation(OpacityProperty, Anim(1, 250, 100));
        var w = Anim(_compactW, 320);
        w.Completed += (_, _) =>
        {
            if (token != _pillToken || _expanded || _uploadActive) return;
            Width = _compactW;
            PositionTop();
        };
        Island.BeginAnimation(WidthProperty, w);
    }

    /// <summary>Показывает процент загрузки, затем «Файл установлен» с иконкой файла — в капсуле (или на полке, если остров раскрыт).</summary>
    private async Task AddToShelfWithProgressAsync(string[] paths)
    {
        int run = ++_shelfRun;
        var first = paths.FirstOrDefault(p => File.Exists(p) || Directory.Exists(p));
        if (first == null) { PillHide(); Schedule(1500); return; }

        string label = Path.GetFileName(first.TrimEnd('\\', '/'));
        if (string.IsNullOrEmpty(label)) label = first;
        if (paths.Length > 1) label += Loc.F(" и ещё {0}", paths.Length - 1);
        var icon = _shelf.GetIcon(first);
        bool plural = paths.Length > 1;

        _uploadBusy = true;
        bool inPill = !_expanded;
        Brush blue = new SolidColorBrush(Color.FromRgb(0x4D, 0xA3, 0xFF));
        Brush green = new SolidColorBrush(Color.FromRgb(0x2F, 0xBF, 0x5B));

        void Show(double v, bool done, string? doneTitle = null)
        {
            string pct = $"{(int)Math.Round(v * 100)}%";
            if (inPill && !_expanded)
            {
                if (!_uploadActive) PillShow("", icon, v);
                PillIcon.Source = icon;
                PillIcon.Visibility = Visibility.Visible;
                PillBar.Visibility = Visibility.Visible;
                PillFillScale.ScaleX = v;
                PillFill.Background = done ? green : blue;
                PillText.Text = done ? doneTitle : Loc.F("Загрузка {0}", pct);
            }
            else
            {
                ShelfProgress.Visibility = Visibility.Visible;
                ShelfProgressIcon.Source = icon;
                ShelfProgressName.Text = label;
                ShelfFillScale.ScaleX = v;
                ShelfProgressFill.Background = done ? green : blue;
                ShelfProgressPercent.Visibility = done ? Visibility.Collapsed : Visibility.Visible;
                ShelfProgressPercent.Text = pct;
                ShelfProgressTitle.Text = done ? doneTitle : Loc.T("Загрузка…");
            }
        }

        Show(0, false);
        ShelfDropHint.Visibility = Visibility.Collapsed;

        // Реальная работа — подсчёт размера; анимация идёт не быстрее минимума, чтобы процент был виден.
        var sizeTask = Task.Run(() => MeasureBytes(paths));
        long bytes = 0;
        bool sized = false;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        double durationMs = 900;
        while (true)
        {
            if (run != _shelfRun) return;
            if (sizeTask.IsCompleted && !sized)
            {
                sized = true;
                bytes = sizeTask.Result;
                durationMs = Math.Clamp(900 + bytes / (1024.0 * 1024) * 3, 900, 3000);
            }

            double t = Math.Min(1, sw.ElapsedMilliseconds / durationMs);
            double eased = 1 - Math.Pow(1 - t, 2);
            if (!sized) eased = Math.Min(eased, 0.9); // пока размер ещё считается — не доходим до конца
            Show(eased, false);
            if (t >= 1 && sized) break;
            await Task.Delay(16);
        }

        int added = _shelf.Add(paths);
        BuildShelf();

        string title = Loc.T(added == 0 ? "Уже на полке" : plural ? "Файлы установлены" : "Файл установлен");
        Show(1, true, title);
        var pop = new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(320))
        { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 } };
        if (inPill && _uploadActive)
        {
            PillBadge.Visibility = Visibility.Visible;
            PillBadgeScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            PillBadgeScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        }
        else
        {
            ShelfDoneBadge.Visibility = Visibility.Visible;
            ShelfBadgeScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            ShelfBadgeScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        }

        await Task.Delay(1700);
        if (run != _shelfRun) return;
        _uploadBusy = false;
        ShelfProgress.Visibility = Visibility.Collapsed;
        ShelfDoneBadge.Visibility = Visibility.Collapsed;
        PillHide();
        Schedule(inPill ? 300 : 1500);
    }

    private static long MeasureBytes(IEnumerable<string> paths)
    {
        long total = 0;
        foreach (var p in paths)
        {
            try
            {
                if (File.Exists(p)) total += new FileInfo(p).Length;
                else if (Directory.Exists(p))
                    foreach (var f in new DirectoryInfo(p).EnumerateFiles("*", new EnumerationOptions
                             { RecurseSubdirectories = true, IgnoreInaccessible = true }))
                        total += f.Length;
            }
            catch (Exception ex) { AppLog.Error("MainWindow.MeasureBytes", ex); }
        }
        return total;
    }

    private void ShelfScroll_Wheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        ShelfScroll.ScrollToHorizontalOffset(ShelfScroll.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private void BuildShelf()
    {
        ShelfItems.Children.Clear();
        ShelfEmpty.Visibility = _shelf.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var path in _shelf.Items.ToList())
            ShelfItems.Children.Add(ShelfTile(path));
    }

    private UIElement ShelfTile(string path)
    {
        bool isDir = Directory.Exists(path);
        bool missing = !isDir && !File.Exists(path);

        string name = isDir ? new DirectoryInfo(path).Name : Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrEmpty(name)) name = path;

        var stack = new StackPanel();
        stack.Children.Add(new Image
        {
            Width = 32,
            Height = 32,
            Source = _shelf.GetIcon(path),
            Opacity = missing ? 0.35 : 1
        });
        stack.Children.Add(new TextBlock
        {
            Text = name,
            FontSize = 10.5,
            Foreground = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxHeight = 28,
            Margin = new Thickness(0, 5, 0, 0)
        });

        var tile = new Border
        {
            Width = 76,
            Margin = new Thickness(0, 0, 6, 0),
            Padding = new Thickness(4, 8, 4, 6),
            CornerRadius = new CornerRadius(12),
            Background = ShelfIdle,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = path,
            VerticalAlignment = VerticalAlignment.Center,
            Child = stack
        };

        tile.MouseEnter += (_, _) => tile.Background = ShelfHover;
        tile.MouseLeave += (_, _) => tile.Background = ShelfIdle;
        tile.MouseLeftButtonUp += (_, _) => OpenShelfItem(path);

        var open = new MenuItem { Header = Loc.T("Открыть") };
        open.Click += (_, _) => OpenShelfItem(path);
        var reveal = new MenuItem { Header = Loc.T("Показать в папке") };
        reveal.Click += (_, _) => ShelfService.RevealInExplorer(path);
        var remove = new MenuItem { Header = Loc.T("Убрать с полки") };
        remove.Click += (_, _) => { _shelf.Remove(path); BuildShelf(); };

        var menu = new ContextMenu();
        menu.Items.Add(open);
        menu.Items.Add(reveal);
        menu.Items.Add(new Separator());
        menu.Items.Add(remove);
        tile.ContextMenu = menu;

        return tile;
    }

    private void OpenShelfItem(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            _shelf.Remove(path);
            BuildShelf();
            EnqueueToast(new NotificationInfo(NotificationService.OwnName, Loc.T("Файл не найден"),
                Loc.T("Он удалён или перемещён — убрал его с полки"), null));
            return;
        }

        if (ShelfService.Launch(path, out var error))
            Collapse();
        else
            EnqueueToast(new NotificationInfo(NotificationService.OwnName, Loc.T("Не удалось запустить"), Loc.T(error ?? ""), null));
    }
}
