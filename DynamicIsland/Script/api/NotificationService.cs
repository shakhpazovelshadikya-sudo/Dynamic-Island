using System.IO;
using System.Text;
using System.Xml.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using Windows.Storage.Streams;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace DynamicIsland;

/// <summary>Уведомление другого приложения: кто прислал, заголовок, текст и иконка приложения.</summary>
public record NotificationInfo(string App, string Title, string Body, byte[]? Icon);

public enum NotifMode { Starting, Official, Database, Unavailable }

/// <summary>
/// Читает уведомления Windows (тосты).
/// 1) Официальный UserNotificationListener — работает только у приложений с package identity (MSIX).
/// 2) Если Windows отказала (обычный exe без упаковки) — читаем базу уведомлений Windows
///    (%LocalAppData%\Microsoft\Windows\Notifications\wpndatabase.db) в режиме «только чтение».
///    Ничего устанавливать и разрешать не нужно.
/// </summary>
public sealed class NotificationService
{
    public const string OwnName = "Dynamic Island";

    /// <summary>Текущий способ получения уведомлений — показывается в настройках.</summary>
    public static NotifMode Mode { get; private set; } = NotifMode.Starting;
    public static string ModeDetail { get; private set; } = "";

    private static void SetMode(NotifMode mode, string detail = "")
    {
        if (Mode != mode || ModeDetail != detail)
            AppLog.Info($"Уведомления: статус {mode}{(detail.Length > 0 ? " — " + detail : "")}");
        Mode = mode;
        ModeDetail = detail;
    }

    private readonly HashSet<uint> _seen = new();
    private readonly HashSet<long> _seenDb = new();

    public event Action<NotificationInfo>? Received;
    public event Action<string>? AccessProblem;

    public async Task RunAsync(Func<bool> isEnabled)
    {
        if (await TryRunListenerAsync(isEnabled)) return; // цикл бесконечный; сюда не дойдём, если API доступен
        await RunDatabaseAsync(isEnabled);
    }

    // ---------- 1. Официальный API ----------
    /// <returns>false — доступа нет (нужен запасной способ). При успехе работает бесконечно.</returns>
    private async Task<bool> TryRunListenerAsync(Func<bool> isEnabled)
    {
        UserNotificationListener listener;
        try
        {
            listener = UserNotificationListener.Current;
            var status = await listener.RequestAccessAsync();
            if (status != UserNotificationListenerAccessStatus.Allowed) return false;
            SetMode(NotifMode.Official);
            AppLog.Info("Уведомления: официальный UserNotificationListener");
        }
        catch (Exception ex)
        {
            AppLog.Error("NotificationService.TryRunListenerAsync", ex);
            return false;
        }

        bool first = true;
        while (true)
        {
            try
            {
                var list = await listener.GetNotificationsAsync(NotificationKinds.Toast);
                var currentIds = new HashSet<uint>();

                foreach (var n in list.OrderBy(x => x.CreationTime))
                {
                    currentIds.Add(n.Id);
                    if (!_seen.Add(n.Id)) continue;       // уже видели
                    if (first || !isEnabled()) continue;   // старые при запуске и выключенный режим не показываем

                    var info = await ParseAsync(n);
                    if (info != null && info.App != OwnName)
                        Received?.Invoke(info);
                }

                _seen.IntersectWith(currentIds); // не копим старые id
                first = false;
            }
            catch (Exception ex) { AppLog.Error("NotificationService.TryRunListenerAsync", ex); }

            await Task.Delay(1500);
        }
    }

    private static async Task<NotificationInfo?> ParseAsync(UserNotification n)
    {
        try
        {
            var binding = n.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
            if (binding == null) return null;

            var texts = binding.GetTextElements()
                .Select(t => t.Text)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            string title = texts.FirstOrDefault() ?? "";
            string body = string.Join("\n", texts.Skip(1));
            if (title.Length == 0 && body.Length == 0) return null;

            string app = n.AppInfo?.DisplayInfo?.DisplayName ?? "";
            byte[]? icon = n.AppInfo != null ? await LoadLogoAsync(n.AppInfo) : null;
            icon ??= await Task.Run(() => AppIconService.Get(n.AppInfo?.AppUserModelId));

            return new NotificationInfo(app, title, body, icon);
        }
        catch (Exception ex)
        {
            AppLog.Error("NotificationService.ParseAsync", ex);
            return null;
        }
    }

    private static async Task<byte[]?> LoadLogoAsync(Windows.ApplicationModel.AppInfo app)
    {
        try
        {
            var logo = app.DisplayInfo.GetLogo(new Windows.Foundation.Size(64, 64));
            using var stream = await logo.OpenReadAsync();
            var bytes = new byte[stream.Size];
            using var reader = new DataReader(stream);
            await reader.LoadAsync((uint)stream.Size);
            reader.ReadBytes(bytes);
            return bytes.Length > 0 ? bytes : null;
        }
        catch (Exception ex)
        {
            AppLog.Error("NotificationService.LoadLogoAsync", ex);
            return null;
        }
    }

    // ---------- 2. Запасной способ: база уведомлений Windows ----------
    private sealed record DbRow(long Id, byte[] Payload, long Arrival, string PrimaryId);

    private async Task RunDatabaseAsync(Func<bool> isEnabled)
    {
        string src = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows", "Notifications", "wpndatabase.db");

        if (!File.Exists(src))
        {
            SetMode(NotifMode.Unavailable, "не найдена база уведомлений Windows");
            AccessProblem?.Invoke(Loc.T("Не найдена база уведомлений Windows, показ уведомлений недоступен."));
            while (!File.Exists(src)) await Task.Delay(TimeSpan.FromSeconds(30)); // вдруг появится позже
        }

        SetMode(NotifMode.Database);
        AppLog.Info("Уведомления: чтение базы wpndatabase.db (официальный API недоступен)");
        string tmpDir = Path.Combine(Path.GetTempPath(), "DynamicIsland");
        string tmp = Path.Combine(tmpDir, "wpn.db");
        try { Directory.CreateDirectory(tmpDir); } catch (Exception ex) { AppLog.Error("NotificationService.RunDatabaseAsync", ex); }

        string lastStamp = "";
        bool first = true;
        int failures = 0;

        while (true)
        {
            try
            {
                string stamp = Stamp(src);
                if (stamp != lastStamp)
                {
                    lastStamp = stamp;
                    var rows = await Task.Run(() => ReadRows(src, tmp));

                    var currentIds = new HashSet<long>();
                    foreach (var row in rows.OrderBy(r => r.Arrival))
                    {
                        currentIds.Add(row.Id);
                        if (!_seenDb.Add(row.Id)) continue;
                        if (first || !isEnabled()) continue;

                        var info = ParseXml(row);
                        if (info != null && info.App != OwnName)
                            Received?.Invoke(info);
                    }

                    _seenDb.IntersectWith(currentIds);
                    first = false;
                }

                if (failures > 0 || Mode != NotifMode.Database) SetMode(NotifMode.Database); // восстановились после сбоя
                failures = 0;
            }
            catch (Exception ex)
            {
                AppLog.Error("NotificationService.RunDatabaseAsync", ex);
                lastStamp = ""; // копия могла получиться «рваной» — на следующем цикле читаем заново
                if (++failures >= 5)
                {
                    // частая причина — Windows изменила структуру базы после обновления
                    SetMode(NotifMode.Unavailable, ex.Message);
                    if (failures == 5) AccessProblem?.Invoke(Loc.T("Не удалось прочитать базу уведомлений Windows."));
                }
            }

            await Task.Delay(1000);
        }
    }

    /// <summary>Отпечаток базы: меняется при каждой записи (в WAL-файл или в саму базу).</summary>
    private static string Stamp(string db)
    {
        string wal = db + "-wal";
        long dbTime = File.GetLastWriteTimeUtc(db).Ticks;
        long walTime = File.Exists(wal) ? File.GetLastWriteTimeUtc(wal).Ticks : 0;
        long walLen = File.Exists(wal) ? new FileInfo(wal).Length : 0;
        return $"{dbTime}|{walTime}|{walLen}";
    }

    private static List<DbRow> ReadRows(string src, string tmp)
    {
        foreach (var f in new[] { tmp, tmp + "-wal", tmp + "-shm" })
        {
            try { if (File.Exists(f)) File.Delete(f); } catch { }
        }

        CopyShared(src, tmp);
        if (File.Exists(src + "-wal")) CopyShared(src + "-wal", tmp + "-wal");

        var rows = new List<DbRow>();
        using var conn = new SqliteConnection($"Data Source={tmp};Pooling=False");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT n.Id, n.Payload, n.ArrivalTime, h.PrimaryId " +
            "FROM Notification n LEFT JOIN NotificationHandler h ON h.RecordId = n.HandlerId " +
            "WHERE n.Type = 'toast'";

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            if (r.IsDBNull(1)) continue;
            object raw = r.GetValue(1);
            byte[] payload = raw as byte[] ?? Encoding.UTF8.GetBytes(raw.ToString() ?? "");
            long arrival = r.IsDBNull(2) ? 0 : r.GetInt64(2);
            string primary = r.IsDBNull(3) ? "" : r.GetString(3);
            rows.Add(new DbRow(r.GetInt64(0), payload, arrival, primary));
        }

        return rows;
    }

    private static void CopyShared(string from, string to)
    {
        using var input = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var output = new FileStream(to, FileMode.Create, FileAccess.Write);
        input.CopyTo(output);
    }

    private static NotificationInfo? ParseXml(DbRow row)
    {
        try
        {
            string xml = Encoding.UTF8.GetString(row.Payload).TrimStart('\uFEFF', '\0', ' ', '\r', '\n');
            var doc = XDocument.Parse(xml);

            var binding = doc.Descendants("binding").FirstOrDefault(b => (string?)b.Attribute("template") == "ToastGeneric")
                          ?? doc.Descendants("binding").FirstOrDefault();
            if (binding == null) return null;

            var texts = binding.Elements("text")
                .Where(t => (string?)t.Attribute("placement") != "attribution") // «через …» не нужен
                .Select(t => t.Value)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            string title = texts.FirstOrDefault() ?? "";
            string body = string.Join("\n", texts.Skip(1));
            if (title.Length == 0 && body.Length == 0) return null;

            return new NotificationInfo(AppName(row.PrimaryId), title, body, AppIconService.Get(row.PrimaryId));
        }
        catch (Exception ex)
        {
            AppLog.Error("NotificationService.ParseXml", ex);
            return null;
        }
    }

    /// <summary>Человеческое имя приложения по его идентификатору (AUMID).</summary>
    internal static string AppName(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "";

        // классические приложения регистрируют имя в реестре
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(@"AppUserModelId\" + id);
            if (key?.GetValue("DisplayName") is string name && name.Length > 0 && !name.StartsWith('@'))
                return name;
        }
        catch (Exception ex) { AppLog.Error("NotificationService.AppName", ex); }

        // идентификатор-путь к exe
        if (id.Contains('\\') || id.Contains('/'))
            return Path.GetFileNameWithoutExtension(id);

        // Store-приложения: "Publisher.AppName_hash!App" -> "AppName"
        int bang = id.IndexOf('!');
        if (bang > 0) id = id[..bang];
        int underscore = id.IndexOf('_');
        if (underscore > 0) id = id[..underscore];
        int dot = id.LastIndexOf('.');
        if (dot >= 0 && dot < id.Length - 1) id = id[(dot + 1)..];
        return id;
    }
}
