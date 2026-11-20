using DynamicIsland;
using Xunit;

namespace DynamicIsland.Tests;

public class UpdateServiceTests
{
    private const string Json = """{"tag_name":"v1.2.0","assets":[{"name":"DynamicIsland.exe","browser_download_url":"https://x/a"},{"name":"DinamicIslandsetap.exe","browser_download_url":"https://x/setup"}]}""";

    [Fact]
    public void Parse_NewerVersion_ReturnsSetupAsset()
    {
        var u = UpdateService.Parse(Json, new Version(1, 0, 0));
        Assert.NotNull(u);
        Assert.Equal(new Version(1, 2, 0), u!.Version);
        Assert.Equal("https://x/setup", u.DownloadUrl);
    }

    [Fact]
    public void Parse_SameOrOlderVersion_ReturnsNull()
    {
        Assert.Null(UpdateService.Parse(Json, new Version(1, 2, 0)));
        Assert.Null(UpdateService.Parse(Json, new Version(2, 0, 0)));
    }

    [Fact]
    public void Parse_NoSetupAsset_ReturnsNull() =>
        Assert.Null(UpdateService.Parse("""{"tag_name":"v9.0.0","assets":[]}""", new Version(1, 0, 0)));
}
