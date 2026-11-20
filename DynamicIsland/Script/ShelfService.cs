using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DynamicIsland;

/// <summary>
/// Полка: список ссылок на файлы, папки и программы, которые перетащили на остров.
/// Файлы не копируются — хранятся только пути (%AppData%\DynamicIsland\shelf.json).
/// </summary>
public sealed class ShelfService
{
    private const int MaxItems = 30;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _file;
    private readonly List<string> _paths = new();
    private readonly Dictionary<string, ImageSource?> _icons = new(StringComparer.OrdinalIgnoreCase);

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DynamicIsland", "shelf.json");

    public IReadOnlyList<string> Items => _paths;

    /// <param name="filePath">Где хранить список. По умолчанию — %AppData%; подменяется в тестах.</param>
    public ShelfService(string? filePath = null)
    {
        _file = filePath ?? FilePath;
        try
        {
            if (!File.Exists(_file)) return;
            var list = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_file), Options);
            if (list == null) return;
            _paths.AddRange(list
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxItems));
        }
        catch (Exception ex) { AppLog.Error("ShelfService.ShelfService", ex); }
    }

    /// <summary>Добавляет пути на полку. Возвращает, сколько реально добавлено.</summary>
    public int Add(IEnumerable<string> paths)
    {
        int added = 0;
        foreach (var raw in paths)
        {
            if (_paths.Count >= MaxItems) break;
            if (string.IsNullOrWhiteSpace(raw)) continue;
            if (!File.Exists(raw) && !Directory.Exists(raw)) continue;
            if (_paths.Contains(raw, StringComparer.OrdinalIgnoreCase)) continue;

            _paths.Add(raw);
            added++;
        }

        if (added > 0) Save();
        return added;
    }

    public void Remove(string path)
    {
        if (_paths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)) > 0)
        {
            _icons.Remove(path);
            Save();
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(_paths, Options));
        }
        catch (Exception ex) { AppLog.Error("ShelfService.Save", ex); }
    }

    /// <summary>Запускает файл/программу/папку так же, как двойной клик в проводнике.</summary>
    public static bool Launch(string path, out string? error)
    {
        error = null;
        try
        {
            var psi = new ProcessStartInfo(path) { UseShellExecute = true };
            if (File.Exists(path))
                psi.WorkingDirectory = Path.GetDirectoryName(path) ?? "";
            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error("ShelfService.Launch", ex);
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Показывает файл в проводнике.</summary>
    public static void RevealInExplorer(string path)
    {
        try
        {
            if (File.Exists(path) || Directory.Exists(path))
                Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
        catch (Exception ex) { AppLog.Error("ShelfService.RevealInExplorer", ex); }
    }

    // ---------- Иконки (системные, как в проводнике) ----------
    public ImageSource? GetIcon(string path)
    {
        if (_icons.TryGetValue(path, out var cached)) return cached;
        var icon = LoadIcon(path);
        _icons[path] = icon;
        return icon;
    }

    private static ImageSource? LoadIcon(string path)
    {
        try
        {
            var info = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_LARGEICON;
            if (!File.Exists(path) && !Directory.Exists(path)) flags |= SHGFI_USEFILEATTRIBUTES;

            SHGetFileInfo(path, FILE_ATTRIBUTE_NORMAL, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
            if (info.hIcon == IntPtr.Zero) return null;

            try
            {
                var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                return src;
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("ShelfService.LoadIcon", ex);
            return null;
        }
    }

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
