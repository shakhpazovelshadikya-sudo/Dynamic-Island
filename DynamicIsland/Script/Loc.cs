using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DynamicIsland;

/// <summary>
/// Язык интерфейса. Ключ перевода — сам русский текст: <c>Loc.T("Главная")</c> вернёт «Home» при английском языке.
/// Статические тексты из XAML переводятся обходом дерева (<see cref="Apply"/>), так что XAML трогать не нужно.
/// </summary>
public static class Loc
{
    /// <summary>Доступные языки: код, название на самом языке. Пустой код — «как в системе».</summary>
    public static readonly (string Code, string Name)[] Choices =
    {
        ("", "Авто"), ("ru", "Русский"), ("en", "English")
    };

    /// <summary>Текущий язык: «ru» или «en». До вызова <see cref="Init"/> — русский (как раньше).</summary>
    public static string Code { get; private set; } = "ru";

    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("ru-RU");

    /// <summary>Выбирает язык по настройке: пусто/неизвестно — по языку интерфейса Windows (русский → ru, иначе en).</summary>
    public static void Init(string? setting)
    {
        Code = Resolve(setting);
        Culture = CultureInfo.GetCultureInfo(Code == "ru" ? "ru-RU" : "en-US");
    }

    public static string Resolve(string? setting)
    {
        if (setting == "ru" || setting == "en") return setting;
        return CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "ru" ? "ru" : "en";
    }

    public static string NameOf(string? setting) =>
        Array.Find(Choices, c => c.Code == (setting ?? "")).Name ?? Choices[0].Name;

    /// <summary>Перевод строки; нет перевода или язык русский — возвращает исходный текст.</summary>
    public static string T(string ru) => Code == "en" && En.TryGetValue(ru, out var en) ? en : ru;

    public static string F(string ru, params object[] args) => string.Format(Culture, T(ru), args);

    // ---------- перевод готового XAML ----------
    /// <summary>Переводит статические тексты (Text, Content, ToolTip, Header, Title) всего дерева окна.</summary>
    public static void Apply(DependencyObject root)
    {
        if (Code == "ru") return;
        Walk(root);
    }

    private static void Walk(DependencyObject d)
    {
        switch (d)
        {
            case Window w when w.Title != null: w.Title = T(w.Title); break;
            case TextBlock tb: tb.Text = T(tb.Text); break;
            case MenuItem mi when mi.Header is string h: mi.Header = T(h); break;
            case ContentControl cc when cc.Content is string c: cc.Content = T(c); break;
        }

        if (d is FrameworkElement fe)
        {
            if (fe.ToolTip is string tip) fe.ToolTip = T(tip);
            if (fe.ContextMenu is { } menu) Walk(menu);
        }

        foreach (var child in LogicalTreeHelper.GetChildren(d))
            if (child is DependencyObject c) Walk(c);
    }

    private static readonly Dictionary<string, string> En = new()
    {
        ["Главная"] = "Home",
        ["Погода"] = "Weather",
        ["Календарь"] = "Calendar",
        ["Полка"] = "Shelf",
        ["Таймер"] = "Timer",
        ["Буфер обмена"] = "Clipboard",
        ["Система"] = "System",
        ["Переводчик"] = "Translator",
        ["Выбрать плеер"] = "Choose player",
        ["Выход"] = "Exit",
        ["Без звука"] = "Mute",
        ["Вернуться к текущему месяцу"] = "Back to current month",
        ["Перетащи сюда файлы, папки или программы — и запускай одним кликом"] = "Drop files, folders or apps here — and launch them with one click",
        ["Загрузка…"] = "Loading…",
        ["Загрузка {0}"] = "Loading {0}",
        ["Отпусти, чтобы добавить на полку"] = "Release to add to the shelf",
        ["1 мин"] = "1 min",
        ["5 мин"] = "5 min",
        ["10 мин"] = "10 min",
        ["25 мин"] = "25 min",
        ["−1 мин"] = "−1 min",
        ["+1 мин"] = "+1 min",
        ["Старт / пауза"] = "Start / pause",
        ["Сбросить"] = "Reset",
        ["Очистить историю"] = "Clear history",
        ["Авто ▾"] = "Auto ▾",
        ["Авто"] = "Auto",
        ["Поменять языки местами"] = "Swap languages",
        ["Вставить"] = "Paste",
        ["Копировать"] = "Copy",
        ["Очистить"] = "Clear",
        ["Введи или вставь текст"] = "Type or paste text",
        ["Параметры Wi-Fi…"] = "Wi-Fi settings…",
        ["Параметры Bluetooth…"] = "Bluetooth settings…",
        ["Заблокировать компьютер"] = "Lock computer",
        ["Блок."] = "Lock",
        ["Режим сна"] = "Sleep",
        ["Сон"] = "Sleep",
        ["Перезагрузить (нужно подтверждение)"] = "Restart (needs confirmation)",
        ["Рестарт"] = "Restart",
        ["Выключить компьютер (нужно подтверждение)"] = "Shut down (needs confirmation)",
        ["Выкл."] = "Off",
        ["Тише"] = "Quieter",
        ["Громче"] = "Louder",
        ["Dynamic Island — Настройки"] = "Dynamic Island — Settings",
        ["Настройки"] = "Settings",
        ["Основные"] = "General",
        ["Остров"] = "Island",
        ["Размер"] = "Size",
        ["Вкладки"] = "Tabs",
        ["Питомец"] = "Pet",
        ["Уведомления"] = "Notifications",
        ["О программе"] = "About",
        ["Запускать вместе с Windows"] = "Start with Windows",
        ["Остров будет запускаться при входе в Windows"] = "The island will start when you sign in to Windows",
        ["Отступ сверху"] = "Top margin",
        ["Монитор"] = "Monitor",
        ["Сменить"] = "Change",
        ["Раскрывать при смене трека"] = "Expand on track change",
        ["Остров сам откроется на 3 секунды"] = "The island opens by itself for 3 seconds",
        ["Анимация смены языка"] = "Keyboard layout animation",
        ["При переключении раскладки остров показывает EN → RU"] = "When you switch the keyboard layout, the island shows EN → RU",
        ["Скрывать в полноэкранном режиме"] = "Hide in full screen",
        ["Когда игра или видео открыты на весь экран, остров прячется сам"] = "When a game or video is full screen, the island hides itself",
        ["История буфера обмена"] = "Clipboard history",
        ["Запоминать скопированный текст для вкладки «Буфер». Только в памяти, на диск не пишется; при выключении история стирается"] = "Remembers copied text for the Clipboard tab. Kept in memory only, never written to disk; turning it off clears the history",
        ["Макс. ширина капсулы"] = "Max. capsule width",
        ["Длинное название трека растягивает капсулу до этой ширины, дальше текст обрезается"] = "A long track title stretches the capsule up to this width, then the text is cut off",
        ["Макс. ширина острова"] = "Max. island width",
        ["Раскрытый остров расширяется под длинное название и исполнителя до этого предела"] = "The expanded island widens for a long title and artist up to this limit",
        ["Включай и выключай вкладки острова, порядок меняется стрелками. «Главная» отключить нельзя."] = "Turn island tabs on and off; use the arrows to reorder. “Home” can't be turned off.",
        ["Живёт в левой части капсулы: сидит, моргает, виляет хвостом, гуляет и спит. Когда играет музыка — пританцовывает, а при смене трека подпрыгивает."] = "Lives on the left of the capsule: sits, blinks, wags its tail, walks and sleeps. Dances when music plays and jumps when the track changes.",
        ["Показывать уведомления"] = "Show notifications",
        ["Сообщения из других приложений выезжают под островом"] = "Messages from other apps slide out under the island",
        ["Состояние"] = "Status",
        ["Обновить статус"] = "Refresh status",
        ["Журнал ошибок"] = "Error log",
        ["Нужен доступ к уведомлениям: Параметры Windows → Конфиденциальность и безопасность → Уведомления."] = "Notification access is required: Windows Settings → Privacy & security → Notifications.",
        ["Показать пример"] = "Show example",
        ["Dynamic Island для Windows"] = "Dynamic Island for Windows",
        ["Версия 1.0"] = "Version 1.0",
        ["Погода: open-meteo.com. Город определяется автоматически по IP."] = "Weather: open-meteo.com. The city is detected automatically by IP.",
        ["Журнал ошибок: %AppData%\\DynamicIsland\\log.txt"] = "Error log: %AppData%\\DynamicIsland\\log.txt",
        ["Открыть журнал ошибок"] = "Open error log",
        ["Файл настроек"] = "Settings file",
        ["Отмена"] = "Cancel",
        ["Сохранить"] = "Save",
        ["Язык острова"] = "Island language",
        ["Язык интерфейса. Изменение применится после перезапуска острова"] = "Interface language. The change applies after the island restarts",
        ["Остров будет перезапущен, чтобы применить язык. Перезапустить сейчас?"] = "The island needs to restart to apply the language. Restart now?",
        ["Язык изменится при следующем запуске острова."] = "The language will change the next time the island starts.",
        ["неделя {0}"] = "week {0}",
        ["Пн"] = "Mo",
        ["Вт"] = "Tu",
        ["Ср"] = "We",
        ["Чт"] = "Th",
        ["Пт"] = "Fr",
        ["Сб"] = "Sa",
        ["Вс"] = "Su",
        ["Скопируй текст — он появится здесь. Клик по записи вернёт её в буфер"] = "Copy some text — it will appear here. Click an entry to put it back on the clipboard",
        ["История буфера выключена: настройки → «Остров»"] = "Clipboard history is off: Settings → “Island”",
        ["Удалить"] = "Delete",
        ["Очистить всё"] = "Clear all",
        ["Авто (как выбирает Windows)"] = "Auto (as chosen by Windows)",
        ["Отпусти файл"] = "Release the file",
        [" и ещё {0}"] = " and {0} more",
        ["Уже на полке"] = "Already on the shelf",
        ["Файлы установлены"] = "Files installed",
        ["Файл установлен"] = "File installed",
        ["Открыть"] = "Open",
        ["Показать в папке"] = "Show in folder",
        ["Убрать с полки"] = "Remove from shelf",
        ["Файл не найден"] = "File not found",
        ["Он удалён или перемещён — убрал его с полки"] = "It was deleted or moved — removed from the shelf",
        ["Не удалось запустить"] = "Couldn't launch",
        ["Вкл"] = "On",
        ["Нет доступа"] = "No access",
        ["Нет модуля"] = "No adapter",
        ["Windows не разрешила управлять радиомодулем"] = "Windows didn't allow controlling the radio",
        ["Нет профилей"] = "No profiles",
        ["Встроенных VPN-профилей Windows нет. Клик — открыть параметры VPN"] = "There are no built-in Windows VPN profiles. Click to open VPN settings",
        ["ПКМ — выбрать профиль"] = "Right-click — choose profile",
        ["Отключение…"] = "Disconnecting…",
        ["Подключение…"] = "Connecting…",
        ["Параметры VPN…"] = "VPN settings…",
        ["Точно?"] = "Sure?",
        ["Идёт"] = "Running",
        ["Пауза"] = "Paused",
        ["Выбери время"] = "Pick a time",
        ["Время вышло: {0} мин"] = "Time's up: {0} min",
        ["Определить язык"] = "Detect language",
        ["Скопировано"] = "Copied",
        ["Перевожу…"] = "Translating…",
        ["Переведены первые {0} символов"] = "Translated the first {0} characters",
        ["Нет данных"] = "No data",
        ["Ощущается {0}  ·  ветер {1} м/с"] = "Feels like {0}  ·  wind {1} m/s",
        ["Сегодня {0} / {1}  ·  влажн. {2}%"] = "Today {0} / {1}  ·  humidity {2}%",
        ["Влажность {0}%"] = "Humidity {0}%",
        ["Нет доступа к уведомлениям"] = "No access to notifications",
        ["Тестовое уведомление"] = "Test notification",
        ["Так будут выглядеть сообщения из других приложений"] = "This is how messages from other apps will look",
        ["Ничего не играет"] = "Nothing playing",
        ["Недоступно при запуске через dotnet run — работает для собранного exe или установленной версии"] = "Unavailable when started via dotnet run — works for a built exe or an installed version",
        ["● Работает: официальный интерфейс Windows"] = "● Working: official Windows interface",
        ["● Работает: чтение базы уведомлений Windows. Способ недокументированный — после крупного обновления Windows может перестать работать."] = "● Working: reading the Windows notification database. This method is undocumented and may stop working after a major Windows update.",
        ["● Не работает: {0}. Подробности — в журнале ошибок."] = "● Not working: {0}. See the error log for details.",
        ["● Запускается…"] = "● Starting…",
        ["Нет"] = "None",
        ["Выше"] = "Up",
        ["Ниже"] = "Down",
        ["Луна"] = "Moon",
        ["Кот"] = "Cat",
        ["Слайм"] = "Slime",
        ["Кролик"] = "Bunny",
        ["Собака"] = "Dog",
        ["Мишка"] = "Bear",
        ["Рыбка"] = "Fish",
        ["Акула"] = "Shark",
        ["Солнце и луна"] = "Sun and moon",
        ["Монитор {0}"] = "Monitor {0}",
        [" (основной)"] = " (primary)",
        ["Скрыть остров"] = "Hide island",
        ["Показать остров"] = "Show island",
        ["Обновить погоду"] = "Refresh weather",
        ["Настройки…"] = "Settings…",
        ["Медиаплеер"] = "Media Player",
        ["Кино и ТВ"] = "Movies & TV",
        ["Плеер"] = "Player",
        ["не найдена база уведомлений Windows"] = "Windows notification database not found",
        ["Не найдена база уведомлений Windows, показ уведомлений недоступен."] = "Windows notification database not found; notifications are unavailable.",
        ["Не удалось прочитать базу уведомлений Windows."] = "Couldn't read the Windows notification database.",
        ["Не удалось выполнить команду"] = "Couldn't run the command",
        ["Превышено время ожидания"] = "Timed out",
        ["Нет соединения с сервисом перевода"] = "No connection to the translation service",
        ["Ясно"] = "Clear",
        ["Преимущественно ясно"] = "Mostly clear",
        ["Переменная облачность"] = "Partly cloudy",
        ["Пасмурно"] = "Overcast",
        ["Туман"] = "Fog",
        ["Морось"] = "Drizzle",
        ["Дождь"] = "Rain",
        ["Снег"] = "Snow",
        ["Ливень"] = "Showers",
        ["Снегопад"] = "Heavy snow",
        ["Гроза"] = "Thunderstorm",
        ["Не удалось определить город"] = "Couldn't determine the city",
        ["Нет связи с сервером погоды"] = "Can't reach the weather server",
        ["Мой город"] = "My city",
        ["Английский"] = "English",
        ["Русский"] = "Russian",
        ["Украинский"] = "Ukrainian",
        ["Белорусский"] = "Belarusian",
        ["Немецкий"] = "German",
        ["Французский"] = "French",
        ["Испанский"] = "Spanish",
        ["Итальянский"] = "Italian",
        ["Португальский"] = "Portuguese",
        ["Китайский (упрощ.)"] = "Chinese (simpl.)",
        ["Китайский (трад.)"] = "Chinese (trad.)",
        ["Японский"] = "Japanese",
        ["Корейский"] = "Korean",
        ["Арабский"] = "Arabic",
        ["Турецкий"] = "Turkish",
        ["Польский"] = "Polish",
        ["Чешский"] = "Czech",
        ["Словацкий"] = "Slovak",
        ["Болгарский"] = "Bulgarian",
        ["Сербский"] = "Serbian",
        ["Хорватский"] = "Croatian",
        ["Греческий"] = "Greek",
        ["Иврит"] = "Hebrew",
        ["Хинди"] = "Hindi",
        ["Вьетнамский"] = "Vietnamese",
        ["Тайский"] = "Thai",
        ["Индонезийский"] = "Indonesian",
        ["Нидерландский"] = "Dutch",
        ["Шведский"] = "Swedish",
        ["Норвежский"] = "Norwegian",
        ["Датский"] = "Danish",
        ["Финский"] = "Finnish",
        ["Венгерский"] = "Hungarian",
        ["Румынский"] = "Romanian",
        ["Казахский"] = "Kazakh",
        ["Узбекский"] = "Uzbek",
        ["Азербайджанский"] = "Azerbaijani",
        ["Армянский"] = "Armenian",
        ["Грузинский"] = "Georgian",
        ["Латышский"] = "Latvian",
        ["Литовский"] = "Lithuanian",
        ["Эстонский"] = "Estonian",
        ["Персидский"] = "Persian",
        ["Татарский"] = "Tatar",
        ["Латынь"] = "Latin",
        ["Эсперанто"] = "Esperanto",
        ["Доступна версия {0}"] = "Version {0} is available",
        ["Открой остров и нажми «Обновить»"] = "Open the island and press “Update”",
        ["⬆ Обновить"] = "⬆ Update",
        ["Обновление"] = "Update",
        ["Нет доступных обновлений"] = "No updates available",
        ["Не удалось скачать обновление"] = "Couldn't download the update",
        ["Версия {0}"] = "Version {0}",
        ["Обновить до {0}"] = "Update to {0}",
        ["Доступна новая версия {0}"] = "A new version {0} is available",
        ["Установлена последняя версия"] = "You have the latest version",
        ["Проверяю…"] = "Checking…",
        ["Не удалось проверить обновления"] = "Couldn't check for updates",
        ["Проверить обновления"] = "Check for updates",
        ["Обновить"] = "Update",
        ["Выкл"] = "Off",
    };
}
