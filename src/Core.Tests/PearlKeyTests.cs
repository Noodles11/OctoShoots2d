using System;
using System.IO;
using System.Linq;
using OctoShoots.Core.Items;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>A pearl looks like what it does (PearlKey).</summary>
public class PearlKeyTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PearlLook Look(string id) => Catalog.Value[id].Pearl!;

    [Fact]
    public void APearlOfOneStatIsThatStatsColourWithAnArrow()
    {
        var shark = Look("shark_tooth");
        Assert.Equal(PearlIcon.Arrow, shark.Icon);
        Assert.Equal(PearlKey.Damage, shark.Colors[1]);
        Assert.Equal(PearlKey.ShotSpeed, Look("cuttlebone").Colors[1]);
        Assert.Equal(PearlKey.Health, Look("moon_jelly_heart").Colors[1]);
    }

    [Fact]
    public void APearlOfSeveralStatsMixesTheirColours_TheStrongestInTheMiddle()
    {
        // Coral Crown: +20 max HP (one step) and +0.3 damage (under one): green first, red beside it.
        var crown = Look("coral_crown");
        Assert.Equal(PearlKey.Health, crown.Colors[1]);
        Assert.Contains(PearlKey.Damage, crown.Colors);
        // A drawback colours nothing: Glass Squid (+1 damage, −20 max HP) is all red.
        Assert.DoesNotContain(PearlKey.Health, Look("glass_squid").Colors);
    }

    [Fact]
    public void TheIconTellsTheKindOfEffect()
    {
        Assert.Equal(PearlIcon.Chevron, Look("anglerfish_lure").Icon);
        Assert.Equal(PearlIcon.Swirl, Look("frost_kelp").Icon);
        Assert.Equal(PearlIcon.Nautilus, Look("lucky_sea_glass").Icon);
        Assert.Equal(PearlIcon.Feather, Look("dolphin_fin").Icon);
        Assert.Equal(PearlIcon.Shield, Look("divers_watch").Icon);
        Assert.Equal(PearlIcon.Star, Look("tide_pulse").Icon);
    }

    [Fact]
    public void EveryPearlHasTwoOrThreeColoursFromTheKey()
    {
        foreach (var item in Catalog.Value.Items)
        {
            Assert.InRange(item.Pearl!.Colors.Count, 2, 3);
            Assert.True(item.Pearl.Colors.All(c => c.Length == 7 && c[0] == '#'), item.Id);
        }
    }
}
