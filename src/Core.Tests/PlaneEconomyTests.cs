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

    /// <summary>A level with a shop (level 1 of a depth never has one).</summary>
    static PlaneWorld World() => new(TestLevels.WithShop(), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()));

    [Fact]
    public void ShellCachesLieAtTheShellCacheAndSecrets()
    {
        var w = World();
        int expectedMin = w.Map.Pois.Sum(p => p.Kind switch
        {
            PoiKind.ShellCache => PlaneEconomyTuning.CacheMin,
            PoiKind.Secret => PlaneEconomyTuning.SecretMin,
            _ => 0,
        });
        Assert.True(w.Shells.Count >= expectedMin, $"{w.Shells.Count} shells");
        Assert.All(w.Shells, s => Assert.True(w.Map.IsOpen(s.Position)));
    }

    [Fact]
    public void SwimmingOverShellsCollectsThem()
    {
        // A level with a shell cache, so there are shells lying about.
        var w = new PlaneWorld(TestLevels.Get(TestLevels.Find(p => p.Caches > 0 && !p.HasBoss)), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()));
        var shell = w.Shells[0];
        w.Player.Position = w.Player.PrevPosition = shell.Position;
        w.Step(default);
        Assert.True(w.Run.Shells >= 1);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.ShellCollected);
        Assert.Equal(w.Run.Shells, w.Stats.ShellsCollected);
    }

    [Fact]
    public void FreedMobsDropAHeartShellsOrNothing()
    {
        // Free every mob of several levels: each leaves a heart, one or two shells, or nothing, in about the tuned shares.
        int mobs = 0, hearts = 0, shellDrops = 0, nothing = 0;
        foreach (var id in TestLevels.All(p => !p.HasBoss).Take(6))
        {
            var w = new PlaneWorld(TestLevels.Get(id), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()));
            foreach (var mob in w.Mobs.ToList())
            {
                int shellsBefore = w.Shells.Count, heartsBefore = w.Hearts.Count;
                mob.Hp = 0.001f;
                w.Shots.Add(new PlaneShot { Position = mob.Position, Line = mob.Position, Velocity = Vector2.UnitX, Life = 1f, FromPlayer = true, Damage = 1f });
                w.Step(default);
                if (mob.Alive) continue;
                mobs++;
                int newShells = w.Shells.Count - shellsBefore, newHearts = w.Hearts.Count - heartsBefore;
                Assert.True(newHearts == 0 || newShells == 0, "a heart or shells, never both");
                Assert.InRange(newShells, 0, 2);
                if (newHearts > 0) hearts++;
                else if (newShells > 0) shellDrops++;
                else nothing++;
            }
        }
        Assert.True(mobs >= 60, $"{mobs} mobs");
        Assert.InRange(hearts / (float)mobs, 0f, 0.16f);
        Assert.InRange(shellDrops / (float)mobs, 0.15f, 0.45f);
        Assert.True(nothing > shellDrops, $"{nothing} empty, {shellDrops} shells, {hearts} hearts");
    }

    [Fact]
    public void AHeartHealsHerOnlyWhenHurt()
    {
        var w = World();
        w.Mobs.Clear();
        var heart = new PlaneHeart { Position = w.Player.Position };
        w.Hearts.Add(heart);
        w.Step(default);
        Assert.False(heart.Taken);
        Assert.Single(w.Hearts);

        w.Player.Hp = 50f;
        w.Step(default);
        Assert.True(heart.Taken);
        Assert.Empty(w.Hearts);
        Assert.Equal(50f + PlaneEconomyTuning.HeartHeal, w.Player.Hp);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.HeartCollected);
    }

    /// <summary>The first shop level whose shop rolled a pearl.</summary>
    static PlaneWorld WorldWithShopPearl() => TestLevels.All(p => p.Shops > 0 && !p.HasBoss)
        .Select(id => new PlaneWorld(TestLevels.Get(id), new Tuning(), new PlaneRun(Catalog.Value, new Tuning())))
        .First(w => w.Stands.Any(s => s.Kind == StandKind.Pearl));

    [Fact]
    public void EveryShopHasOneHeartAndAtMostOnePearlAt30()
    {
        int shops = 0;
        foreach (var id in TestLevels.All(p => p.Shops > 0 && !p.HasBoss).Take(8))
        {
            var w = new PlaneWorld(TestLevels.Get(id), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()));
            shops++;
            Assert.Single(w.Stands, s => s.Kind == StandKind.Health);
            var pearls = w.Stands.Where(s => s.Kind == StandKind.Pearl).ToList();
            Assert.InRange(pearls.Count, 0, 1);
            Assert.All(pearls, s => Assert.Equal(30, s.Price));
        }
        Assert.True(shops > 0);
    }

    [Fact]
    public void TheShopSellsPearlsAndATopUpForShells()
    {
        var w = WorldWithShopPearl();
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
    public void ShellsCarryDownTheHole()
    {
        var run = new PlaneRun(Catalog.Value, new Tuning()) { Shells = 12 };
        var w = new PlaneWorld(TestLevels.After(TestLevels.WithShop()), new Tuning(), run);
        Assert.Equal(12, w.Run.Shells);
    }
}
