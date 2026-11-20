using System.Windows;

namespace DynamicIsland;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        Loc.Init(SettingsService.Load().Language);
        AppLog.Info("Запуск Dynamic Island");

        // Необработанные ошибки пишем в журнал, а остров продолжает работать
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Error("UI", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) AppLog.Error("Domain", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("Task", args.Exception);
            args.SetObserved();
        };

        base.OnStartup(e);
    }
}
