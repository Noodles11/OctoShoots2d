using System;
using System.IO;
using OctoShoots.Core.Creatures;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>creatures.json: the Depth 1 creatures' numbers, waiting to be ported onto the plane.</summary>
public class CreatureCatalogTests
{
    static readonly Lazy<CreatureCatalog> Shared = new(() =>
        CreatureCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "creatures.json"))));

    [Fact]
    public void TheCatalogCoversEveryCreatureAndRejectsShortTelegraphs()
    {
        var catalog = Shared.Value;
        foreach (var kind in CreatureCatalog.Kinds) Assert.True(catalog[kind].Telegraph >= 0.45f, kind.ToString());
        Assert.Equal(30f, catalog[EnemyKind.Barracuda].NoticeRange);
        var json = catalog.ToJson().Replace("\"telegraph\": 0.8,", "\"telegraph\": 0.2,");
        Assert.Throws<InvalidOperationException>(() => CreatureCatalog.FromJson(json));
    }
}
