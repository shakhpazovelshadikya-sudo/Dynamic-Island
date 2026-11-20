using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Windows.Devices.Radios;

namespace DynamicIsland;

/// <summary>Вкладка «Система»: Wi-Fi, Bluetooth, VPN, блокировка, сон, перезагрузка, выключение, громкость.</summary>
public partial class MainWindow
{
    private DateTime _sysRefreshedAt = DateTime.MinValue;
    private bool _sysBusy, _vpnBusy;
    private List<string> _vpnProfiles = new();
    private HashSet<string> _vpnActive = new(StringComparer.OrdinalIgnoreCase);

    // Выключение и перезагрузка — с подтверждением вторым кликом
    private readonly DispatcherTimer _armTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private Button? _armedBtn;
    private TextBlock? _armedLabel;
    private string _armedNormal = "";

    private async Task RefreshSystemAsync()
    {
        if (_sysBusy) return;
        _sysBusy = true;
        try
        {
            SetRadioTile(TileWifi, WifiState, await SystemService.GetRadioAsync(RadioKind.WiFi));
            SetRadioTile(TileBluetooth, BtState, await SystemService.GetRadioAsync(RadioKind.Bluetooth));

            _vpnProfiles = await Task.Run(SystemService.VpnProfiles);
            if (_vpnProfiles.Count > 0 && !_vpnBusy)
            {
                var active = await SystemService.VpnActiveAsync();
                _vpnActive = _vpnProfiles.Where(active.Contains).ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            UpdateVpnTile();
            _sysRefreshedAt = DateTime.Now;
        }
        catch (Exception ex) { AppLog.Error("MainWindow.RefreshSystemAsync", ex); }
        finally { _sysBusy = false; }
    }

    // ---------- Wi-Fi / Bluetooth ----------
    private static void SetRadioTile(Button tile, TextBlock state, RadioStatus s)
    {
        state.Text = s switch
        {
            RadioStatus.On => Loc.T("Вкл"),
            RadioStatus.Off => Loc.T("Выкл"),
            RadioStatus.Denied => Loc.T("Нет доступа"),
            _ => Loc.T("Нет модуля")
        };
        tile.Tag = s == RadioStatus.On ? "on" : null;
        tile.IsEnabled = s is RadioStatus.On or RadioStatus.Off;
    }

    private async void TileWifi_Click(object sender, RoutedEventArgs e) => await ToggleRadioAsync(RadioKind.WiFi, TileWifi, WifiState, "Wi-Fi");
    private async void TileBluetooth_Click(object sender, RoutedEventArgs e) => await ToggleRadioAsync(RadioKind.Bluetooth, TileBluetooth, BtState, "Bluetooth");

    private async Task ToggleRadioAsync(RadioKind kind, Button tile, TextBlock state, string name)
    {
        tile.IsEnabled = false;
        state.Text = "…";
        var status = await SystemService.ToggleRadioAsync(kind);
        SetRadioTile(tile, state, status);

        if (status == RadioStatus.Denied)
            EnqueueToast(new NotificationInfo(NotificationService.OwnName, name, Loc.T("Windows не разрешила управлять радиомодулем"), null));
    }

    private void OpenWifiSettings_Click(object sender, RoutedEventArgs e) => OpenUri("ms-settings:network-wifi");
    private void OpenBluetoothSettings_Click(object sender, RoutedEventArgs e) => OpenUri("ms-settings:bluetooth");

    private static void OpenUri(string uri)
    {
        try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); }
        catch (Exception ex) { AppLog.Error("MainWindow.OpenUri", ex); }
    }

    // ---------- VPN ----------
    private string? CurrentVpn() =>
        _vpnProfiles.FirstOrDefault(p => p == _settings.VpnProfile) ?? _vpnProfiles.FirstOrDefault();

    private void UpdateVpnTile()
    {
        if (_vpnProfiles.Count == 0)
        {
            VpnState.Text = Loc.T("Нет профилей");
            TileVpn.Tag = null;
            TileVpn.ToolTip = Loc.T("Встроенных VPN-профилей Windows нет. Клик — открыть параметры VPN");
        }
        else
        {
            bool on = _vpnActive.Count > 0;
            VpnState.Text = _vpnBusy ? VpnState.Text : on ? Loc.T("Вкл") : Loc.T("Выкл");
            TileVpn.Tag = on ? "on" : null;
            TileVpn.ToolTip = $"VPN: {(on ? string.Join(", ", _vpnActive) : CurrentVpn())}\n" + Loc.T("ПКМ — выбрать профиль");
        }
        TileVpn.IsEnabled = !_vpnBusy;
    }

    private async void TileVpn_Click(object sender, RoutedEventArgs e)
    {
        if (_vpnBusy) return;
        if (_vpnProfiles.Count == 0) { OpenUri("ms-settings:network-vpn"); return; }

        _vpnBusy = true;
        bool disconnecting = _vpnActive.Count > 0;
        VpnState.Text = Loc.T(disconnecting ? "Отключение…" : "Подключение…");
        TileVpn.IsEnabled = false;

        try
        {
            if (disconnecting)
            {
                foreach (var name in _vpnActive.ToList()) await SystemService.VpnSetAsync(name, false);
            }
            else if (CurrentVpn() is { } profile)
            {
                var (ok, msg) = await SystemService.VpnSetAsync(profile, true);
                if (!ok) EnqueueToast(new NotificationInfo(NotificationService.OwnName, $"VPN «{profile}»", Loc.T(msg), null));
            }
        }
        catch (Exception ex) { AppLog.Error("MainWindow.TileVpn_Click", ex); }
        finally { _vpnBusy = false; }

        _vpnActive.Clear();
        await RefreshSystemAsync();
    }

    private void TileVpn_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        VpnMenu.Items.Clear();
        string? current = CurrentVpn();

        foreach (var p in _vpnProfiles)
        {
            var item = new MenuItem { Header = p, IsCheckable = true, IsChecked = p == current };
            item.Click += (_, _) =>
            {
                var s = SettingsService.Load();
                s.VpnProfile = p;
                SettingsService.Save(s);
                _settings.VpnProfile = p;
                UpdateVpnTile();
            };
            VpnMenu.Items.Add(item);
        }

        if (_vpnProfiles.Count > 0) VpnMenu.Items.Add(new Separator());
        var open = new MenuItem { Header = Loc.T("Параметры VPN…") };
        open.Click += (_, _) => OpenUri("ms-settings:network-vpn");
        VpnMenu.Items.Add(open);
    }

    // ---------- Питание ----------
    private void Lock_Click(object sender, RoutedEventArgs e) => SystemService.Lock();

    private void Sleep_Click(object sender, RoutedEventArgs e) => _ = Task.Run(() =>
    {
        try { SystemService.Sleep(); } catch (Exception ex) { AppLog.Error("MainWindow.Sleep_Click", ex); }
    });

    private void Restart_Click(object sender, RoutedEventArgs e) =>
        ArmOrRun(BtnRestart, RestartLabel, Loc.T("Рестарт"), SystemService.Restart);

    private void Shutdown_Click(object sender, RoutedEventArgs e) =>
        ArmOrRun(BtnShutdown, ShutdownLabel, Loc.T("Выкл."), SystemService.Shutdown);

    /// <summary>Первый клик — «Точно?», второй в течение 3 секунд — выполнить.</summary>
    private void ArmOrRun(Button btn, TextBlock label, string normal, Action run)
    {
        if (_armedBtn == btn)
        {
            DisarmPower();
            try { run(); } catch (Exception ex) { AppLog.Error("MainWindow.ArmOrRun", ex); }
            return;
        }

        DisarmPower();
        _armedBtn = btn;
        _armedLabel = label;
        _armedNormal = normal;
        label.Text = Loc.T("Точно?");
        btn.Tag = "danger";
        _armTimer.Stop();
        _armTimer.Start();
    }

    private void DisarmPower()
    {
        _armTimer.Stop();
        if (_armedBtn == null) return;
        _armedBtn.Tag = null;
        if (_armedLabel != null) _armedLabel.Text = _armedNormal;
        _armedBtn = null;
        _armedLabel = null;
    }

    // ---------- Громкость ----------
    private void VolDown_Click(object sender, RoutedEventArgs e) => SystemService.VolumeKey(SystemService.VolumeDown, 5);
    private void VolUp_Click(object sender, RoutedEventArgs e) => SystemService.VolumeKey(SystemService.VolumeUp, 5);

    // ---------- Батарея ----------
    private static string GetBatteryText()
    {
        if (!GetSystemPowerStatus(out var s)) return "";
        if (s.BatteryFlag == 128 || s.BatteryLifePercent == 255) return "";
        string charging = s.ACLineStatus == 1 ? " ⚡" : "";
        return $"{s.BatteryLifePercent}%{charging}";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);
}
