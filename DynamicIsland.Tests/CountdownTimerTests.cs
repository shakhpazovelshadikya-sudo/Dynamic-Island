using DynamicIsland;
using Xunit;

namespace DynamicIsland.Tests;

public class CountdownTimerTests
{
    private sealed class FakeClock
    {
        public DateTime Now { get; set; } = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        public void Advance(TimeSpan t) => Now += t;
    }

    private static (CountdownTimer Timer, FakeClock Clock) Make()
    {
        var clock = new FakeClock();
        return (new CountdownTimer(() => clock.Now), clock);
    }

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(59, "00:59")]
    [InlineData(60, "01:00")]
    [InlineData(300, "05:00")]
    [InlineData(3599, "59:59")]
    [InlineData(3600, "1:00:00")]
    [InlineData(10800, "3:00:00")]
    public void Format_Seconds(int seconds, string expected) =>
        Assert.Equal(expected, CountdownTimer.Format(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void Format_RoundsPartialSecondsUp() =>
        Assert.Equal("00:05", CountdownTimer.Format(TimeSpan.FromSeconds(4.2)));

    [Fact]
    public void NewTimer_IsIdle_FiveMinutes()
    {
        var (t, _) = Make();
        Assert.Equal(TimerState.Idle, t.State);
        Assert.Equal(TimeSpan.FromMinutes(5), t.Remaining);
    }

    [Theory]
    [InlineData(-10, 1)]
    [InlineData(0, 1)]
    [InlineData(25, 25)]
    [InlineData(999, CountdownTimer.MaxMinutes)]
    public void SetMinutes_IsClamped(int input, int expected)
    {
        var (t, _) = Make();
        t.SetMinutes(input);
        Assert.Equal(TimeSpan.FromMinutes(expected), t.Duration);
        Assert.Equal(TimeSpan.FromMinutes(expected), t.Remaining);
    }

    [Fact]
    public void Start_CountsDownByClock()
    {
        var (t, clock) = Make();
        t.Start();
        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(TimerState.Running, t.State);
        Assert.Equal(TimeSpan.FromMinutes(3), t.Remaining);
    }

    [Fact]
    public void Pause_FreezesRemaining_ResumeContinues()
    {
        var (t, clock) = Make();
        t.Start();
        clock.Advance(TimeSpan.FromMinutes(1));
        t.Pause();
        clock.Advance(TimeSpan.FromMinutes(10)); // пока на паузе время не идёт

        Assert.Equal(TimerState.Paused, t.State);
        Assert.Equal(TimeSpan.FromMinutes(4), t.Remaining);

        t.Start();
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(TimeSpan.FromMinutes(3), t.Remaining);
    }

    [Fact]
    public void Reset_ReturnsToFullDuration()
    {
        var (t, clock) = Make();
        t.SetMinutes(10);
        t.Start();
        clock.Advance(TimeSpan.FromMinutes(4));
        t.Reset();
        Assert.Equal(TimerState.Idle, t.State);
        Assert.Equal(TimeSpan.FromMinutes(10), t.Remaining);
    }

    [Fact]
    public void Adjust_WhenIdle_ChangesDuration()
    {
        var (t, _) = Make();
        t.Adjust(+5);
        Assert.Equal(TimeSpan.FromMinutes(10), t.Duration);
        t.Adjust(-100);
        Assert.Equal(TimeSpan.FromMinutes(1), t.Duration);
    }

    [Fact]
    public void Adjust_WhenRunning_ChangesRemaining()
    {
        var (t, clock) = Make();
        t.Start();
        clock.Advance(TimeSpan.FromMinutes(1)); // осталось 4
        t.Adjust(+2);
        Assert.Equal(TimeSpan.FromMinutes(6), t.Remaining);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(TimeSpan.FromMinutes(5), t.Remaining);
    }

    [Fact]
    public void Adjust_WhenRunning_KeepsAtLeastFiveSeconds()
    {
        var (t, _) = Make();
        t.Start();
        t.Adjust(-100);
        Assert.Equal(TimeSpan.FromSeconds(5), t.Remaining);
    }

    [Fact]
    public void Poll_FiresOnce_ThenResets()
    {
        var (t, clock) = Make();
        t.SetMinutes(1);
        t.Start();

        Assert.False(t.Poll());
        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.True(t.Poll());
        Assert.False(t.Poll());
        Assert.Equal(TimerState.Idle, t.State);
        Assert.Equal(TimeSpan.FromMinutes(1), t.Remaining);
    }

    [Fact]
    public void Remaining_NeverNegative()
    {
        var (t, clock) = Make();
        t.Start();
        clock.Advance(TimeSpan.FromHours(5));
        Assert.Equal(TimeSpan.Zero, t.Remaining);
    }
}
