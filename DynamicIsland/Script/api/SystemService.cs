using System.Threading;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Devices.Radios;

namespace DynamicIsland;

public enum RadioStatus { Missing, Denied, Off, On }

/// <summary>Системные действия: Wi-Fi / Bluetooth (радиомодули Windows), VPN (rasdial), питание, громкость.</summary>
public static class SystemService
{
    // ---------- Wi-Fi / Bluetooth ----------
    public static async Task<RadioStatus> GetRadioAsync(RadioKind kind)
    {
        try
        {
            if (await Radio.RequestAccessAsync() != RadioAccessStatus.Allowed) return RadioStatus.Denied;
            var radios = (await Radio.GetRadiosAsync()).Where(r => r.Kind == kind).ToList();
            if (radios.Count == 0) return RadioStatus.Missing;
            return radios.Any(r => r.State == RadioState.On) ? RadioStatus.On : RadioStatus.Off;
        }
        catch (Exception ex)
        {
            AppLog.Error("SystemService.GetRadioAsync", ex);
            return RadioStatus.Missing;
        }
    }

    public static async Task<RadioStatus> ToggleRadioAsync(RadioKind kind)
    {
        try
        {
            var status = await GetRadioAsync(kind);
            if (status is RadioStatus.Missing or RadioStatus.Denied) return status;

            var target = status == RadioStatus.On ? RadioState.Off : RadioState.On;
            foreach (var r in (await Radio.GetRadiosAsync()).Where(r => r.Kind == kind))
                await r.SetStateAsync(target);
        }
        catch (Exception ex) { AppLog.Error("SystemService.ToggleRadioAsync", ex); }

        return await GetRadioAsync(kind);
    }

    // ---------- VPN (встроенные профили Windows) ----------
    /// <summary>Имена VPN-профилей из rasphone.pbk (пользовательского и общего). Только Type=2 (VPN).</summary>
    public static List<string> VpnProfiles()
    {
        var result = new List<string>();
        string tail = Path.Combine("Microsoft", "Network", "Connections", "Pbk", "rasphone.pbk");
        var files = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), tail),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), tail)
        };

        foreach (var file in files)
        {
            try
            {
                if (!File.Exists(file)) continue;
                string? section = null;
                bool isVpn = false;

                foreach (var raw in ReadPbk(file).Split('\n'))
                {
                    string line = raw.Trim();
                    if (line.StartsWith('[') && line.EndsWith(']'))
                    {
                        if (section != null && isVpn && !result.Contains(section)) result.Add(section);
                        section = line[1..^1];
                        isVpn = false;
                    }
                    else if (line.Equals("Type=2", StringComparison.OrdinalIgnoreCase)) isVpn = true;
                }
                if (section != null && isVpn && !result.Contains(section)) result.Add(section);
            }
            catch (Exception ex) { AppLog.Error("SystemService.VpnProfiles", ex); }
        }
        return result;
    }

    private static string ReadPbk(string path)
    {
        byte[] b = File.ReadAllBytes(path);
        if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) return Encoding.Unicode.GetString(b);
        try { return new UTF8Encoding(false, true).GetString(b); }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage).GetString(b);
        }
    }

    /// <summary>Имена активных подключений: rasdial без аргументов печатает их построчно (язык не важен).</summary>
    public static async Task<HashSet<string>> VpnActiveAsync()
    {
        var (_, output) = await RunAsync("rasdial", Array.Empty<string>(), 8000);
        return output.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0)
                     .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static async Task<(bool Ok, string Message)> VpnSetAsync(string profile, bool connect)
    {
        var args = connect ? new[] { profile } : new[] { profile, "/disconnect" };
        var (code, output) = await RunAsync("rasdial", args, 45000);
        if (code == 0) return (true, "");

        string msg = output.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? Loc.T("Не удалось выполнить команду");
        return (false, msg);
    }

    private static async Task<(int Code, string Output)> RunAsync(string file, string[] args, int timeoutMs)
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var enc = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage); // консоль Windows пишет в OEM-кодировке
            var psi = new ProcessStartInfo(file)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = enc,
                StandardErrorEncoding = enc
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var p = Process.Start(psi);
            if (p == null) return (-1, Loc.T("Не удалось запустить") + " " + file);

            var outTask = p.StandardOutput.ReadToEndAsync();
            var errTask = p.StandardError.ReadToEndAsync();
            using var cts = new CancellationTokenSource(timeoutMs);
            try { await p.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException)
            {
                try { p.Kill(true); } catch { }
                return (-1, Loc.T("Превышено время ожидания"));
            }
            return (p.ExitCode, await outTask + await errTask);
        }
        catch (Exception ex)
        {
            AppLog.Error("SystemService.RunAsync", ex);
            return (-1, ex.Message);
        }
    }

    // ---------- Питание ----------
    [DllImport("user32.dll")] private static extern bool LockWorkStation();
    [DllImport("powrprof.dll")] private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    public static void Lock() => LockWorkStation();

    /// <summary>Режим сна (не гибернация, даже если гибернация включена).</summary>
    public static void Sleep() => SetSuspendState(false, false, false);

    // без /f: программы с несохранёнными данными сами спросят, закрывать ли их
    public static void Shutdown() => Shell("/s /t 0");
    public static void Restart() => Shell("/r /t 0");

    private static void Shell(string args) =>
        Process.Start(new ProcessStartInfo("shutdown", args) { UseShellExecute = false, CreateNoWindow = true });

    // ---------- Громкость (медиаклавиши — Windows сама покажет индикатор) ----------
    public const byte VolumeMute = 0xAD, VolumeDown = 0xAE, VolumeUp = 0xAF;

    [DllImport("user32.dll")] private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

    public static void VolumeKey(byte vk, int times = 1)
    {
        for (int i = 0; i < times; i++)
        {
            keybd_event(vk, 0, 0, UIntPtr.Zero);
            keybd_event(vk, 0, 2, UIntPtr.Zero); // KEYEVENTF_KEYUP
        }
    }
}
