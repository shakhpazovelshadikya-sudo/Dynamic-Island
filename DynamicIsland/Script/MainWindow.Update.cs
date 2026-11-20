using System.Windows;
using System.Windows.Threading;

namespace DynamicIsland;

/// <summary>Проверка обновлений: кнопка «Обновить» на острове и уведомление о новой версии.</summary>
public partial class MainWindow
{
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromMinutes(30) };
    private string? _updateAnnounced;
    private bool _updateBusy;

    private void InitUpdates()
    {
        UpdateService.Changed += () => Dispatcher.Invoke(OnUpdateChanged);
        _updateTimer.Tick += async (_, _) => await UpdateService.CheckAsync();
        _updateTimer.Start();
        _ = Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(_ => Dispatcher.Invoke(async () => await UpdateService.CheckAsync()));
    }

    private void OnUpdateChanged()
    {
        var update = UpdateService.Available;
        UpdateButton.Visibility = update != null ? Visibility.Visible : Visibility.Collapsed;
        if (update == null) return;

        UpdateButton.ToolTip = Loc.F("Доступна версия {0}", update.Version);
        if (_updateAnnounced == update.Tag) return;
        _updateAnnounced = update.Tag;
        EnqueueToast(new NotificationInfo(NotificationService.OwnName, Loc.F("Доступна версия {0}", update.Version),
            Loc.T("Открой остров и нажми «Обновить»"), null));
    }

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        if (_updateBusy) return;
        _updateBusy = true;
        UpdateButton.IsEnabled = false;
        UpdateButton.Content = Loc.T("Загрузка…");

        string? error = await UpdateService.InstallAsync();
        if (error == null)
        {
            Application.Current.Shutdown(); // установщик сам закроет остров и запустит новую версию
            return;
        }

        _updateBusy = false;
        UpdateButton.IsEnabled = true;
        UpdateButton.Content = Loc.T("⬆ Обновить");
        EnqueueToast(new NotificationInfo(NotificationService.OwnName, Loc.T("Обновление"), error, null));
    }
}
