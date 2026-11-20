using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace DynamicIsland;

/// <summary>Иконка в трее: показать/скрыть остров, автозапуск, выход.</summary>
public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly System.Windows.Window _window;
    private readonly Forms.ToolStripMenuItem _toggleItem;
    private readonly Forms.ToolStripMenuItem _autoItem;
    private readonly Icon _appIcon;

    public TrayService(System.Windows.Window window, Action refreshWeather, Action openSettings)
    {
        _window = window;
        _appIcon = LoadIcon();

        _toggleItem = new Forms.ToolStripMenuItem(Loc.T("Скрыть остров"));
        _toggleItem.Click += (_, _) => ToggleWindow();

        _autoItem = new Forms.ToolStripMenuItem(Loc.T("Запускать вместе с Windows"))
        {
            CheckOnClick = false,
            Checked = AutoStart.IsEnabled()
        };
        _autoItem.Click += (_, _) =>
        {
            AutoStart.Set(!AutoStart.IsEnabled());
            _autoItem.Checked = AutoStart.IsEnabled();
        };

        var weatherItem = new Forms.ToolStripMenuItem(Loc.T("Обновить погоду"));
        weatherItem.Click += (_, _) => refreshWeather();

        var cityItem = new Forms.ToolStripMenuItem(Loc.T("Настройки…"));
        cityItem.Click += (_, _) => openSettings();

        var logItem = new Forms.ToolStripMenuItem(Loc.T("Открыть журнал ошибок"));
        logItem.Click += (_, _) => AppLog.Open();

        var exitItem = new Forms.ToolStripMenuItem(Loc.T("Выход"));
        exitItem.Click += (_, _) => System.Windows.Application.Current.Shutdown();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_toggleItem);
        menu.Items.Add(weatherItem);
        menu.Items.Add(cityItem);
        menu.Items.Add(logItem);
        menu.Items.Add(_autoItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);
        menu.Opening += (_, _) => _autoItem.Checked = AutoStart.IsEnabled();

        _icon = new Forms.NotifyIcon
        {
            Icon = _appIcon,
            Text = "Dynamic Island",
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) ToggleWindow();
        };
    }

    private void ToggleWindow()
    {
        bool show = _window.Visibility != System.Windows.Visibility.Visible;
        _window.Visibility = show ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;
        _toggleItem.Text = Loc.T(show ? "Скрыть остров" : "Показать остров");
    }

    // Берём иконку, вшитую в exe (DynamicIsland.ico); если не вышло — рисуем запасную
    private static Icon LoadIcon()
    {
        try
        {
            using var stream = typeof(TrayService).Assembly.GetManifestResourceStream("DynamicIsland.ico");
            if (stream != null)
                return new Icon(stream, Forms.SystemInformation.SmallIconSize);
        }
        catch (Exception ex) { AppLog.Error("TrayService.LoadIcon", ex); }
        return CreateIcon();
    }

    // Запасная иконка рисуется кодом, чтобы не таскать отдельный .ico файл
    private static Icon CreateIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var r = new RectangleF(2, 9, 28, 14);
            float d = r.Height;
            using var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 90, 180);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 180);
            path.CloseFigure();

            using var fill = new SolidBrush(Color.FromArgb(255, 30, 30, 30));
            using var border = new Pen(Color.White, 2f);
            g.FillPath(fill, path);
            g.DrawPath(border, path);
            g.FillEllipse(Brushes.White, 21, 13, 6, 6);
        }

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _appIcon.Dispose();
    }
}
