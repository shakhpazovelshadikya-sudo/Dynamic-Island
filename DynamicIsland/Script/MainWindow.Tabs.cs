using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace DynamicIsland;

/// <summary>Вкладки острова: переключение, а также порядок и видимость из настроек.</summary>
public partial class MainWindow
{
    private HashSet<int> _visibleTabs = Enumerable.Range(0, TabCatalog.All.Length).ToHashSet();

    private Button[] TabButtons() => new[] { TabMain, TabWeather, TabCalendar, TabShelf, TabTimer, TabClipboard, TabSystem, TabTranslate };

    private bool IsTabVisible(int tab) => _visibleTabs.Contains(tab);

    /// <summary>Расставляет кнопки вкладок по порядку из настроек и прячет отключённые.</summary>
    private void ApplyTabs()
    {
        var buttons = TabButtons();
        var order = TabCatalog.Normalize(_settings.Tabs).Select(TabCatalog.IndexOf).ToList();

        TabBar.Children.Clear();
        foreach (int i in order) TabBar.Children.Add(buttons[i]);
        _visibleTabs = order.ToHashSet();

        if (!IsTabVisible(_tab)) SelectTab(0, false);
    }

    // ---------- Вкладки ----------
    private void TabMain_Click(object sender, RoutedEventArgs e) => SelectTab(0);
    private void TabWeather_Click(object sender, RoutedEventArgs e) => SelectTab(1);
    private void TabCalendar_Click(object sender, RoutedEventArgs e) => SelectTab(2);
    private void TabShelf_Click(object sender, RoutedEventArgs e) => SelectTab(3);
    private void TabTimer_Click(object sender, RoutedEventArgs e) => SelectTab(4);
    private void TabClipboard_Click(object sender, RoutedEventArgs e) => SelectTab(5);
    private void TabSystem_Click(object sender, RoutedEventArgs e) => SelectTab(6);
    private void TabTranslate_Click(object sender, RoutedEventArgs e) => SelectTab(7);

    private void SelectTab(int tab, bool animate = true)
    {
        if (!IsTabVisible(tab)) tab = 0; // скрытая в настройках вкладка недоступна
        _tab = tab;
        var buttons = TabButtons();
        var panels = new UIElement[] { MainPanel, WeatherPanel, CalendarPanel, ShelfPanel, TimerPanel, ClipboardPanel, SystemPanel, TranslatePanel };

        for (int i = 0; i < panels.Length; i++)
        {
            buttons[i].Tag = i == tab ? "selected" : null;
            panels[i].Visibility = i == tab ? Visibility.Visible : Visibility.Collapsed;
        }

        if (animate)
            panels[tab].BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));

        if (tab == 1 && (_weatherInfo == null || DateTime.Now - _weatherLoadedAt > TimeSpan.FromMinutes(10)))
            _ = RefreshWeatherAsync();

        if (tab == 2)
        {
            _calMonth = DateTime.Today;
            BuildCalendar();
        }

        if (tab == 3) BuildShelf();
        if (tab == 4) UpdateTimerUi();
        if (tab == 5) BuildClipboard();
        if (tab != 6) DisarmPower();
        if (tab == 6) _ = RefreshSystemAsync();
        if (tab == 7) InitTranslator();
    }
}
