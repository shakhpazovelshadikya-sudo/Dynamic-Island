using System.IO;
using DynamicIsland;
using Xunit;

namespace DynamicIsland.Tests;

public sealed class ShelfServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "DynamicIslandTests", Guid.NewGuid().ToString("N"));
    private string ShelfFile => Path.Combine(_dir, "shelf.json");

    public ShelfServiceTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private string MakeFile(string name)
    {
        string p = Path.Combine(_dir, name);
        File.WriteAllText(p, "x");
        return p;
    }

    [Fact]
    public void Add_ExistingFile_IsStored()
    {
        var shelf = new ShelfService(ShelfFile);
        string f = MakeFile("a.txt");

        Assert.Equal(1, shelf.Add(new[] { f }));
        Assert.Equal(new[] { f }, shelf.Items);
    }

    [Fact]
    public void Add_Directory_IsStored()
    {
        var shelf = new ShelfService(ShelfFile);
        Assert.Equal(1, shelf.Add(new[] { _dir }));
    }

    [Fact]
    public void Add_SkipsMissingBlankAndDuplicates()
    {
        var shelf = new ShelfService(ShelfFile);
        string f = MakeFile("a.txt");

        int added = shelf.Add(new[] { f, f.ToUpperInvariant(), "", "   ", Path.Combine(_dir, "nope.txt") });

        Assert.Equal(1, added);
        Assert.Single(shelf.Items);
    }

    [Fact]
    public void Add_StopsAtThirtyItems()
    {
        var shelf = new ShelfService(ShelfFile);
        var files = Enumerable.Range(0, 40).Select(i => MakeFile($"f{i}.txt")).ToList();

        Assert.Equal(30, shelf.Add(files));
        Assert.Equal(30, shelf.Items.Count);
        Assert.Equal(0, shelf.Add(new[] { MakeFile("extra.txt") }));
    }

    [Fact]
    public void Remove_IsCaseInsensitive()
    {
        var shelf = new ShelfService(ShelfFile);
        string f = MakeFile("a.txt");
        shelf.Add(new[] { f });

        shelf.Remove(f.ToUpperInvariant());

        Assert.Empty(shelf.Items);
    }

    [Fact]
    public void Items_PersistAcrossInstances()
    {
        string a = MakeFile("a.txt"), b = MakeFile("b.txt");
        new ShelfService(ShelfFile).Add(new[] { a, b });

        var reloaded = new ShelfService(ShelfFile);
        Assert.Equal(new[] { a, b }, reloaded.Items);
    }

    [Fact]
    public void CorruptFile_GivesEmptyShelf()
    {
        File.WriteAllText(ShelfFile, "{ not json");
        Assert.Empty(new ShelfService(ShelfFile).Items);
    }
}
