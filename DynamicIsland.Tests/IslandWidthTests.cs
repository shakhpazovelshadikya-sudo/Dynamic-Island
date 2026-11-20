using DynamicIsland;
using Xunit;

namespace DynamicIsland.Tests;

public class IslandWidthTests
{
    [Fact]
    public void ShortText_UsesMinimum() =>
        Assert.Equal(160, MainWindow.FitWidth(40, 60, 160, 380));

    [Fact]
    public void LongText_GrowsToFit() =>
        Assert.Equal(300, MainWindow.FitWidth(240, 60, 160, 380));

    [Fact]
    public void VeryLongText_IsCappedAtMaximum() =>
        Assert.Equal(380, MainWindow.FitWidth(2000, 60, 160, 380));

    [Fact]
    public void Marquee_NotNeeded_WhenTextFits() =>
        Assert.Equal(0, MainWindow.MarqueeDistance(200, 200));

    [Fact]
    public void Marquee_IgnoresSubPixelOverflow() =>
        Assert.Equal(0, MainWindow.MarqueeDistance(200.8, 200));

    [Fact]
    public void Marquee_DistanceIsOverflowPlusPadding() =>
        Assert.Equal(100 + 6, MainWindow.MarqueeDistance(300, 200));

    [Fact]
    public void FractionalWidth_RoundsUp() =>
        Assert.Equal(201, MainWindow.FitWidth(140.2, 60, 160, 380));
}
