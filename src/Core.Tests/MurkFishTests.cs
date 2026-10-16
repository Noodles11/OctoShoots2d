using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Murklings corrupting the reef's fish (docs/CORRUPTION.md, "Lesser murklings").</summary>
public class MurkFishTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly PlaneOptions War = new() { Corruption = true, Pufferlings = false, Boss = OctoShoots.Core.Bosses.BossGenerator.QueenClam(1UL, 1, 1) };

    /// <summary>A Depth 1 reef (internal 2) at war, with a school in it; the murklings and Blotters cleared away.</summary>
    static PlaneWorld World()
    {
        var plan = TestLevels.Find(p => p.Id.Depth == 2 && !p.HasBoss);
        var w = new PlaneWorld(TestLevels.Get(plan), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), War);
        foreach (var area in w.Areas) area.Spent = true;
        w.Murklings.Clear();
        w.Blotters.Clear();
        w.Bombs.Clear();
        foreach (var vine in w.Vines) vine.Points.RemoveRange(1, vine.Points.Count - 1);
        Assert.NotEmpty(w.Schools);
        return w;
    }

    static void Run(PlaneWorld w, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            w.Player.Hp = 100f;
            w.Step(default);
        }
    }

    /// <summary>Open water this far from a point, in sight of it.</summary>
    static Vector2 Beside(PlaneWorld w, Vector2 at, float distance)
    {
        for (int i = 0; i < 64; i++)
        {
            float a = i * 0.41f;
            var q = at + new Vector2(MathF.Cos(a), MathF.Sin(a)) * distance;
            if (w.Map.IsOpen(q) && w.Map.IsOpen(q + Vector2.UnitX) && w.Map.IsOpen(q - Vector2.UnitX) && w.Map.IsOpen(q + Vector2.UnitY) && w.Map.IsOpen(q - Vector2.UnitY) && w.LineOfSight(at, q)) return q;
        }
        throw new InvalidOperationException("no open water beside it");
    }

    static Murkling Turn(PlaneWorld w, PlaneReefFish f)
    {
        f.Turning = 1e-4f;
        w.Step(default);
        return w.Murklings.Single(m => m.Fish == f);
    }

    [Fact]
    public void TheChanceToTurnGrowsWithMenace()
    {
        Assert.Equal(0.35f, MurkFishTuning.ChanceAt(0f), 3);
        Assert.InRange(MurkFishTuning.ChanceAt(0.8f), 0.8f, 0.86f);
        Assert.Equal(MurkFishTuning.ChanceMax, MurkFishTuning.ChanceAt(3f), 3);
    }

    [Fact]
    public void WhileSheIsAwayAMurklingHuntsTheFishAndTurnsThem()
    {
        var w = World();
        var school = w.Schools[0];
        var m = new Murkling { Position = Beside(w, school.Position, 6f), Bud = 0f };
        w.Murklings.Add(m);
        Assert.True(Vector2.Distance(w.Player.Position, m.Position) > BlightTuning.Sense);
        bool turned = false;
        for (int i = 0; i < 60 * 40 && !turned; i++)
        {
            w.Player.Hp = 100f;
            w.Step(default);
            turned = w.Events.Any(e => e.Type == PlaneEventType.FishTurned);
        }
        Assert.True(turned, "a school near a murkling is turned, a splash at a time");
        var lesser = w.Murklings.First(x => x.Lesser);
        Assert.True(lesser.Fish!.Corrupted);
        // A chromis joins its school's swarm; another fish goes alone.
        Assert.Equal(lesser.Fish.Kind == ReefFishKind.Chromis ? lesser.Fish.Host : -1, lesser.Swarm);
        Assert.True(lesser.Radius < BlightTuning.MurkRadius);
    }

    [Fact]
    public void WithHerNearAMurklingLeavesTheFishAlone()
    {
        var w = World();
        var school = w.Schools[0];
        var m = new Murkling { Position = Beside(w, school.Position, 6f), Bud = 0f };
        w.Murklings.Add(m);
        var her = Beside(w, m.Position, 10f);
        for (int i = 0; i < 60 * 3; i++)
        {
            w.Player.Position = w.Player.PrevPosition = her;
            w.Player.Hp = 100f;
            w.Step(default);
            Assert.Null(m.Prey);
        }
    }

    [Fact]
    public void FishFleeMurklings()
    {
        var w = World();
        var tang = w.ReefFish.FirstOrDefault(f => f.Kind == ReefFishKind.BlueTang);
        if (tang is null) return;
        Run(w, 60);
        var m = new Murkling { Position = Beside(w, tang.Position, 1.5f), Bud = 0f };
        w.Murklings.Add(m);
        Run(w, 60);
        Assert.True(Vector2.Distance(tang.Position, m.Position) > 2.5f);
    }

    [Fact]
    public void ALesserMurklingChasesHerClingsWithoutAHitAndIsFreedBackIntoItsFish()
    {
        var w = World();
        var f = w.ReefFish.First(x => x.Kind == ReefFishKind.Chromis);
        var lesser = Turn(w, f);
        Assert.DoesNotContain(w.ReefFish, x => x != f && x.Corrupted);
        // She comes near: it chases, faster than a murkling.
        var her = Beside(w, lesser.Position, 6f);
        float hp = 0f;
        bool clung = false;
        for (int i = 0; i < 60 * 4 && !clung; i++)
        {
            w.Player.Position = w.Player.PrevPosition = her;
            if (i == 30) Assert.True(lesser.Velocity.Length() > BlightTuning.MurkSpeed);
            hp = w.Player.Hp;
            w.Step(default);
            clung = lesser.Clinging;
        }
        Assert.True(clung);
        // Clinging is no hit: only a drain.
        Assert.True(hp - w.Player.Hp < 0.1f);
        Assert.Equal(0, w.Clingers);
        Assert.Equal(1, w.LesserClingers);

        // A dash throws it off with a shot's worth of light: a chromis's meter is less, so it is freed.
        w.Player.DashTimer = 0.2f;
        typeof(PlaneWorld).GetMethod("DashFreed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(w, null);
        Assert.False(lesser.Alive);
        Assert.False(f.Corrupted);
        Assert.True(f.Freed);
        Assert.False(f.Huntable);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.FishFreed);
    }

    [Fact]
    public void ATurnedSchoolSwarmsTogether()
    {
        var w = World();
        var school = w.Schools[0];
        var fish = w.ReefFish.Where(x => x.Kind == ReefFishKind.Chromis && x.Host == w.Schools.IndexOf(school)).Take(20).ToList();
        foreach (var f in fish) f.Turning = 1e-4f;
        w.Step(default);
        var swarm = w.Murklings.Where(m => m.Lesser).ToList();
        Assert.Equal(fish.Count, swarm.Count);
        Run(w, 60 * 5);
        var centre = swarm.Aggregate(Vector2.Zero, (s, m) => s + m.Position) / swarm.Count;
        Assert.All(swarm, m => Assert.True(Vector2.Distance(m.Position, centre) < 3f, "a swarm keeps together"));
        // She comes near and it closes in on her as one.
        var her = Beside(w, centre, 9f);
        float before = Vector2.Distance(centre, her);
        for (int i = 0; i < 60; i++)
        {
            w.Player.Position = w.Player.PrevPosition = her;
            w.Player.Hp = 100f;
            w.Step(default);
        }
        var after = swarm.Where(m => m.Alive).Aggregate(Vector2.Zero, (s, m) => s + m.Position) / swarm.Count(m => m.Alive);
        Assert.True(Vector2.Distance(after, her) < before - 1.5f);
    }
}
