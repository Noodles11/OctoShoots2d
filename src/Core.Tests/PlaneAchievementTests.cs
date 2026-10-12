using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Saves;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Achievements: the data, the gating of their pearls, each condition, and the five pearls they unlock.</summary>
public class PlaneAchievementTests
{
    static readonly Lazy<ItemCatalog> Items = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));
    static readonly Lazy<AchievementCatalog> Catalog = new(() =>
        AchievementCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "achievements.json")), Items.Value));

    static readonly Lazy<LevelMap> Level = new(TestLevels.OneTreasure);

    static PlaneWorld World(params string[] pearls)
    {
        var run = new PlaneRun(Items.Value, new Tuning());
        foreach (string id in pearls) run.Add(id);
        var w = new PlaneWorld(Level.Value, new Tuning(), run);
        return w;
    }

    [Fact]
    public void TheFiveAchievementsEachUnlockOnePortedPearl()
    {
        var all = Catalog.Value.All;
        Assert.Equal(new[] { Achievements.BigBubbleEnergy, Achievements.BubbleBath, Achievements.ShuckedInFifteen, Achievements.Untouchable, Achievements.HermitHoarder },
            all.Select(a => a.Id));
        Assert.Equal(all.Count, all.Select(a => a.Pearl).Distinct().Count());
        foreach (var a in all)
        {
            Assert.Contains(a.Pearl, PlaneRun.PortedPearls);
            Assert.Equal(a.Id, Items.Value[a.Pearl].Unlock);
        }
        // The rest of the ported pearls are the basic set: open from the first run.
        var locked = all.Select(a => a.Pearl).ToHashSet();
        Assert.All(PlaneRun.PortedPearls.Where(id => !locked.Contains(id)), id => Assert.Null(Items.Value[id].Unlock));
    }

    [Fact]
    public void ACatalogWithAPearlUnlockedTwiceIsRefused()
    {
        string json = """{ "achievements": [ { "id": "untouchable", "title": "A", "line": "B", "hint": "C", "pearl": "lucky_sea_glass" }, { "id": "bubble_bath", "title": "A", "line": "B", "hint": "C", "pearl": "lucky_sea_glass" } ] }""";
        Assert.Throws<InvalidOperationException>(() => AchievementCatalog.FromJson(json, Items.Value));
    }

    [Fact]
    public void LockedPearlsAreNeverOfferedUntilTheirAchievementIsEarned()
    {
        var locked = Catalog.Value.All.Select(a => a.Pearl).ToHashSet();
        var run = new PlaneRun(Items.Value, new Tuning());
        Assert.All(locked, id => Assert.False(run.CanOffer(id)));
        Assert.True(run.CanOffer("triple_tentacle"));
        // Earned mid-run, it is offered from then on.
        run.Unlocked.Add(Achievements.Untouchable);
        Assert.True(run.CanOffer("lucky_sea_glass"));
        Assert.False(run.CanOffer("mitosis"));

        // Over many levels, no locked pearl turns up in a treasure room or shop.
        foreach (var id in TestLevels.All(p => !p.HasBoss).Take(12))
        {
            var w = new PlaneWorld(TestLevels.Get(id), new Tuning(), new PlaneRun(Items.Value, new Tuning()));
            Assert.DoesNotContain(w.Pearls, p => locked.Contains(p.ItemId));
            Assert.DoesNotContain(w.Stands, s => locked.Contains(s.ItemId));
        }
    }

    [Fact]
    public void BubbleBathIsTenBubblesInOneVolleyAndSeededRunsEarnNothing()
    {
        foreach (bool seeded in new[] { false, true })
        {
            var profile = new Profile();
            var ach = new PlaneAchievements(profile, seeded);
            var w = World("triple_tentacle", "hammerhead", "plankton_swarm", "double_helix");
            w.Mobs.Clear();
            w.Step(new PlaneInput { Aim = Vector2.UnitX, Fire = true });
            Assert.Contains(w.Events, e => e.Type == PlaneEventType.Shot && e.Size >= 10);
            var earned = ach.Observe(w);
            if (seeded)
            {
                Assert.Empty(earned);
                Assert.Empty(profile.Achievements);
            }
            else
            {
                Assert.Equal(new[] { Achievements.BubbleBath }, earned);
                // Once only.
                w.Player.ShotTimer = 0f;
                w.Step(new PlaneInput { Aim = Vector2.UnitX, Fire = true });
                Assert.Empty(ach.Observe(w));
            }
        }
    }

    [Fact]
    public void BigBubbleEnergyIsAFullBubbleFreeingAFoeAtFullHealthInOneHit()
    {
        var profile = new Profile();
        var ach = new PlaneAchievements(profile, false);
        var w = World();
        var mob = w.Mobs[0];
        var shot = new PlaneShot { Position = mob.Position, Line = mob.Position, Velocity = Vector2.UnitX, Life = 1f, FromPlayer = true, Damage = 150f, Bubbles = PlaneCombatTuning.BubbleCap, Volley = 1 };
        w.Shots.Add(shot);
        w.Step(default);
        Assert.False(mob.Alive);
        Assert.Contains(Achievements.BigBubbleEnergy, ach.Observe(w));

        // A smaller bubble, or a foe already hurt, does not count.
        var profile2 = new Profile();
        var ach2 = new PlaneAchievements(profile2, false);
        var w2 = World();
        var hurt = w2.Mobs[0];
        hurt.Hp = PlaneCombatTuning.MobHp - 5f;
        w2.Shots.Add(new PlaneShot { Position = hurt.Position, Line = hurt.Position, Velocity = Vector2.UnitX, Life = 1f, FromPlayer = true, Damage = 150f, Bubbles = PlaneCombatTuning.BubbleCap, Volley = 1 });
        w2.Step(default);
        Assert.Empty(ach2.Observe(w2));
    }

    [Fact]
    public void ShuckedInFifteenIsABossFreedWithinFifteenSecondsOfLanding()
    {
        foreach (float seconds in new[] { 12f, 18f })
        {
            var ach = new PlaneAchievements(new Profile(), false);
            var w = World();
            w.Mobs.Clear();
            w.Step(default);
            w.Events.Add(new PlaneEvent(PlaneEventType.BossLanded, Vector2.Zero, Vector2.Zero));
            ach.Observe(w);
            for (int i = 0; i < (int)(seconds * PlaneWorld.TickRate); i++)
            {
                w.Player.Hp = w.Run.MaxHp;
                w.Step(default);
            }
            w.Events.Add(new PlaneEvent(PlaneEventType.BossDefeated, Vector2.Zero, Vector2.Zero));
            var earned = ach.Observe(w);
            Assert.Equal(seconds <= PlaneAchievements.ShuckSeconds, earned.Contains(Achievements.ShuckedInFifteen));
        }
    }

    [Fact]
    public void UntouchableIsALevelLeftWithoutDamage()
    {
        var ach = new PlaneAchievements(new Profile(), false);
        var w = World();
        Assert.Equal(new[] { Achievements.Untouchable }, ach.LevelCleared(w));

        var ach2 = new PlaneAchievements(new Profile(), false);
        var w2 = World();
        w2.Stats.DamageTaken = 4f;
        Assert.Empty(ach2.LevelCleared(w2));
    }

    [Fact]
    public void HermitHoarderIsAHundredShellsOnOneLevel()
    {
        var ach = new PlaneAchievements(new Profile(), false);
        var w = World();
        w.Stats.ShellsCollected = 99;
        w.Step(default);
        Assert.Empty(ach.Observe(w));
        w.Stats.ShellsCollected = 100;
        Assert.Equal(new[] { Achievements.HermitHoarder }, ach.Observe(w));
    }

    [Fact]
    public void MitosisSplitsABubbleThatPopsOnAFoeIntoTwoHalves()
    {
        var w = World("mitosis");
        var mob = w.Mobs[0];
        var shot = new PlaneShot { Position = mob.Position, Line = mob.Position, Velocity = Vector2.UnitX * 10f, Speed0 = 10f, Range = 10f, Life = 1f, FromPlayer = true, Damage = 10f, Volley = 1, Split = true };
        w.Shots.Add(shot);
        w.Step(default);
        var halves = w.Shots.Where(s => s.FromPlayer && s != shot).ToList();
        Assert.Equal(2, halves.Count);
        Assert.All(halves, h => Assert.Equal(5f, h.Damage, 3));
        Assert.All(halves, h => Assert.False(h.Split));
        // Off either side of its way on.
        float a0 = MathF.Atan2(halves[0].Velocity.Y, halves[0].Velocity.X), a1 = MathF.Atan2(halves[1].Velocity.Y, halves[1].Velocity.X);
        Assert.InRange(MathF.Abs(a0 - a1) * 180f / MathF.PI, 70f, 90f);
    }

    [Fact]
    public void GiantSquidEyeLandsSomeTripleDamageHits()
    {
        var w = World("giant_squid_eye");
        var mob = w.Mobs[0];
        int crits = 0, hits = 0;
        for (int i = 0; i < 120; i++)
        {
            mob.Hp = 10000f;
            w.Shots.Add(new PlaneShot { Position = mob.Position, Line = mob.Position, Velocity = Vector2.UnitX, Life = 1f, FromPlayer = true, Damage = 10f, Volley = 1000 + i });
            w.Step(default);
            foreach (var e in w.Events.Where(e => e.Type == PlaneEventType.MobHit))
            {
                hits++;
                if (e.Size > 25f) crits++;
            }
            w.Player.Hp = w.Run.MaxHp;
        }
        Assert.True(hits >= 100, $"{hits} hits");
        Assert.InRange(crits / (float)hits, 0.03f, 0.2f);
    }

    [Fact]
    public void LuckySeaGlassMakesEveryFreedFoeLeaveTwoMoreShells()
    {
        var w = World("lucky_sea_glass");
        var mob = w.Mobs[0];
        int before = w.Shells.Count;
        w.Shots.Add(new PlaneShot { Position = mob.Position, Line = mob.Position, Velocity = Vector2.UnitX, Life = 1f, FromPlayer = true, Damage = 999f, Volley = 1 });
        w.Step(default);
        Assert.False(mob.Alive);
        Assert.True(w.Shells.Count - before >= 2);
    }

    [Fact]
    public void PiratesDoubloonGivesFifteenShellsAndRicherCaches()
    {
        var w = World();
        w.Mobs.Clear();
        w.Pearls.Add(new PlanePearl { ItemId = "pirates_doubloon", Position = w.Player.Position });
        int shells = w.Run.Shells;
        w.StepRitual();
        Assert.Equal(shells + 15, w.Run.Shells);

        // A level with a shell cache: half as many again in it.
        var map = TestLevels.Get(TestLevels.Find(p => p.Caches > 0 && !p.HasBoss));
        var cache = map.Pois.First(p => p.Kind == PoiKind.ShellCache);
        int Cached(PlaneRun run) => new PlaneWorld(map, new Tuning(), run).Shells.Count(s => Vector2.Distance(s.Position, cache.Position) <= cache.Radius);
        var plain = new PlaneRun(Items.Value, new Tuning());
        var rich = new PlaneRun(Items.Value, new Tuning());
        rich.Add("pirates_doubloon");
        Assert.Equal(MathF.Round(Cached(plain) * PlaneEconomyTuning.RichCaches), Cached(rich), 0);
    }
}
