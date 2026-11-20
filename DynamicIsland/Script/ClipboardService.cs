using System.Runtime.InteropServices;
using System.Windows;

namespace DynamicIsland;

public record ClipItem(string Text, DateTime At);

/// <summary>
/// История буфера обмена (только текст, только в памяти — на диск ничего не пишется).
/// Записи, помеченные менеджерами паролей как «не для истории», пропускаются.
/// </summary>
public sealed class ClipboardService
{
    private const int MaxItems = 25;
    private const int MaxChars = 20000;

    private readonly List<ClipItem> _items = new();
    private string? _selfText;
    private DateTime _selfAt;

    public IReadOnlyList<ClipItem> Items => _items;
    public event Action? Changed;

    /// <summary>Вызывается при WM_CLIPBOARDUPDATE (в потоке интерфейса).</summary>
    public async void NotifyUpdate()
    {
        try
        {
            string? text = await ReadTextAsync();
            if (string.IsNullOrWhiteSpace(text)) return;
            if (text.Length > MaxChars) text = text[..MaxChars];

            // это мы сами положили текст из истории — порядок не меняем
            if (_selfText != null && text == _selfText && DateTime.UtcNow - _selfAt < TimeSpan.FromSeconds(3))
            {
                _selfText = null;
                return;
            }

            Add(text);
        }
        catch (Exception ex) { AppLog.Error("ClipboardService.NotifyUpdate", ex); }
    }

    /// <summary>Копирует запись обратно в буфер, не поднимая её наверх списка.</summary>
    public bool CopyBack(ClipItem item)
    {
        try
        {
            _selfText = item.Text;
            _selfAt = DateTime.UtcNow;
            Clipboard.SetText(item.Text);
            return true;
        }
        catch (Exception ex)
        {
            _selfText = null;
            AppLog.Error("ClipboardService.CopyBack", ex);
            return false;
        }
    }

    public void Remove(ClipItem item)
    {
        if (_items.Remove(item)) Changed?.Invoke();
    }

    public void Clear()
    {
        if (_items.Count == 0) return;
        _items.Clear();
        Changed?.Invoke();
    }

    private void Add(string text)
    {
        _items.RemoveAll(i => i.Text == text);
        _items.Insert(0, new ClipItem(text, DateTime.Now));
        if (_items.Count > MaxItems) _items.RemoveRange(MaxItems, _items.Count - MaxItems);
        Changed?.Invoke();
    }

    private static async Task<string?> ReadTextAsync()
    {
        // буфер может быть занят другой программой в этот момент — пробуем несколько раз
        for (int i = 0; i < 4; i++)
        {
            try
            {
                if (Clipboard.ContainsData("ExcludeClipboardContentFromMonitorProcessing")) return null; // менеджеры паролей
                if (!Clipboard.ContainsText()) return null;
                return Clipboard.GetText();
            }
            catch (COMException) { await Task.Delay(80); }
            catch (Exception ex)
            {
                AppLog.Error("ClipboardService.ReadTextAsync", ex);
                return null;
            }
        }
        return null;
    }
}
