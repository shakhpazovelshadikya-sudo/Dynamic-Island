namespace DynamicIsland;

public enum TimerState { Idle, Running, Paused }

/// <summary>Таймер обратного отсчёта. Считает по часам (конечное время), поэтому не «плывёт» вместе с UI.</summary>
public sealed class CountdownTimer
{
    public const int MaxMinutes = 180;

    private readonly Func<DateTime> _now;
    private DateTime _end;
    private TimeSpan _left = TimeSpan.FromMinutes(5);

    /// <param name="now">Источник времени (UTC). По умолчанию — системные часы; подменяется в тестах.</param>
    public CountdownTimer(Func<DateTime>? now = null) => _now = now ?? (() => DateTime.UtcNow);

    public TimeSpan Duration { get; private set; } = TimeSpan.FromMinutes(5);
    public TimerState State { get; private set; } = TimerState.Idle;

    public TimeSpan Remaining => State == TimerState.Running ? Clamp(_end - _now()) : _left;

    private static TimeSpan Clamp(TimeSpan t) => t < TimeSpan.Zero ? TimeSpan.Zero : t;

    /// <summary>Задать длительность (сбрасывает таймер).</summary>
    public void SetMinutes(int minutes)
    {
        Duration = TimeSpan.FromMinutes(Math.Clamp(minutes, 1, MaxMinutes));
        Reset();
    }

    public void Start()
    {
        if (State == TimerState.Running) return;
        if (_left <= TimeSpan.Zero) _left = Duration;
        _end = _now() + _left;
        State = TimerState.Running;
    }

    public void Pause()
    {
        if (State != TimerState.Running) return;
        _left = Remaining;
        State = TimerState.Paused;
    }

    public void Reset()
    {
        State = TimerState.Idle;
        _left = Duration;
    }

    /// <summary>±минуты: в покое меняет длительность, на ходу — оставшееся время.</summary>
    public void Adjust(int deltaMinutes)
    {
        if (State == TimerState.Idle)
        {
            SetMinutes((int)Duration.TotalMinutes + deltaMinutes);
            return;
        }

        var left = Remaining + TimeSpan.FromMinutes(deltaMinutes);
        var min = TimeSpan.FromSeconds(5);
        var max = TimeSpan.FromMinutes(MaxMinutes);
        left = left < min ? min : left > max ? max : left;

        _left = left;
        if (State == TimerState.Running) _end = _now() + left;
    }

    /// <summary>true один раз — когда время вышло (после этого таймер возвращается в исходное состояние).</summary>
    public bool Poll()
    {
        if (State != TimerState.Running || Remaining > TimeSpan.Zero) return false;
        Reset();
        return true;
    }

    public static string Format(TimeSpan t)
    {
        int s = (int)Math.Ceiling(t.TotalSeconds);
        return s >= 3600 ? $"{s / 3600}:{s % 3600 / 60:00}:{s % 60:00}" : $"{s / 60:00}:{s % 60:00}";
    }
}
