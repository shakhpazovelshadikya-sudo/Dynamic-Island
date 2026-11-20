using DynamicIsland;
using Xunit;

namespace DynamicIsland.Tests;

public class TranslateServiceTests
{
    [Fact]
    public void Parse_JoinsSegments_AndReadsDetectedLanguage()
    {
        string json = "[[[\"Hello \",\"Привет \",null,null,1],[\"world\",\"мир\",null,null,1]],null,\"ru\"]";
        var (text, det) = TranslateService.ParseResponse(json);
        Assert.Equal("Hello world", text);
        Assert.Equal("ru", det);
    }

    [Fact]
    public void Parse_EmptyOrUnexpected_GivesEmptyText()
    {
        Assert.Equal("", TranslateService.ParseResponse("[]").Text);
        Assert.Equal("", TranslateService.ParseResponse("{}").Text);
    }

    [Fact]
    public void Languages_HaveUniqueCodes_AndNamesAreResolved()
    {
        Assert.Equal(TranslateService.Languages.Length, TranslateService.Languages.Select(l => l.Code).Distinct().Count());
        Assert.Equal("Русский", TranslateService.NameOf("ru"));
        Assert.Equal("Авто", TranslateService.NameOf("auto"));
    }
}
