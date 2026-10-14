using System;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The reef's background fish (docs/REEF-FISH-PROPOSAL.md): clownfish, blue tangs, chromis schools.</summary>
public class ReefFishTests
{
    static readonly Lazy<LevelMap> Level = new(() => TestLevels.Get(TestLevels.Find(p => p.Id.Depth == 1 && !p.HasBoss)));

    static PlaneWorld World(LevelMap? map = null)
    {
        var w = new PlaneWorld(map ?? Level.Value, new Tuning());
        w.Mobs.Clear();
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

    [Fact]
    public void ADepthOneReefHasAnemonesWithClownfishTangsAndSchoolsAwayFromTheStart()
    {
        var w = World();
        var n = ReefFishTuning.ForDepth(1);
        Assert.InRange(w.Anemones.Count, Math.Max(1, n.AnemoneMin - 1), n.AnemoneMax);
        for (int i = 0; i < w.Anemones.Count; i++)
            Assert.InRange(w.ReefFish.Count(f => f.Kind == ReefFishKind.Clownfish && f.Host == i), 2, 3);
        Assert.InRange(w.ReefFish.Count(f => f.Kind == ReefFishKind.BlueTang), 1, n.TangMax);
        Assert.InRange(w.Schools.Count, 1, n.SchoolMax);
        for (int i = 0; i < w.Schools.Count; i++)
            Assert.InRange(w.ReefFish.Count(f => f.Kind == ReefFishKind.Chromis && f.Host == i), n.SizeMin, n.SizeMax);
        foreach (var f in w.ReefFish)
        {
            Assert.True(w.Map.IsOpen(f.Position));
            Assert.True(Vector2.Distance(f.Position, w.Map.Start.Position) > ReefFishTuning.StartClear - 2f);
        }
    }

    [Fact]
    public void TheyAreSeededSoTheSameLevelSwimsTheSameWay()
    {
        var a = World();
        var b = World();
        Run(a, 300);
        Run(b, 300);
        Assert.Equal(a.ReefFish.Select(f => f.Position), b.ReefFish.Select(f => f.Position));
    }

    [Fact]
    public void TheDeepReefHasNone()
    {
        var deep = TestLevels.All(p => p.Id.Depth >= 4).FirstOrDefault();
        if (deep == default) return;
        var w = World(TestLevels.Get(deep));
        Assert.Empty(w.ReefFish);
        Assert.Empty(w.Anemones);
    }

    [Fact]
    public void TheyKeepToOpenWaterAndStayNearHome()
    {
        var w = World();
        Run(w, 60 * 20);
        foreach (var f in w.ReefFish)
        {
            Assert.True(w.Map.IsOpen(f.Position), $"{f.Kind} at {f.Position}");
            switch (f.Kind)
            {
                case ReefFishKind.Clownfish:
                    Assert.True(Vector2.Distance(f.Position, w.Anemones[f.Host].Position) < ReefFishTuning.ClownRange + 1f, "clownfish by its anemone");
                    break;
                case ReefFishKind.Chromis:
                    Assert.True(Vector2.Distance(f.Position, w.Schools[f.Host].Position) < 4f, "chromis with its school");
                    break;
                default:
                    Assert.True(Vector2.Distance(f.Position, f.Home) < ReefFishTuning.TangHomeRange + 4f, "tang near home");
                    break;
            }
        }
    }

    [Fact]
    public void ASchoolPartsAroundHer()
    {
        var w = World();
        Assert.NotEmpty(w.Schools);
        var school = w.Schools[0];
        // She hangs right in the middle of the school.
        for (int i = 0; i < 90; i++)
        {
            w.Player.Position = w.Player.PrevPosition = school.Position;
            w.Player.Velocity = Vector2.Zero;
            w.Player.Hp = 100f;
            w.Step(default);
        }
        float closest = w.ReefFish.Where(f => f.Kind == ReefFishKind.Chromis && f.Host == 0).Min(f => Vector2.Distance(f.Position, w.Player.Position));
        Assert.True(closest > 0.6f, $"closest chromis {closest} m");
    }

    [Fact]
    public void AClownfishTucksIntoItsAnemoneWhenSheComesClose()
    {
        var w = World();
        Assert.NotEmpty(w.Anemones);
        var host = w.Anemones[0].Position;
        Run(w, 120);
        var spot = host + new Vector2(2f, 0f);
        for (int i = 0; i < 60; i++)
        {
            w.Player.Position = w.Player.PrevPosition = spot;
            w.Player.Velocity = Vector2.Zero;
            w.Player.Hp = 100f;
            w.Step(default);
        }
        foreach (var f in w.ReefFish.Where(f => f.Kind == ReefFishKind.Clownfish && f.Host == 0))
        {
            Assert.True(f.Hiding);
            Assert.True(Vector2.Distance(f.Position, host) < 0.6f, $"{Vector2.Distance(f.Position, host)} m from its anemone");
        }
    }
}
