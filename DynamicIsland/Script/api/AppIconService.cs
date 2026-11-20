using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Xml.Linq;
using Microsoft.Win32;

namespace DynamicIsland;

/// <summary>
/// Иконка приложения по его AppUserModelID (AUMID) — для уведомлений, у которых Windows не отдаёт логотип.
/// Пробует по очереди: Store-пакет → реестр → exe/ярлык → запущенный процесс. Результат — PNG-байты (или null).
/// </summary>
public static class AppIconService
{
    private static readonly ConcurrentDictionary<string, byte[]?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static byte[]? Get(string? aumid)
    {
        if (string.IsNullOrWhiteSpace(aumid)) return null;
        return Cache.GetOrAdd(aumid, Resolve);
    }

    private static byte[]? Resolve(string id)
    {
        try
        {
            return (id.Contains('!') ? FromStorePackage(id) : null)
                   ?? FromRegistry(id)
                   ?? FromPath(id)
                   ?? FromStartMenu(NotificationService.AppName(id), id)
                   ?? FromProcess(NotificationService.AppName(id), id);
        }
        catch (Exception ex)
        {
            AppLog.Error("AppIconService.Resolve", ex);
            return null;
        }
    }

    // ---------- Store / UWP ----------
    private static byte[]? FromStorePackage(string id)
    {
        try
        {
            string family = id[..id.IndexOf('!')];
            var pkg = new Windows.Management.Deployment.PackageManager().FindPackagesForUser("", family).FirstOrDefault();
            string? root = pkg?.InstalledPath;
            if (string.IsNullOrEmpty(root)) return null;

            var manifest = XDocument.Load(Path.Combine(root, "AppxManifest.xml"));
            var visual = manifest.Descendants().FirstOrDefault(e => e.Name.LocalName == "VisualElements");
            string?[] logos =
            {
                (string?)visual?.Attribute("Square44x44Logo"),
                (string?)visual?.Attribute("Square150x150Logo"),
                (string?)manifest.Descendants().FirstOrDefault(e => e.Name.LocalName == "Logo")
            };

            foreach (var logo in logos)
            {
                if (string.IsNullOrEmpty(logo)) continue;
                string full = Path.Combine(root, logo.Replace('/', '\\'));
                string dir = Path.GetDirectoryName(full) ?? root;
                if (!Directory.Exists(dir)) continue;

                string stem = Path.GetFileNameWithoutExtension(full);
                var files = Directory.GetFiles(dir, stem + "*.png");
                string[] prefer = { "targetsize-48_altform-unplated", "targetsize-48", "targetsize-32_altform-unplated", "targetsize-44", "scale-200", "scale-100" };
                string? best = prefer.Select(p => files.FirstOrDefault(f => f.Contains(p, StringComparison.OrdinalIgnoreCase))).FirstOrDefault(f => f != null)
                               ?? files.FirstOrDefault();
                if (best != null) return File.ReadAllBytes(best);
            }
        }
        catch (Exception ex) { AppLog.Error("AppIconService.FromStorePackage", ex); }
        return null;
    }

    // ---------- Реестр: HKCR\AppUserModelId\<id> ----------
    private static byte[]? FromRegistry(string id)
    {
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(@"AppUserModelId\" + id);
            if (key == null) return null;

            foreach (var name in new[] { "IconUri", "IconPath" })
            {
                if (key.GetValue(name) is not string value || value.Length == 0) continue;
                var bytes = FromPath(value);
                if (bytes != null) return bytes;
            }
        }
        catch (Exception ex) { AppLog.Error("AppIconService.FromRegistry", ex); }
        return null;
    }

    // ---------- Путь к exe / ico / png / ярлыку ----------
    private static byte[]? FromPath(string value)
    {
        string path = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
        int comma = path.LastIndexOf(',');
        if (comma > 2 && int.TryParse(path[(comma + 1)..], out _)) path = path[..comma].Trim('"'); // "file.dll,-123"
        if (!File.Exists(path)) return null;

        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp") return File.ReadAllBytes(path);
        if (ext is ".exe" or ".ico" or ".dll" or ".lnk") return IconToPng(path);
        return null;
    }

    private static byte[]? IconToPng(string path)
    {
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon == null) return null;
            using var bmp = icon.ToBitmap();
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            AppLog.Error("AppIconService.IconToPng", ex);
            return null;
        }
    }

    // ---------- Ярлык в меню «Пуск» с тем же именем ----------
    private static byte[]? FromStartMenu(string appName, string id)
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu)
        };

        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                foreach (var lnk in Directory.EnumerateFiles(Path.Combine(root, "Programs"), "*.lnk", SearchOption.AllDirectories))
                {
                    string n = Path.GetFileNameWithoutExtension(lnk);
                    if (n.Equals(appName, StringComparison.OrdinalIgnoreCase) || n.Equals(id, StringComparison.OrdinalIgnoreCase))
                        return IconToPng(lnk);
                }
            }
            catch { /* нет доступа к какой-то папке — идём дальше */ }
        }
        return null;
    }

    // ---------- Запущенный процесс с таким именем ----------
    private static byte[]? FromProcess(string appName, string id)
    {
        foreach (var name in new[] { appName, id }.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct())
        {
            foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(name)))
            {
                try
                {
                    string? file = p.MainModule?.FileName;
                    if (file != null) return IconToPng(file);
                }
                catch { /* нет доступа к процессу */ }
                finally { p.Dispose(); }
            }
        }
        return null;
    }
}
