using System;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Plane-locked movement and heightfield collision (DESIGN-TOPDOWN §2.2, §4.5).</summary>
public class PlaneWorldTests
{
    static readonly Lazy<LevelMap> Shared = new(() => TopDownGenerator.Generate(new RunStreams(SeedCode.Parse("KELP7Q2Z")), new LevelId(1, 2, 3)));

    static PlaneWorld World() => new(Shared.Value, new Tuning());

    /// <summary>Along the first corridor, away from the start: open water for a good while.</summary>
    static Vector2 DownTheCorridor(PlaneWorld w)
    {
        var c = w.Map.Corridors.First(c => c.Kind == CorridorKind.Main);
        return Vector2.Normalize(c.Points[6] - c.Points[0]);
    }

    static void Hold(PlaneWorld w, Vector2 move, int ticks, bool dash = false)
    {
        for (int i = 0; i < ticks; i++) w.Step(new PlaneInput { Move = move, Dash = dash && i == 0 });
    }

    [Fact]
    public void SheCruisesAtSixMetresASecondAfterAJetStart()
    {
        var w = World();
        Vector2 dir = DownTheCorridor(w);
        float peak = 0f;
        for (int i = 0; i < 18; i++)
        {
            w.Step(new PlaneInput { Move = dir });
            peak = MathF.Max(peak, w.Player.Velocity.Length());
        }
        Assert.True(peak > 6f * 1.5f, $"jet start peak {peak:0.0} m/s");
        Hold(w, dir, 42);
        Assert.Equal(6f, w.Player.Velocity.Length(), 1);
    }

    [Fact]
    public void LettingGoStopsHerQuickly()
    {
        var w = World();
        Vector2 dir = DownTheCorridor(w);
        Hold(w, dir, 40);
        Hold(w, Vector2.Zero, 8); // 0.13 s
        Assert.True(w.Player.Velocity.Length() < 0.5f, $"{w.Player.Velocity.Length():0.00} m/s");
    }

    [Fact]
    public void TheInkDashBurstsLeavesInkAndCoolsDown()
    {
        var t = new Tuning();
        var w = World();
        Vector2 dir = DownTheCorridor(w);
        w.Step(new PlaneInput { Move = dir, Dash = true });
        Assert.Equal(6f * t.DashMultiplier, w.Player.Velocity.Length(), 1);
        Assert.True(w.Player.DashInvulnerableTimer > 0.3f);
        Assert.Single(w.Clouds);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.DashStarted);
        // No second dash until the cooldown (0.85 s) runs out.
        Hold(w, dir, 20);
        w.Step(new PlaneInput { Move = dir, Dash = true });
        Assert.Single(w.Clouds);
        Hold(w, dir, 40);
        w.Step(new PlaneInput { Move = dir, Dash = true });
        Assert.Equal(2, w.Clouds.Count);
    }

    [Fact]
    public void RidgesBlockHerAndSheSlidesAlongThem()
    {
        var w = World();
        // Straight at the nearest edge of the square: the rim stops her.
        Vector2 start = w.Map.Start.Position;
        float[] edges = { start.Y, LevelMap.Size - start.X, LevelMap.Size - start.Y, start.X };
        int side = Array.IndexOf(edges, edges.Min());
        Vector2 outward = side switch { 0 => -Vector2.UnitY, 1 => Vector2.UnitX, 2 => Vector2.UnitY, _ => -Vector2.UnitX };
        for (int i = 0; i < 180; i++)
        {
            w.Step(new PlaneInput { Move = outward });
            Assert.True(w.Clear(w.Player.Position, w.Radius), $"inside rock at {w.Player.Position}");
        }
        Assert.True(w.Map.HeightAt(w.Player.Position) <= LevelMap.BlockHeight);

        // Pressing diagonally into the wall, she keeps gliding along it.
        Vector2 along = new(-outward.Y, outward.X);
        Vector2 before = w.Player.Position;
        Hold(w, Vector2.Normalize(outward + along), 30);
        float slid = Vector2.Dot(w.Player.Position - before, along);
        Assert.True(MathF.Abs(slid) > 1f || !w.Clear(before + along * 1.5f, w.Radius), $"slid {slid:0.0} m");
    }

    [Fact]
    public void ShePassesUnderArchesButNeverIntoTheWalls()
    {
        // Swim the shortest route to the rift: every step stays in open water and she gets there.
        var w = World();
        // The rock alone: no pots (she would break them) in a narrow throat.
        w.Vases.Clear();
        // A route with a body-width margin, as a player would swim it (not grazing every lip).
        var path = LevelValidator.ShortestPath(w.Map, w.Map.Start.Position, w.Map.Exit.Position, clearance: 1f);
        Assert.NotEmpty(path);
        int target = 0, progress = 0;
        for (int tick = 0; tick < 60 * 120 && Vector2.Distance(w.Player.Position, w.Map.Exit.Position) > 3f; tick++)
        {
            // Steer a couple of cells ahead of the nearest point of the route.
            for (int i = progress; i < Math.Min(path.Count, progress + 12); i++)
                if (Vector2.Distance(path[i], w.Player.Position) < Vector2.Distance(path[progress], w.Player.Position)) progress = i;
            target = Math.Min(path.Count - 1, progress + 2);
            Vector2 to = path[target] - w.Player.Position;
            w.Step(new PlaneInput { Move = to.LengthSquared() > 1e-4f ? Vector2.Normalize(to) : Vector2.Zero });
            Assert.True(w.Clear(w.Player.Position, w.Radius));
        }
        Assert.True(Vector2.Distance(w.Player.Position, w.Map.Exit.Position) <= 3f, $"she stopped at {w.Player.Position} (waypoint {target}/{path.Count} at {path[target]}, h {w.Map.HeightAt(w.Player.Position):0.00}, start {w.Map.Start.Position}, rift {w.Map.Exit.Position})");
    }

    [Fact]
    public void WeakRockPlugsTheSecretCave()
    {
        // A level with a secret (the tutorial's first always has one).
        var w = new PlaneWorld(TestLevels.Get(TestLevels.Find(p => p.Secrets > 0)), new Tuning());
        var rock = w.WeakRocks.First();
        Assert.False(w.Clear(rock.Center, w.Radius));
    }

    [Fact]
    public void ALongSealedWallOpensOnlyAGateInItsMiddle()
    {
        var map = TestLevels.All(p => p.HasBoss).Select(TestLevels.Get).First(m => m.WeakRocks.Any(r => r.Gate < r.Radius));
        var w = new PlaneWorld(map, new Tuning());
        var seal = w.Seals.First(s => s.Rock.Gate < s.Rock.Radius);
        var rock = seal.Rock;
        for (int k = 0; k < 60 && !seal.Open; k++) w.DebugStrikeSeal();
        Assert.True(seal.Open);
        // Its gate is open; either side of it the wall still stands.
        Assert.True(w.Clear(rock.Center, 0.4f));
        Assert.False(w.Clear(rock.Center + rock.Across * (rock.Radius - 0.6f), 0.4f));
        Assert.False(w.Clear(rock.Center - rock.Across * (rock.Radius - 0.6f), 0.4f));
    }

    [Fact]
    public void ASealsLightDrainsWhenHerHitsStopComing()
    {
        var w = new PlaneWorld(TestLevels.Get(TestLevels.Find(p => p.Secrets > 0)), new Tuning());
        var seal = w.Seals.First();
        for (int k = 0; k < 3; k++) w.DebugStrikeSeal();
        Assert.Equal(3, seal.Hits);
        // Held a moment, then drained away.
        for (int i = 0; i < (int)(SealTuning.Hold * 60f) - 5; i++) w.Step(default);
        Assert.Equal(3, seal.Hits);
        for (int i = 0; i < 120; i++) w.Step(default);
        Assert.Equal(0, seal.Hits);
        Assert.False(seal.Open);
        Assert.False(w.Clear(seal.Rock.Center, w.Radius));
    }

    [Fact]
    public void HerLightOnTheEmblemFillsItAndTheSealCrumbles()
    {
        var w = new PlaneWorld(TestLevels.Get(TestLevels.Find(p => p.Secrets > 0)), new Tuning());
        var seal = w.Seals.First();
        Assert.False(w.Clear(seal.Rock.Center, w.Radius));
        // A shot flying into the emblem from the canyon side: whatever its damage, it counts once.
        var seen = new System.Collections.Generic.List<PlaneEventType>();
        void Shoot(Vector2 aimAt, float damage)
        {
            var from = aimAt + seal.Facing * 1.2f;
            w.Shots.Add(new PlaneShot { Position = from, Line = from, Velocity = -seal.Facing * 12f, Life = 1f, FromPlayer = true, Damage = damage, Radius = 0.2f });
            for (int i = 0; i < 12; i++)
            {
                w.Step(default);
                seen.AddRange(w.Events.Select(e => e.Type));
            }
        }
        // Striking the seal far from its emblem (its back, inside the cave) does nothing.
        var back = seal.Rock.Center - seal.Facing * (seal.Rock.Radius - 0.2f);
        w.Shots.Add(new PlaneShot { Position = back - seal.Facing * 1.2f, Line = back, Velocity = seal.Facing * 12f, Life = 1f, FromPlayer = true, Damage = 10f, Radius = 0.2f });
        for (int i = 0; i < 12; i++) w.Step(default);
        Assert.Equal(0, seal.Hits);
        for (int k = 0; k < SealTuning.Hits - 1; k++) Shoot(seal.Emblem, k == 0 ? 500f : 10f);
        Assert.Equal(SealTuning.Hits - 1, seal.Hits);
        Assert.False(seal.Open);
        Assert.Contains(w.SealedCaves, c => c == seal.Rock.Cave);
        Shoot(seal.Emblem, 10f);
        Assert.True(seal.Open);
        Assert.Contains(PlaneEventType.SealOpened, seen);
        Assert.Equal(SealTuning.Hits, seen.Count(t => t == PlaneEventType.SealLit));
        Assert.True(w.Clear(seal.Rock.Center, w.Radius), "the way in is open");
        Assert.DoesNotContain(w.SealedCaves, c => c == seal.Rock.Cave);
    }

    [Fact]
    public void MovementIsDeterministic()
    {
        ulong Run()
        {
            var w = World();
            for (int i = 0; i < 600; i++)
            {
                float a = i * 0.013f;
                w.Step(new PlaneInput { Move = new Vector2(MathF.Cos(a), MathF.Sin(a)), Dash = i % 97 == 0 });
            }
            return w.StateHash();
        }
        Assert.Equal(Run(), Run());
    }
}
