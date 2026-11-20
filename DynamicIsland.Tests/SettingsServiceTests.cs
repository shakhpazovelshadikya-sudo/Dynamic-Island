using System.IO;
using DynamicIsland;
using Xunit;

namespace DynamicIsland.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "DynamicIslandTests", Guid.NewGuid().ToString("N"));
    private string File_ => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void Load_MissingFile_CreatesItWithDefaults()
    {
        var s = SettingsService.Load(File_);

        Assert.True(File.Exists(File_));
        Assert.Equal(6, s.TopMargin);
        Assert.True(s.ExpandOnTrackChange);
        Assert.True(s.ShowNotifications);
        Assert.False(s.HideInFullscreen);
        Assert.Equal("", s.MonitorDevice);
    }

    [Fact]
    public void Load_Defaults_IncludeWidthLimitsAndAllTabs()
    {
        var s = SettingsService.Load(File_);
        Assert.Equal(380, s.CompactMaxWidth);
        Assert.Equal(640, s.ExpandedMaxWidth);
        Assert.Equal(TabCatalog.All.Length, s.Tabs.Count);
    }

    [Fact]
    public void SaveThenLoad_KeepsTabOrderAndWidths()
    {
        var s = new AppSettings { CompactMaxWidth = 300, ExpandedMaxWidth = 700, Tabs = new() { "main", "timer", "system" } };
        SettingsService.Save(s, File_);

        var loaded = SettingsService.Load(File_);

        Assert.Equal(300, loaded.CompactMaxWidth);
        Assert.Equal(700, loaded.ExpandedMaxWidth);
        Assert.Equal(new[] { "main", "timer", "system" }, loaded.Tabs);
    }

    [Fact]
    public void Pet_IsSavedAndLoaded()
    {
        SettingsService.Save(new AppSettings { Pet = "slime" }, File_);
        Assert.Equal("slime", SettingsService.Load(File_).Pet);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var s = new AppSettings { TopMargin = 12, HideInFullscreen = true, VpnProfile = "Работа", MonitorDevice = @"\\.\DISPLAY2" };
        SettingsService.Save(s, File_);

        var loaded = SettingsService.Load(File_);

        Assert.Equal(12, loaded.TopMargin);
        Assert.True(loaded.HideInFullscreen);
        Assert.Equal("Работа", loaded.VpnProfile);
        Assert.Equal(@"\\.\DISPLAY2", loaded.MonitorDevice);
    }

    [Fact]
    public void Load_IsCaseInsensitive_AndFillsMissingWithDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(File_, "{ \"topmargin\": 20 }");

        var s = SettingsService.Load(File_);

        Assert.Equal(20, s.TopMargin);
        Assert.True(s.ClipboardHistory); // не указано в файле — значение по умолчанию
    }

    [Fact]
    public void Load_CorruptFile_FallsBackToDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(File_, "garbage {{{");

        var s = SettingsService.Load(File_);

        Assert.Equal(6, s.TopMargin);
    }
}
