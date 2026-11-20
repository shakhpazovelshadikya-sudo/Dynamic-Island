using System.Threading;
using System.IO;
using Windows.Media.Control;

namespace DynamicIsland;

public class MediaInfo
{
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public bool IsPlaying { get; set; }
    public bool HasSession { get; set; }
    public byte[]? Cover { get; set; }

    /// <summary>Название плеера (Spotify, Chrome…) и сколько плееров сейчас видит Windows.</summary>
    public string Source { get; set; } = "";
    public string SourceId { get; set; } = "";
    public int PlayerCount { get; set; }

    /// <summary>Позиция в момент CapturedAt и длительность трека (0 — трек без шкалы, например прямой эфир).</summary>
    public TimeSpan Position { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public bool CanSeek { get; set; }

    /// <summary>Текущая позиция с учётом времени, прошедшего с последнего опроса.</summary>
    public TimeSpan CurrentPosition
    {
        get
        {
            var p = Position;
            if (IsPlaying) p += DateTime.UtcNow - CapturedAt;
            if (p < TimeSpan.Zero) p = TimeSpan.Zero;
            if (Duration > TimeSpan.Zero && p > Duration) p = Duration;
            return p;
        }
    }
}

public record PlayerInfo(string Id, string Name, bool IsPlaying);

public class MediaService
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private string? _pinnedId;
    private int _version;

    // обложку перечитываем только при смене трека
    private string _coverKey = "";
    private byte[]? _cover;

    public event Action<MediaInfo>? Changed;

    /// <summary>Закреплённый плеер (null — тот, что считает текущим сама Windows).</summary>
    public string? PinnedId => _pinnedId;

    public async Task InitAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.CurrentSessionChanged += (_, _) => Attach();
        _manager.SessionsChanged += (_, _) => Attach();
        Attach();
    }

    private void OnPropsChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) => _ = RefreshAsync();
    private void OnPlaybackChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) => _ = RefreshAsync();
    private void OnTimelineChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args) => _ = RefreshAsync();

    private void Attach()
    {
        try
        {
            GlobalSystemMediaTransportControlsSession? next = null;
            if (_pinnedId != null)
                next = _manager?.GetSessions().FirstOrDefault(x => x.SourceAppUserModelId == _pinnedId);
            next ??= _manager?.GetCurrentSession();

            // тот же плеер — события уже подписаны
            if (next != null && _session != null && next.SourceAppUserModelId == _session.SourceAppUserModelId)
            {
                _ = RefreshAsync();
                return;
            }

            if (_session != null)
            {
                _session.MediaPropertiesChanged -= OnPropsChanged;
                _session.PlaybackInfoChanged -= OnPlaybackChanged;
                _session.TimelinePropertiesChanged -= OnTimelineChanged;
            }

            _session = next;
            _coverKey = "";
            _cover = null;

            if (_session != null)
            {
                _session.MediaPropertiesChanged += OnPropsChanged;
                _session.PlaybackInfoChanged += OnPlaybackChanged;
                _session.TimelinePropertiesChanged += OnTimelineChanged;
            }
        }
        catch (Exception ex) { AppLog.Error("MediaService.Attach", ex); }

        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        int version = Interlocked.Increment(ref _version);
        var s = _session;
        if (s == null)
        {
            Changed?.Invoke(new MediaInfo { PlayerCount = SafePlayerCount() });
            return;
        }

        try
        {
            var props = await s.TryGetMediaPropertiesAsync();
            var playback = s.GetPlaybackInfo();
            var tl = s.GetTimelineProperties();
            bool playing = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            string key = $"{s.SourceAppUserModelId}|{props.Title}|{props.Artist}|{props.AlbumTitle}";
            if (key != _coverKey || _cover == null)
            {
                _cover = await LoadCoverAsync(props);
                _coverKey = key;
            }

            if (version != _version) return; // пришло более свежее обновление

            var duration = tl.EndTime - tl.StartTime;
            var position = tl.Position - tl.StartTime;
            if (playing)
            {
                var elapsed = DateTimeOffset.Now - tl.LastUpdatedTime;
                if (elapsed > TimeSpan.Zero && elapsed < TimeSpan.FromHours(6))
                    position += TimeSpan.FromTicks((long)(elapsed.Ticks * (playback.PlaybackRate ?? 1.0)));
            }

            bool hasTimeline = duration > TimeSpan.Zero;
            Changed?.Invoke(new MediaInfo
            {
                Title = props.Title ?? "",
                Artist = props.Artist ?? "",
                IsPlaying = playing,
                HasSession = true,
                Cover = _cover,
                Source = PrettyName(s.SourceAppUserModelId),
                SourceId = s.SourceAppUserModelId,
                PlayerCount = SafePlayerCount(),
                Position = hasTimeline ? position : TimeSpan.Zero,
                Duration = hasTimeline ? duration : TimeSpan.Zero,
                CapturedAt = DateTime.UtcNow,
                CanSeek = hasTimeline && playback.Controls.IsPlaybackPositionEnabled
            });
        }
        catch (Exception ex) { AppLog.Error("MediaService.RefreshAsync", ex); }
    }

    private static async Task<byte[]?> LoadCoverAsync(GlobalSystemMediaTransportControlsSessionMediaProperties props)
    {
        if (props.Thumbnail == null) return null;
        try
        {
            using var rs = await props.Thumbnail.OpenReadAsync();
            using var stream = rs.AsStreamForRead();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return ms.Length > 0 ? ms.ToArray() : null;
        }
        catch (Exception ex)
        {
            AppLog.Error("MediaService.LoadCoverAsync", ex);
            return null;
        }
    }

    // ---------- Выбор плеера ----------
    private int SafePlayerCount()
    {
        try { return _manager?.GetSessions().Count ?? 0; }
        catch { return 0; }
    }

    public List<PlayerInfo> GetPlayers()
    {
        var list = new List<PlayerInfo>();
        try
        {
            if (_manager == null) return list;
            foreach (var s in _manager.GetSessions())
            {
                bool playing = s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                list.Add(new PlayerInfo(s.SourceAppUserModelId, PrettyName(s.SourceAppUserModelId), playing));
            }
        }
        catch (Exception ex) { AppLog.Error("MediaService.GetPlayers", ex); }
        return list;
    }

    /// <summary>Закрепить плеер по его идентификатору; null — вернуться к выбору Windows.</summary>
    public void SelectPlayer(string? id)
    {
        _pinnedId = id;
        Attach();
    }

    private static readonly Dictionary<string, string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zunemusic"] = "Медиаплеер",
        ["zunevideo"] = "Кино и ТВ",
        ["msedge"] = "Edge",
        ["chrome"] = "Chrome",
        ["firefox"] = "Firefox",
        ["yandexmusic"] = "Яндекс Музыка",
        ["vlc"] = "VLC"
    };

    public static string PrettyName(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return Loc.T("Плеер");

        string name = id.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? id[..^4]
            : NotificationService.AppName(id);

        if (Known.TryGetValue(name, out var known)) return Loc.T(known);
        return name.Length > 0 ? char.ToUpper(name[0]) + name[1..] : Loc.T("Плеер");
    }

    // ---------- Управление ----------
    public async Task PlayPauseAsync()
    {
        if (_session != null) await _session.TryTogglePlayPauseAsync();
    }

    public async Task NextAsync()
    {
        if (_session != null) await _session.TrySkipNextAsync();
    }

    public async Task PrevAsync()
    {
        if (_session != null) await _session.TrySkipPreviousAsync();
    }

    /// <summary>Перемотка на позицию от начала трека.</summary>
    public async Task SeekAsync(TimeSpan position)
    {
        try
        {
            if (_session != null) await _session.TryChangePlaybackPositionAsync(position.Ticks);
        }
        catch (Exception ex) { AppLog.Error("MediaService.SeekAsync", ex); }
    }
}
