using System;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The curse den (DESIGN-TOPDOWN §4.2): a long dark dead-end tunnel, a slow curse, a gamble at the end.</summary>
public class CurseDenTests
{
    static readonly Lazy<LevelMap> Level = new(() => TestLevels.Get(TestLevels.Find(p => p.Curses > 0 && !p.HasBoss)));

    static PlaneWorld World()
    {
        var w = new PlaneWorld(Level.Value, new Tuning());
        w.Mobs.Clear();
        return w;
    }

    static void Hold(PlaneWorld w, Vector2 at, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            w.Player.Position = w.Player.PrevPosition = at;
            w.Player.Velocity = Vector2.Zero;
            w.Step(default);
        }
    }

    [Fact]
    public void EveryCurseDenIsALongNarrowDeadEndTunnel()
    {
        var ids = TestLevels.All(p => p.Curses > 0).Take(6).ToList();
        Assert.NotEmpty(ids);
        foreach (var id in ids)
        {
            var map = TestLevels.Get(id);
            var poi = map.Pois.Single(p => p.Kind == PoiKind.CurseDen);
            var cave = map.Caves[poi.Cave];
            Assert.Equal(CaveKind.Tunnel, cave.Kind);
            float length = 0f;
            for (int i = 1; i < cave.Chambers.Count; i++)
            {
                var (a, _) = cave.Chambers[i - 1];
                var (b, _) = cave.Chambers[i];
                length += Vector2.Distance(a, b);
                // Open all along its line, and narrow: rock a couple of metres to either side.
                for (float t = 0f; t <= 1f; t += 0.25f) Assert.True(map.IsOpen(Vector2.Lerp(a, b, t)), $"{id}: the tunnel is blocked");
                var side = Vector2.Normalize(new Vector2(b.Y - a.Y, a.X - b.X));
                var mid = Vector2.Lerp(a, b, 0.5f);
                if (i > 2) Assert.False(map.IsOpen(mid + side * 3.2f) && map.IsOpen(mid - side * 3.2f), $"{id}: the tunnel is not narrow at {mid}");
            }
            Assert.True(length >= 26f, $"{id}: only {length} m long");
            // A dead end: from the end chamber, the only way out is back along the tunnel to the mouth.
            var end = cave.Chambers[^1].Center;
            var reach = LevelValidator.Distances(map, map.Start.Position);
            Assert.True(!float.IsPositiveInfinity(LevelValidator.DistanceAt(reach, end)), $"{id}: the end cannot be reached");
            Assert.True(LevelValidator.DistanceAt(reach, end) - LevelValidator.DistanceAt(reach, cave.Mouth) > length * 0.8f, $"{id}: a short cut reaches the end");
        }
    }

    [Fact]
    public void InsideTheTunnelTheCurseCorruptsHerSlowly()
    {
        var w = World();
        var den = w.Curse!;
        var deep = den.Line[den.Line.Count / 2];
        Hold(w, deep, 120);
        Assert.True(w.CurseDepth > 5f);
        Assert.InRange(100f - w.Player.Hp, CurseTuning.Drain * 2f - 0.2f, CurseTuning.Drain * 2f + 0.2f);
        // Outside it, nothing.
        float hp = w.Player.Hp;
        Hold(w, w.Map.Start.Position, 120);
        Assert.Equal(-1f, w.CurseDepth);
        Assert.Equal(hp, w.Player.Hp);
    }

    [Fact]
    public void NothingAtTheEndButALaugh()
    {
        var w = World();
        w.Curse!.Reward = CurseReward.Nothing;
        bool laughed = false;
        for (int i = 0; i < 30; i++)
        {
            Hold(w, w.Curse.End, 1);
            laughed |= w.Events.Any(e => e.Type == PlaneEventType.CurseLaugh);
        }
        Assert.True(laughed);
        Assert.DoesNotContain(w.Shells, s => Vector2.Distance(s.Position, w.Curse.End) < 4f);
    }

    [Fact]
    public void TheFountainSpoutsAShellASecondWhileSheStays()
    {
        var w = World();
        w.Curse!.Reward = CurseReward.Fountain;
        int before = w.Run.Shells;
        Hold(w, w.Curse.End, 60 * 6);
        int got = w.Run.Shells - before;
        Assert.InRange(got, 4, 6);
        Assert.True(w.Player.Hp < 100f - CurseTuning.Drain * 5f, "she pays for them");
    }

    [Fact]
    public void TheTeleportSendsHerToAPlaceSheHasNotBeen()
    {
        var w = World();
        w.Curse!.Reward = CurseReward.Teleport;
        Hold(w, w.Curse.End, 1);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.CurseTeleport);
        var arrived = w.Player.Position;
        w.Step(default);
        Assert.True(Vector2.Distance(arrived, w.Curse.End) > 10f);
        Assert.True(w.Clear(arrived, w.Radius));
        // Near one of the level's places, never the way down or the start.
        var near = w.Map.Pois.OrderBy(p => Vector2.Distance(p.Position, arrived)).First();
        Assert.DoesNotContain(near.Kind, new[] { PoiKind.Exit, PoiKind.Start, PoiKind.CurseDen });
        Assert.Equal(-1f, w.CurseDepth);
    }

    [Fact]
    public void TheRewardsAreRolledInTheirProportions()
    {
        int nothing = 0, fountain = 0, other = 0;
        foreach (var id in TestLevels.All(p => p.Curses > 0))
        {
            var w = new PlaneWorld(TestLevels.Get(id), new Tuning());
            switch (w.Curse!.Reward)
            {
                case CurseReward.Nothing: nothing++; break;
                case CurseReward.Fountain: fountain++; break;
                default: other++; break;
            }
        }
        Assert.True(fountain + nothing > 0);
        Assert.True(CurseTuning.TeleportChance + CurseTuning.PearlChance + CurseTuning.FountainChance < 1f);
        Assert.Equal(0.3f, 1f - CurseTuning.TeleportChance - CurseTuning.PearlChance - CurseTuning.FountainChance, 3);
    }
}
