using DynamicIsland;
using Xunit;

namespace DynamicIsland.Tests;

public class TabCatalogTests
{
    [Fact]
    public void Normalize_Null_GivesAllTabsInDefaultOrder() =>
        Assert.Equal(new[] { "main", "weather", "calendar", "shelf", "timer", "clipboard", "system", "translate" }, TabCatalog.Normalize(null));

    [Fact]
    public void Normalize_KeepsCustomOrder() =>
        Assert.Equal(new[] { "main", "timer", "weather" }, TabCatalog.Normalize(new[] { "main", "timer", "weather" }));

    [Fact]
    public void Normalize_DropsUnknownAndDuplicates() =>
        Assert.Equal(new[] { "main", "shelf" }, TabCatalog.Normalize(new[] { "main", "bogus", "shelf", "shelf", "" }));

    [Fact]
    public void Normalize_AlwaysContainsMain_AtFrontWhenMissing() =>
        Assert.Equal(new[] { "main", "system" }, TabCatalog.Normalize(new[] { "system" }));

    [Fact]
    public void Normalize_EmptyList_GivesOnlyMain() =>
        Assert.Equal(new[] { "main" }, TabCatalog.Normalize(Array.Empty<string>()));

    [Fact]
    public void IndexOf_MatchesCatalogOrder()
    {
        Assert.Equal(0, TabCatalog.IndexOf("main"));
        Assert.Equal(3, TabCatalog.IndexOf("shelf"));
        Assert.Equal(6, TabCatalog.IndexOf("system"));
        Assert.Equal(7, TabCatalog.IndexOf("translate"));
        Assert.Equal(-1, TabCatalog.IndexOf("nope"));
    }

    [Fact]
    public void AppSettings_DefaultTabs_AreAllTabs() =>
        Assert.Equal(TabCatalog.All.Select(t => t.Id), new AppSettings().Tabs);
}
