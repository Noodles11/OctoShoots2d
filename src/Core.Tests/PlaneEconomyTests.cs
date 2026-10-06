using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Shells (the currency): where they lie, what mobs drop, and what the shop sells for them.</summary>
public class PlaneEconomyTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly Lazy<LevelMap> Room1 = new(() => TopDownGenerator.Generate(new RunStreams(SeedCode.Parse("KELP7Q2Z")), 1, 1));

    static PlaneWorld World() => new(Room1.Value, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()));

    [Fact]
    public void ShellCachesLieAtTheItemCacheAndSecrets()
    {
        var w = World();
        int expectedMin = w.Map.Pois.Sum(p => p.Kind switch
        {
            PoiKind.ItemSpawn => PlaneEconomyTuning.CacheMin,
            PoiKind.Secret => PlaneEconomyTuning.SecretMin,
            _ => 0,
        });
        Assert.True(w.Shells.Count >= expectedMin, $"{w.Shells.Count} shells");
        Assert.All(w.Shells, s => Assert.True(w.Map.IsOpen(s.Position)));
    }

    [Fact]
    public void SwimmingOverShellsCollectsThem()
    {
        var w = World();
        var shell = w.Shells[0];
        w.Player.Position = w.Player.PrevPosition = shell.Position;
        w.Step(default);
        Assert.True(w.Run.Shells >= 1);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.ShellCollected);
        Assert.Equal(w.Run.Shells, w.Stats.ShellsCollected);
    }

    [Fact]
    public void EveryDefeatedMobDropsShells()
    {
        var w = World();
        int before = w.Shells.Count;
        var mob = w.Mobs[0];
        w.Shots.Add(new PlaneShot { Position = mob.Position, Line = mob.Position, Velocity = Vector2.UnitX, Life = 1f, FromPlayer = true, Damage = 999f });
        w.Step(default);
        Assert.False(mob.Alive);
        Assert.Equal(1, w.Stats.MobsDefeated);
        int dropped = w.Shells.Count - before;
        Assert.InRange(dropped, PlaneEconomyTuning.MobDropMin, PlaneEconomyTuning.MobDropMax);
    }

    [Fact]
    public void TheShopSellsPearlsAndATopUpForShells()
    {
        var w = World();
        Assert.Contains(w.Stands, s => s.Kind == StandKind.Health);
        var pearl = w.Stands.First(s => s.Kind == StandKind.Pearl);
        Assert.DoesNotContain(w.Pearls, p => p.ItemId == pearl.ItemId);

        // Too poor: turned away, nothing sold.
        w.Player.Position = w.Player.PrevPosition = pearl.Position;
        w.Step(default);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.CannotAfford);
        Assert.False(pearl.Sold);

        w.Run.Shells = PlaneEconomyTuning.PearlPrice + 3;
        w.Step(default);
        Assert.True(pearl.Sold);
        Assert.Equal(3, w.Run.Shells);
        Assert.Contains(pearl.ItemId, w.Run.Items);
    }

    [Fact]
    public void TheTopUpIsOnlySoldToSomeoneHurt()
    {
        var w = World();
        var health = w.Stands.Single(s => s.Kind == StandKind.Health);
        w.Run.Shells = 50;
        w.Player.Position = w.Player.PrevPosition = health.Position;
        w.Step(default);
        Assert.False(health.Sold);
        w.Player.Hp = 40f;
        w.Step(default);
        Assert.True(health.Sold);
        Assert.Equal(40f + PlaneEconomyTuning.HealthAmount, w.Player.Hp);
        Assert.Equal(50 - PlaneEconomyTuning.HealthPrice, w.Run.Shells);
    }

    [Fact]
    public void ShellsCarryThroughTheRift()
    {
        var run = new PlaneRun(Catalog.Value, new Tuning()) { Shells = 12 };
        var room2 = TopDownGenerator.Generate(new RunStreams(SeedCode.Parse("KELP7Q2Z")), 1, 2);
        var w = new PlaneWorld(room2, new Tuning(), run);
        Assert.Equal(12, w.Run.Shells);
    }
}
