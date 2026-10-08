using System;
using System.Collections.Generic;
using System.Linq;
using OctoShoots.Core.Items;
using OctoShoots.Core.Run;
using Xunit;

namespace OctoShoots.Core.Tests;

public class ItemTests
{
    static readonly Lazy<ItemCatalog> Shared = new(() =>
        ItemCatalog.FromJson(System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static ItemCatalog Catalog => Shared.Value;

    [Fact]
    public void CatalogLoadsTheFullSet()
    {
        Assert.Equal(63, Catalog.Items.Count);
        Assert.Equal(14, Catalog.Synergies.Count);
        Assert.Equal(5, Catalog.Transformations.Count);
        Assert.Equal(11, Catalog.Items.Count(i => i.Kind == ItemKind.Active));
    }

    [Fact]
    public void EveryItemDescribesItsEffects()
    {
        foreach (var item in Catalog.Items)
            Assert.True(ItemCaption.Describe(item).Count > 0, $"{item.Id} has no caption lines");
    }

    [Fact]
    public void EveryItemHasItsOwnPearl()
    {
        var looks = Catalog.Items.Select(i => string.Join(",", i.Pearl!.Colors) + i.Pearl.Pattern).ToList();
        Assert.Equal(looks.Count, looks.Distinct().Count());
    }

    [Fact]
    public void EveryTransformationCanBeReached()
    {
        foreach (var t in Catalog.Transformations)
            Assert.True(Catalog.Items.Count(i => i.Tags.Contains(t.Tag)) >= t.Count, t.Id);
    }

    [Theory]
    [InlineData("""{ "items": [ { "id": "a", "name": "A", "pools": ["treasure"] }, { "id": "a", "name": "A2", "pools": ["treasure"] } ] }""", "defined twice")]
    [InlineData("""{ "items": [ { "id": "a", "name": "A", "pools": ["moon"] } ] }""", "unknown pool")]
    [InlineData("""{ "items": [ { "id": "a", "name": "A", "pools": ["treasure"], "tags": ["laser"] } ] }""", "unknown tag")]
    [InlineData("""{ "items": [ { "id": "a", "name": "A", "pools": ["treasure"] } ], "synergies": [ { "id": "s", "requires": ["a", "ghost"] } ] }""", "unknown item 'ghost'")]
    [InlineData("""{ "items": [ { "id": "a", "name": "A", "kind": "active", "pools": ["treasure"] } ] }""", "no recharge time")]
    [InlineData("""{ "items": [ { "id": "a", "name": "A", "pools": ["treasure"] } ] }""", "no pearl look")]
    [InlineData("""{ "items": [ { "id": "a", "name": "A", "pools": ["treasure"], "pearl": { "colors": ["red"] } } ] }""", "pearl needs 2–3 colours")]
    [InlineData("""{ "items": [ { "id": "a", "name": "A", "pools": ["treasure"] } ], "transformations": [ { "id": "t", "tag": "coral", "count": 3 } ] }""", "only 0 exist")]
    public void ValidationReportsBrokenData(string json, string expected)
    {
        var e = Assert.Throws<InvalidOperationException>(() => ItemCatalog.FromJson(json));
        Assert.Contains(expected, e.Message);
    }

    [Fact]
    public void AddsApplyBeforeMultipliers()
    {
        var tuning = new Tuning();
        var loadout = Loadout.Build(Catalog, new[] { "pufferfish_pout", "shark_tooth" }, tuning);
        Assert.Equal((3.5f + 0.5f) * 1.5f, loadout.Stats.Damage, 4);
        // Order of pickup doesn't matter.
        var reversed = Loadout.Build(Catalog, new[] { "shark_tooth", "pufferfish_pout" }, tuning);
        Assert.Equal(loadout.Stats.Damage, reversed.Stats.Damage);
    }

    [Fact]
    public void StatsAreClamped()
    {
        var tuning = new Tuning();
        var slow = Loadout.Build(Catalog, Enumerable.Repeat("barnacle_armor", 20).ToList(), tuning);
        Assert.Equal(0.4f, slow.Stats.Speed);
    }

    [Fact]
    public void ShotModifiersCompose()
    {
        var loadout = Loadout.Build(Catalog, new[] { "triple_tentacle", "double_helix", "anglerfish_lure", "swordfish_bill" }, new Tuning());
        Assert.Equal(4, loadout.Shot.Multishot);
        Assert.True(loadout.Shot.Wave && loadout.Shot.Homing && loadout.Shot.Pierce);
        Assert.Equal(0.8f, loadout.Shot.DamageMult, 4);
    }

    [Fact]
    public void ChancesCombineInsteadOfAdding()
    {
        var a = new ShotSpec { FreezeChance = 0.5f };
        a.MergeFrom(new ShotSpec { FreezeChance = 0.5f });
        Assert.Equal(0.75f, a.FreezeChance, 4);
    }

    [Fact]
    public void SynergyNeedsBothItems()
    {
        var tuning = new Tuning();
        Assert.DoesNotContain("pinball_storm", Loadout.Build(Catalog, new[] { "electric_eel_tail" }, tuning).Synergies);
        Assert.Contains("pinball_storm", Loadout.Build(Catalog, new[] { "electric_eel_tail", "mirror_scale" }, tuning).Synergies);
    }

    [Fact]
    public void ThreeTaggedItemsTransform()
    {
        var tuning = new Tuning();
        var two = Loadout.Build(Catalog, new[] { "shark_tooth", "swordfish_bill" }, tuning);
        Assert.Empty(two.Transformations);
        var three = Loadout.Build(Catalog, new[] { "shark_tooth", "swordfish_bill", "stingray_barb" }, tuning);
        Assert.Contains("shark_mode", three.Transformations);
        Assert.Equal(1f + 0.2f, three.Stats.Speed, 4);
    }

    [Fact]
    public void LastActivePickedIsHeld()
    {
        var loadout = Loadout.Build(Catalog, new[] { "conch_horn", "shark_tooth", "whale_song" }, new Tuning());
        Assert.Equal("whale_song", loadout.Active?.Id);
    }

    [Theory]
    [InlineData("pufferfish_pout", "×1.5 damage")]
    [InlineData("pufferfish_pout", "−0.2 shot speed")]
    [InlineData("coral_crown", "+20 max HP")]
    [InlineData("frost_kelp", "12% chance to freeze foes for 1.6s")]
    [InlineData("conch_horn", "Recharges in 30s")]
    [InlineData("lamprey_mouth", "Kills heal 3 HP")]
    [InlineData("whale_lung", "No idle sinking")]
    [InlineData("hammerhead", "Fires a 5-pellet cone")]
    [InlineData("barnacle_armor", "+30 foam HP")]
    public void CaptionsListRealNumbers(string item, string line)
    {
        Assert.Contains(line, ItemCaption.Describe(Catalog[item]));
    }

    [Fact]
    public void PoolDrawsAreSeededAndRespectExclusions()
    {
        var unlocked = new HashSet<string>();
        var streams = new RunStreams(SeedCode.Parse("KELP 7Q2Z"));
        var first = ItemPools.Draw(Catalog, "treasure", streams.Items(1, 1, 4), unlocked, new HashSet<string>());
        var again = ItemPools.Draw(Catalog, "treasure", new RunStreams(SeedCode.Parse("KELP 7Q2Z")).Items(1, 1, 4), unlocked, new HashSet<string>());
        Assert.NotNull(first);
        Assert.Equal(first!.Id, again!.Id);

        var excluded = new HashSet<string> { first.Id };
        var other = ItemPools.Draw(Catalog, "treasure", streams.Items(1, 1, 4), unlocked, excluded);
        Assert.NotEqual(first.Id, other!.Id);
    }

    [Fact]
    public void LockedItemsStayOutUntilUnlocked()
    {
        var rng = new Rng(1);
        var none = new HashSet<string>();
        for (int i = 0; i < 500; i++)
        {
            var item = ItemPools.Draw(Catalog, "treasure", rng, none, none)!;
            Assert.Null(item.Unlock);
        }

        var everythingButLamprey = Catalog.Items.Where(i => i.Id != "lamprey_mouth").Select(i => i.Id).ToHashSet();
        Assert.Null(ItemPools.Draw(Catalog, "treasure", new Rng(2), none, everythingButLamprey));
        var drawn = ItemPools.Draw(Catalog, "treasure", new Rng(2), new HashSet<string> { "hollow_maw" }, everythingButLamprey);
        Assert.Equal("lamprey_mouth", drawn?.Id);
    }

    [Fact]
    public void ExhaustedPoolFallsBackToTreasureThenNull()
    {
        var unlocked = new HashSet<string>();
        var allShop = Catalog.Items.Where(i => i.Pools.Contains("shop")).Select(i => i.Id).ToHashSet();
        var fallback = ItemPools.Draw(Catalog, "shop", new Rng(3), unlocked, allShop);
        Assert.NotNull(fallback);
        Assert.Contains("treasure", fallback!.Pools);

        var everything = Catalog.Items.Select(i => i.Id).ToHashSet();
        Assert.Null(ItemPools.Draw(Catalog, "shop", new Rng(3), unlocked, everything));
    }
}
