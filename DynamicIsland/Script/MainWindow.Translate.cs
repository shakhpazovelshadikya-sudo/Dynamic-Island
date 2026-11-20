using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DynamicIsland;

/// <summary>Вкладка «Переводчик»: текст слева, перевод справа, выбор любых языков.</summary>
public partial class MainWindow
{
    private string _trFrom = TranslateService.Auto, _trTo = "en";
    private string? _trDetected;
    private int _trRun;
    private CancellationTokenSource? _trCts;
    private readonly DispatcherTimer _trDebounce = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private bool _trInit;

    private void InitTranslator()
    {
        if (_trInit) return;
        _trInit = true;
        _trFrom = string.IsNullOrEmpty(_settings.TranslateFrom) ? TranslateService.Auto : _settings.TranslateFrom;
        _trTo = string.IsNullOrEmpty(_settings.TranslateTo) ? "en" : _settings.TranslateTo;
        _trDebounce.Tick += (_, _) => { _trDebounce.Stop(); _ = RunTranslateAsync(); };
        TrInput.MaxLength = TranslateService.MaxChars;
        UpdateTrButtons();
    }

    private void UpdateTrButtons()
    {
        string src = TranslateService.NameOf(_trFrom);
        if (_trFrom == TranslateService.Auto && _trDetected != null) src += $" · {TranslateService.NameOf(_trDetected)}";
        TrFromBtn.Content = src + " ▾";
        TrToBtn.Content = TranslateService.NameOf(_trTo) + " ▾";
    }

    private void SaveTrLangs()
    {
        try
        {
            var s = SettingsService.Load();
            s.TranslateFrom = _trFrom;
            s.TranslateTo = _trTo;
            SettingsService.Save(s);
            _settings.TranslateFrom = _trFrom;
            _settings.TranslateTo = _trTo;
        }
        catch (Exception ex) { AppLog.Error("MainWindow.SaveTrLangs", ex); }
    }

    private void ShowLangMenu(Button anchor, bool source)
    {
        var menu = new ContextMenu { PlacementTarget = anchor, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom, MaxHeight = 260 };
        TrackMenu(menu);

        void Add(string code, string name)
        {
            bool cur = code == (source ? _trFrom : _trTo);
            var item = new MenuItem { Header = name, IsCheckable = true, IsChecked = cur };
            item.Click += (_, _) =>
            {
                if (source) _trFrom = code; else _trTo = code;
                SaveTrLangs();
                _trDetected = null;
                UpdateTrButtons();
                QueueTranslate(0);
            };
            menu.Items.Add(item);
        }

        if (source) Add(TranslateService.Auto, Loc.T("Определить язык"));
        foreach (var l in TranslateService.Languages) Add(l.Code, Loc.T(l.Name));
        menu.IsOpen = true;
    }

    private void TrFrom_Click(object sender, RoutedEventArgs e) => ShowLangMenu(TrFromBtn, true);
    private void TrTo_Click(object sender, RoutedEventArgs e) => ShowLangMenu(TrToBtn, false);

    private void TrSwap_Click(object sender, RoutedEventArgs e)
    {
        string newTo = _trFrom == TranslateService.Auto ? (_trDetected ?? Loc.Code) : _trFrom;
        string newFrom = _trTo;
        string result = TrOutput.Text;

        _trFrom = newFrom;
        _trTo = newTo;
        _trDetected = null;
        SaveTrLangs();
        UpdateTrButtons();

        if (!string.IsNullOrWhiteSpace(result)) TrInput.Text = result; // TextChanged запустит перевод
        else QueueTranslate(0);
    }

    private void TrPaste_Click(object sender, RoutedEventArgs e)
    {
        try { if (Clipboard.ContainsText()) TrInput.Text = Clipboard.GetText(); }
        catch (Exception ex) { AppLog.Error("MainWindow.TrPaste", ex); }
    }

    private void TrCopy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TrOutput.Text)) return;
        try
        {
            Clipboard.SetText(TrOutput.Text);
            TrStatus.Text = Loc.T("Скопировано");
        }
        catch (Exception ex) { AppLog.Error("MainWindow.TrCopy", ex); }
    }

    private void TrClear_Click(object sender, RoutedEventArgs e) => TrInput.Clear();

    private void TrInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_trInit) return;
        TrPlaceholder.Visibility = TrInput.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        QueueTranslate(700);
    }

    private void TrInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            QueueTranslate(0);
        }
    }

    private void QueueTranslate(int ms)
    {
        _trDebounce.Stop();
        if (string.IsNullOrWhiteSpace(TrInput.Text))
        {
            _trCts?.Cancel();
            TrOutput.Text = "";
            TrStatus.Text = "";
            return;
        }

        if (ms <= 0) _ = RunTranslateAsync();
        else { _trDebounce.Interval = TimeSpan.FromMilliseconds(ms); _trDebounce.Start(); }
    }

    private async Task RunTranslateAsync()
    {
        string text = TrInput.Text;
        if (string.IsNullOrWhiteSpace(text)) return;

        _trCts?.Cancel();
        _trCts = new CancellationTokenSource();
        int run = ++_trRun;
        TrStatus.Text = Loc.T("Перевожу…");

        var (res, det, err) = await TranslateService.TranslateAsync(text, _trFrom, _trTo, _trCts.Token);
        if (run != _trRun) return; // пока ждали ответ, текст изменился

        if (err != null) { TrStatus.Text = err; return; }
        if (res == null) return;

        TrOutput.Text = res;
        TrStatus.Text = text.Length >= TranslateService.MaxChars ? Loc.F("Переведены первые {0} символов", TranslateService.MaxChars) : "";
        if (_trFrom == TranslateService.Auto) { _trDetected = det; UpdateTrButtons(); }
    }

    // ---------- Ввод с клавиатуры ----------
    // Остров не забирает фокус (WS_EX_NOACTIVATE), поэтому на время ввода делаем окно активным.
    private bool _trTyping;

    private void TrInput_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_trTyping)
        {
            _trTyping = true;
            SetActivatable(true);
            Activate();
        }
        Dispatcher.BeginInvoke(() => TrInput.Focus(), DispatcherPriority.Input);
    }

    private void TrInput_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_trTyping) return;
        _trTyping = false;
        SetActivatable(false);
        if (!Island.IsMouseOver && _menusOpen == 0) Schedule(900);
    }

    private void SetActivatable(bool on)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int style = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, on ? style & ~WS_EX_NOACTIVATE : style | WS_EX_NOACTIVATE);
    }
}
