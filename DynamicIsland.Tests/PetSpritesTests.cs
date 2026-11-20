using DynamicIsland;
using Xunit;

namespace DynamicIsland.Tests;

public class PetSpritesTests
{
    public static IEnumerable<object[]> AllPets() => PetSprites.All.Select(p => new object[] { p.Id });

    [Fact]
    public void HasAtLeastThreePets_WithUniqueIds()
    {
        Assert.True(PetSprites.All.Count >= 3);
        Assert.Equal(PetSprites.All.Count, PetSprites.All.Select(p => p.Id).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(AllPets))]
    public void Sprites_AreValid(string id)
    {
        var pet = PetSprites.Find(id)!;
        Assert.Null(PetSprites.Validate(pet));
    }

    [Theory]
    [MemberData(nameof(AllPets))]
    public void EveryFrame_HasVisiblePixels(string id)
    {
        var pet = PetSprites.Find(id)!;
        foreach (var key in PetSprites.FrameKeys)
            Assert.Contains(pet.Frames[key], row => row.Any(c => c != '.'));
    }

    [Theory]
    [MemberData(nameof(AllPets))]
    public void SitAndBlinkFrames_Differ(string id)
    {
        var pet = PetSprites.Find(id)!;
        Assert.NotEqual(pet.Frames["sit0"], pet.Frames["sit1"]);
    }

    [Fact]
    public void Find_UnknownOrEmpty_ReturnsNull()
    {
        Assert.Null(PetSprites.Find(""));
        Assert.Null(PetSprites.Find(null));
        Assert.Null(PetSprites.Find("dragon"));
    }

    [Fact]
    public void SkyPet_ShowsSunByDay_AndMoonByNight()
    {
        var sky = PetSprites.Find("sky")!;
        Assert.Null(PetSprites.Validate(PetSprites.Moon));
        Assert.Equal("sky", PetSprites.Resolve(sky, new DateTime(2026, 1, 1, 12, 0, 0)).Id);
        Assert.Equal("moon", PetSprites.Resolve(sky, new DateTime(2026, 1, 1, 23, 0, 0)).Id);
        Assert.Equal("moon", PetSprites.Resolve(sky, new DateTime(2026, 1, 1, 3, 0, 0)).Id);
        Assert.Equal("cat", PetSprites.Resolve(PetSprites.Find("cat")!, new DateTime(2026, 1, 1, 23, 0, 0)).Id);
    }

    [Fact]
    public void DefaultSetting_IsCat() => Assert.Equal("cat", new AppSettings().Pet);
}
