using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace DynamicIsland;

/// <summary>Отдельное окно настроек Dynamic Island (открывается шестерёнкой на острове или из трея).</summary>
public partial class SettingsWindow : Window
{
    private readonly Action _onSaved;
    private readonly Action _onTest;
    private int _monitorIndex;

    public SettingsWindow(Action onSaved, Action onTest)
    {
        InitializeComponent();
        Loc.Apply(this);
        _onSaved = onSaved;
        _onTest = onTest;

        var s = SettingsService.Load();
        _langChoice = Loc.Choices.Any(c => c.Code == s.Language) ? s.Language : "";
        _langSaved = _langChoice;
        UpdateLanguageText();
        MarginSlider.Value = Math.Clamp(s.TopMargin, 0, 40);
        ExpandSwitch.IsChecked = s.ExpandOnTrackChange;
        NotifSwitch.IsChecked = s.ShowNotifications;
        LangSwitch.IsChecked = s.AnimateLanguage;
        FullscreenSwitch.IsChecked = s.HideInFullscreen;
        ClipSwitch.IsChecked = s.ClipboardHistory;
        AutoSwitch.IsChecked = AutoStart.IsEnabled();
        var screens = ScreenHelper.All;
        _monitorIndex = Math.Max(0, Array.FindIndex(screens, sc => sc == ScreenHelper.Resolve(s.MonitorDevice)));
        UpdateMonitorText();
        if (!AutoStart.IsAvailable)
        {
            AutoSwitch.IsEnabled = false;
            AutoHint.Text = Loc.T("Недоступно при запуске через dotnet run — работает для собранного exe или установленной версии");
        }
        CompactMaxSlider.Value = Math.Clamp(s.CompactMaxWidth, CompactMaxSlider.Minimum, CompactMaxSlider.Maximum);
        ExpandedMaxSlider.Value = Math.Clamp(s.ExpandedMaxWidth, ExpandedMaxSlider.Minimum, ExpandedMaxSlider.Maximum);
        BuildTabRows(s.Tabs);
        BuildPetChoices(s.Pet);
        AboutPath.Text = SettingsService.FilePath;
        AboutVersion.Text = Loc.F("Версия {0}", UpdateService.Current);
        UpdateService.Changed += OnUpdateChanged;
        Closed += (_, _) => UpdateService.Changed -= OnUpdateChanged;
        RefreshUpdateUi();
        UpdateSizeText();
        UpdateMarginText();
        UpdateNotifStatus();
    }

    // ---------- Обновления ----------
    private void OnUpdateChanged() => Dispatcher.Invoke(RefreshUpdateUi);

    private void RefreshUpdateUi()
    {
        var update = UpdateService.Available;
        UpdateBtn.Visibility = update != null ? Visibility.Visible : Visibility.Collapsed; // кнопка только когда есть обновление
        if (update != null)
        {
            UpdateBtn.Content = Loc.F("Обновить до {0}", update.Version);
            UpdateStatus.Text = Loc.F("Доступна новая версия {0}", update.Version);
        }
        else
            UpdateStatus.Text = UpdateService.Checked ? Loc.T("Установлена последняя версия") : "";
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateBtn.IsEnabled = false;
        UpdateStatus.Text = Loc.T("Проверяю…");
        await UpdateService.CheckAsync();
        RefreshUpdateUi();
        if (!UpdateService.Checked) UpdateStatus.Text = Loc.T("Не удалось проверить обновления");
        CheckUpdateBtn.IsEnabled = true;
    }

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        UpdateBtn.IsEnabled = false;
        UpdateBtn.Content = Loc.T("Загрузка…");
        string? error = await UpdateService.InstallAsync();
        if (error == null) { Application.Current.Shutdown(); return; }
        UpdateStatus.Text = error;
        UpdateBtn.IsEnabled = true;
        RefreshUpdateUi();
    }

    // ---------- Язык острова ----------
    private string _langChoice = "";
    private string _langSaved = "";

    private void Language_Click(object sender, RoutedEventArgs e)
    {
        int i = Array.FindIndex(Loc.Choices, c => c.Code == _langChoice);
        _langChoice = Loc.Choices[(i + 1) % Loc.Choices.Length].Code;
        UpdateLanguageText();
    }

    // названия — на своём языке, поэтому не переводятся (кроме «Авто»)
    private void UpdateLanguageText() => LanguageBtn.Content = _langChoice == "" ? Loc.T("Авто") : Loc.NameOf(_langChoice);

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (PanelGeneral == null || PanelIsland == null || PanelSize == null || PanelTabs == null || PanelPet == null || PanelNotif == null || PanelAbout == null) return; // событие приходит во время InitializeComponent
        string tag = (string)((FrameworkElement)sender).Tag;
        var panels = new (string Tag, UIElement Panel)[]
        {
            ("General", PanelGeneral), ("Island", PanelIsland), ("Size", PanelSize),
            ("Tabs", PanelTabs), ("Pet", PanelPet), ("Notif", PanelNotif), ("About", PanelAbout)
        };
        foreach (var (t, panel) in panels)
            panel.Visibility = t == tag ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshStatus_Click(object sender, RoutedEventArgs e) => UpdateNotifStatus();

    private void UpdateNotifStatus()
    {
        var (text, color) = NotificationService.Mode switch
        {
            NotifMode.Official => (Loc.T("● Работает: официальный интерфейс Windows"), "#5BD67F"),
            NotifMode.Database => (Loc.T("● Работает: чтение базы уведомлений Windows. Способ недокументированный — после крупного обновления Windows может перестать работать."), "#F0B24A"),
            NotifMode.Unavailable => (Loc.F("● Не работает: {0}. Подробности — в журнале ошибок.", Loc.T(NotificationService.ModeDetail)), "#FF6B6B"),
            _ => (Loc.T("● Запускается…"), "#8A8A93")
        };
        NotifStatus.Text = text;
        NotifStatus.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(color)!;
    }

    private void Monitor_Click(object sender, RoutedEventArgs e)
    {
        _monitorIndex = (_monitorIndex + 1) % ScreenHelper.All.Length;
        UpdateMonitorText();
    }

    private void UpdateMonitorText()
    {
        var all = ScreenHelper.All;
        if (_monitorIndex >= all.Length) _monitorIndex = 0;
        MonitorText.Text = ScreenHelper.Describe(all[_monitorIndex], _monitorIndex);
    }

    private void MarginSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateMarginText();

    private void UpdateMarginText()
    {
        if (MarginValue == null) return; // событие приходит ещё во время InitializeComponent
        MarginValue.Text = $"{MarginSlider.Value:0} px";
    }

    private void Size_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateSizeText();

    private void UpdateSizeText()
    {
        if (CompactMaxValue == null || ExpandedMaxValue == null) return; // событие приходит ещё во время InitializeComponent
        CompactMaxValue.Text = $"{CompactMaxSlider.Value:0} px";
        ExpandedMaxValue.Text = $"{ExpandedMaxSlider.Value:0} px";
    }

    // ---------- Питомец ----------
    private string _petChoice = "";
    private readonly List<(string Id, Border Card)> _petCards = new();

    private void BuildPetChoices(string? current)
    {
        _petChoice = PetSprites.Find(current)?.Id ?? "";
        _petCards.Clear();
        PetList.Children.Clear();

        AddPetCard("", Loc.T("Нет"), null);
        foreach (var pet in PetSprites.All)
            AddPetCard(pet.Id, Loc.T(pet.Title), PetSprites.GetFrame(pet, "sit0"));
        RefreshPetCards();
    }

    private void AddPetCard(string id, string title, System.Windows.Media.ImageSource? preview)
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        if (preview != null)
        {
            var img = new Image { Source = preview, Width = 48, Height = 48, Margin = new Thickness(0, 4, 0, 6) };
            System.Windows.Media.RenderOptions.SetBitmapScalingMode(img, System.Windows.Media.BitmapScalingMode.NearestNeighbor);
            stack.Children.Add(img);
        }
        else
        {
            stack.Children.Add(new TextBlock
            {
                Text = "—", FontSize = 26, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x77, 0x77, 0x80)),
                Height = 48, Margin = new Thickness(0, 4, 0, 6), HorizontalAlignment = HorizontalAlignment.Center
            });
        }
        stack.Children.Add(new TextBlock { Text = title, Foreground = System.Windows.Media.Brushes.White, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center });

        var card = new Border
        {
            Width = 92, Height = 94, Margin = new Thickness(0, 0, 10, 10),
            CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(2),
            Cursor = System.Windows.Input.Cursors.Hand, Child = stack
        };
        card.MouseLeftButtonUp += (_, _) => { _petChoice = id; RefreshPetCards(); };

        _petCards.Add((id, card));
        PetList.Children.Add(card);
    }

    private void RefreshPetCards()
    {
        foreach (var (id, card) in _petCards)
        {
            bool on = id == _petChoice;
            card.Background = new System.Windows.Media.SolidColorBrush(on
                ? System.Windows.Media.Color.FromRgb(0x2E, 0x2E, 0x36) : System.Windows.Media.Color.FromRgb(0x1C, 0x1C, 0x21));
            card.BorderBrush = new System.Windows.Media.SolidColorBrush(on
                ? System.Windows.Media.Color.FromRgb(0x5A, 0x78, 0xFF) : System.Windows.Media.Colors.Transparent);
        }
    }

    // ---------- Вкладки: порядок и видимость ----------
    private readonly List<(string Id, CheckBox Switch, UIElement Row)> _tabRows = new();

    private void BuildTabRows(IEnumerable<string> visible)
    {
        // сначала включённые в сохранённом порядке, потом выключенные
        var enabled = TabCatalog.Normalize(visible);
        var all = enabled.Concat(TabCatalog.All.Select(t => t.Id).Where(id => !enabled.Contains(id))).ToList();

        _tabRows.Clear();
        TabsList.Children.Clear();
        foreach (string id in all)
        {
            var row = MakeTabRow(id, enabled.Contains(id));
            TabsList.Children.Add(row.Row);
            _tabRows.Add(row);
        }
        UpdateMoveButtons();
    }

    private (string Id, CheckBox Switch, UIElement Row) MakeTabRow(string id, bool on)
    {
        string title = Loc.T(TabCatalog.All[TabCatalog.IndexOf(id)].Title);
        bool isMain = id == "main";

        var sw = new CheckBox
        {
            Style = (Style)FindResource("Switch"),
            IsChecked = on || isMain,
            IsEnabled = !isMain,
            VerticalAlignment = VerticalAlignment.Center
        };
        var name = new TextBlock
        {
            Text = title,
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 13,
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var up = new Button { Style = (Style)FindResource("MiniButton"), Content = "▲", ToolTip = Loc.T("Выше") };
        var down = new Button { Style = (Style)FindResource("MiniButton"), Content = "▼", ToolTip = Loc.T("Ниже") };
        up.Click += (_, _) => MoveTab(id, -1);
        down.Click += (_, _) => MoveTab(id, +1);

        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(name, 1);
        Grid.SetColumn(up, 2);
        Grid.SetColumn(down, 3);
        grid.Children.Add(sw);
        grid.Children.Add(name);
        grid.Children.Add(up);
        grid.Children.Add(down);
        return (id, sw, grid);
    }

    private void MoveTab(string id, int delta)
    {
        int i = _tabRows.FindIndex(r => r.Id == id);
        int j = i + delta;
        if (i < 0 || j < 0 || j >= _tabRows.Count) return;

        (_tabRows[i], _tabRows[j]) = (_tabRows[j], _tabRows[i]);
        TabsList.Children.Clear();
        foreach (var r in _tabRows) TabsList.Children.Add(r.Row);
        UpdateMoveButtons();
    }

    /// <summary>Первая строка не поднимается выше, последняя не опускается ниже.</summary>
    private void UpdateMoveButtons()
    {
        for (int i = 0; i < _tabRows.Count; i++)
        {
            var grid = (Grid)_tabRows[i].Row;
            ((Button)grid.Children[2]).IsEnabled = i > 0;
            ((Button)grid.Children[3]).IsEnabled = i < _tabRows.Count - 1;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var s = SettingsService.Load();
        s.TopMargin = Math.Round(MarginSlider.Value);
        s.ExpandOnTrackChange = ExpandSwitch.IsChecked == true;
        s.ShowNotifications = NotifSwitch.IsChecked == true;
        s.AnimateLanguage = LangSwitch.IsChecked == true;
        s.HideInFullscreen = FullscreenSwitch.IsChecked == true;
        s.ClipboardHistory = ClipSwitch.IsChecked == true;
        s.CompactMaxWidth = Math.Round(CompactMaxSlider.Value);
        s.ExpandedMaxWidth = Math.Round(ExpandedMaxSlider.Value);
        s.Language = _langChoice;
        s.Pet = _petChoice;
        s.Tabs = TabCatalog.Normalize(_tabRows.Where(r => r.Switch.IsChecked == true).Select(r => r.Id));
        var all = ScreenHelper.All;
        var chosen = all[Math.Min(_monitorIndex, all.Length - 1)];
        s.MonitorDevice = chosen.Primary ? "" : chosen.DeviceName; // основной храним как пусто — следует за ним
        SettingsService.Save(s);

        if (AutoStart.IsAvailable)
        {
            try { AutoStart.Set(AutoSwitch.IsChecked == true); } catch (Exception ex) { AppLog.Error("SettingsWindow.Save_Click", ex); }
        }

        _onSaved();
        Close();

        if (_langChoice != _langSaved && Loc.Resolve(_langChoice) != Loc.Code)
            AskRestart(_langChoice);
    }

    /// <summary>Язык применяется при старте — предлагаем перезапустить остров сразу.</summary>
    private static void AskRestart(string newLanguage)
    {
        string question = Loc.Resolve(newLanguage) == "en"
            ? "The island needs to restart to apply the language. Restart now?"
            : "Остров будет перезапущен, чтобы применить язык. Перезапустить сейчас?";
        string exe = Environment.ProcessPath ?? "";
        bool canRestart = exe.Length > 0 && !Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase);

        if (!canRestart)
        {
            System.Windows.MessageBox.Show(Loc.Resolve(newLanguage) == "en"
                ? "The language will change the next time the island starts."
                : "Язык изменится при следующем запуске острова.", "Dynamic Island");
            return;
        }

        if (System.Windows.MessageBox.Show(question, "Dynamic Island", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        try
        {
            // небольшая задержка, чтобы старый экземпляр успел освободить трей и порты
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c ping 127.0.0.1 -n 2 >nul & start \"\" \"{exe}\"") { CreateNoWindow = true, UseShellExecute = false });
            Application.Current.Shutdown();
        }
        catch (Exception ex) { AppLog.Error("SettingsWindow.AskRestart", ex); }
    }

    private void Test_Click(object sender, RoutedEventArgs e) => _onTest();

    private void OpenLog_Click(object sender, RoutedEventArgs e) => AppLog.Open();

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
