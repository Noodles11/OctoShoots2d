using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Tide Pulse: the active pearl every run starts with, its three waves, and one active at a time.</summary>
public class PlanePushWaveTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly Lazy<LevelMap> Room = new(TestLevels.OneTreasure);

    static PlaneWorld World()
    {
        var run = new PlaneRun(Catalog.Value, new Tuning());
        run.Add(PlaneRun.StartingPearl);
        var w = new PlaneWorld(Room.Value, new Tuning(), run);
        w.Mobs.Clear();
        w.Pearls.Clear();
        w.Shells.Clear();
        return w;
    }

    /// <summary>A spot with open water all round it, out past the pearl's radius and a margin.</summary>
    static Vector2 OpenSpot(PlaneWorld w, float reach)
    {
        for (int y = 6; y < LevelMap.Size - 6; y += 2)
            for (int x = 6; x < LevelMap.Size - 6; x += 2)
            {
                var c = new Vector2(x, y);
                if (Enumerable.Range(0, 24).All(k => w.Clear(c + new Vector2(MathF.Cos(k * MathF.Tau / 24f), MathF.Sin(k * MathF.Tau / 24f)) * reach, 0.6f))
                    && w.Clear(c, reach * 0.5f))
                    return c;
            }
        throw new InvalidOperationException("no open water");
    }

    [Fact]
    public void ItsThreeWavesLeaveTheWaterRoundHerClear()
    {
        var w = World();
        var spec = w.Run.Active!.Active!;
        Assert.Equal(ActiveAction.PushWave, spec.Action);
        var her = OpenSpot(w, spec.Radius + 2f);
        w.Player.Position = w.Player.PrevPosition = her;
        // Pufferlings and shells all round her, near and far inside its radius.
        for (int i = 0; i < 8; i++)
        {
            var dir = new Vector2(MathF.Cos(i * MathF.Tau / 8f), MathF.Sin(i * MathF.Tau / 8f));
            float d = 0.9f + (i % 3) * 0.8f;
            w.Mobs.Add(new PlaneMob { Position = her + dir * d, Home = her + dir * d, Rng = new Rng((ulong)i + 1), FireTimer = 99f });
            w.Shells.Add(new PlaneShell { Position = her + Vector2.Transform(dir, Matrix3x2.CreateRotation(0.4f)) * (d + 0.3f) });
        }
        var waves = new System.Collections.Generic.List<int>();
        w.Step(new PlaneInput { UseActive = true });
        for (int i = 0; i < PlaneWorld.TickRate && w.Player.PushTime >= 0f; i++)
        {
            w.Step(new PlaneInput());
            waves.AddRange(w.Events.Where(e => e.Type == PlaneEventType.PushWave).Select(e => (int)e.Size));
        }
        Assert.Equal(new[] { 0, 1, 2 }, waves);
        Assert.True(w.Player.PushTime < 0f, "done within its second");
        foreach (var m in w.Mobs) Assert.True(Vector2.Distance(m.Position, w.Player.Position) >= spec.Radius, $"a pufferling {Vector2.Distance(m.Position, w.Player.Position):0.00} m off");
        // (Shells the magnet already drew in are hers; the rest were pushed out past it.)
        foreach (var s in w.Shells.Where(s => !s.Taken)) Assert.True(Vector2.Distance(s.Position, w.Player.Position) >= spec.Radius - 0.05f);
        Assert.True(w.Run.ActiveCharge < 0.1f, "it recharges over a minute");
    }

    [Fact]
    public void EachWaveReachesFurtherAndHitsHarderThanTheLast()
    {
        for (int k = 1; k < PushTuning.At.Length; k++)
        {
            Assert.True(PushTuning.Reach[k] > PushTuning.Reach[k - 1]);
            Assert.True(PushTuning.Kick[k] > PushTuning.Kick[k - 1]);
            Assert.True(PushTuning.At[k] > PushTuning.At[k - 1]);
        }
        Assert.Equal(1f, PushTuning.Reach[^1]);
        Assert.True(PushTuning.At[^1] + PushTuning.FrontShare <= 1f, "the last wave is done within the duration");
    }

    [Fact]
    public void ANewActivePearlTakesTheOldOnesPlace()
    {
        var run = new PlaneRun(Catalog.Value, new Tuning());
        run.Add(PlaneRun.StartingPearl);
        run.Add("shark_tooth");
        run.Add("bubble_shield");
        Assert.Equal("bubble_shield", run.Active!.Id);
        Assert.DoesNotContain(PlaneRun.StartingPearl, run.Items);
        Assert.Contains("shark_tooth", run.Items);
        run.Add("whale_song");
        Assert.Equal(new[] { "shark_tooth", "whale_song" }, run.Items);
    }

    [Fact]
    public void TheStartingPearlIsNeverOffered()
    {
        var run = new PlaneRun(Catalog.Value, new Tuning());
        Assert.False(run.CanOffer(PlaneRun.StartingPearl));
        Assert.False(run.CanOffer("sea_chart_nautilus"));
        Assert.Contains(PlaneRun.StartingPearl, PlaneRun.PortedPearls);
    }
}
