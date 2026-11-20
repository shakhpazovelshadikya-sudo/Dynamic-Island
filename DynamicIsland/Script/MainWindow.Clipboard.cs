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

/// <summary>Вкладка «Буфер обмена».</summary>
public partial class MainWindow
{
    private readonly ClipboardService _clip = new();
    private static readonly Brush ClipDone = Frozen(Color.FromArgb(0x55, 0x5B, 0xD6, 0x7F));
    private const int WM_CLIPBOARDUPDATE = 0x031D;

    // ---------- Буфер обмена ----------
    private void BuildClipboard()
    {
        ClipItems.Children.Clear();
        var items = _clip.Items;

        ClipEmpty.Text = _settings.ClipboardHistory
            ? Loc.T("Скопируй текст — он появится здесь. Клик по записи вернёт её в буфер")
            : Loc.T("История буфера выключена: настройки → «Остров»");
        ClipEmpty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ClipCount.Text = items.Count > 0 ? items.Count.ToString() : "";

        foreach (var item in items.ToList())
            ClipItems.Children.Add(MakeClipRow(item));
    }

    private static string ClipPreview(string text)
    {
        string one = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return one.Length > 200 ? one[..200] + "…" : one;
    }

    private Border MakeClipRow(ClipItem item)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new TextBlock
        {
            Text = ClipPreview(item.Text),
            Foreground = Brushes.White,
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        });

        var time = new TextBlock
        {
            Text = item.At.ToString("HH:mm"),
            Foreground = Frozen(Color.FromRgb(0x77, 0x77, 0x77)),
            FontSize = 10,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(time, 1);
        grid.Children.Add(time);

        string tip = item.Text.Length > 400 ? item.Text[..400] + "…" : item.Text;
        var row = new Border
        {
            Background = ShelfIdle,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(0, 0, 0, 4),
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = grid,
            ToolTip = tip
        };

        row.MouseEnter += (_, _) => row.Background = ShelfHover;
        row.MouseLeave += (_, _) => row.Background = ShelfIdle;
        row.MouseLeftButtonUp += (_, _) => CopyClip(item, row);

        var menu = new ContextMenu();
        var copy = new MenuItem { Header = Loc.T("Копировать") };
        copy.Click += (_, _) => CopyClip(item, row);
        var remove = new MenuItem { Header = Loc.T("Удалить") };
        remove.Click += (_, _) => _clip.Remove(item);
        var clear = new MenuItem { Header = Loc.T("Очистить всё") };
        clear.Click += (_, _) => _clip.Clear();
        menu.Items.Add(copy);
        menu.Items.Add(remove);
        menu.Items.Add(clear);
        TrackMenu(menu);
        row.ContextMenu = menu;

        return row;
    }

    private async void CopyClip(ClipItem item, Border row)
    {
        if (!_clip.CopyBack(item)) return;
        row.Background = ClipDone;
        await Task.Delay(500);
        row.Background = row.IsMouseOver ? ShelfHover : ShelfIdle;
    }

    private void ClipClear_Click(object sender, RoutedEventArgs e) => _clip.Clear();
}
