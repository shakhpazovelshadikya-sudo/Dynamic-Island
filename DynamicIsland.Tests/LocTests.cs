using DynamicIsland;
using Xunit;

namespace DynamicIsland.Tests;

public class LocTests
{
    [Fact]
    public void Resolve_ExplicitLanguages() { Assert.Equal("ru", Loc.Resolve("ru")); Assert.Equal("en", Loc.Resolve("en")); }

    [Fact]
    public void English_TranslatesKnownAndKeepsUnknown()
    {
        try
        {
            Loc.Init("en");
            Assert.Equal("Home", Loc.T("Главная"));
            Assert.Equal("week 47", Loc.F("неделя {0}", 47));
            Assert.Equal("нет такого ключа", Loc.T("нет такого ключа"));
        }
        finally { Loc.Init("ru"); }
    }

    [Fact]
    public void Russian_ReturnsSourceText()
    {
        Loc.Init("ru");
        Assert.Equal("Главная", Loc.T("Главная"));
    }
}
