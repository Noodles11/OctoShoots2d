using System;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The curse den (DESIGN-TOPDOWN §4.2): a long dead-end tunnel, safe going in, a reward at its end, and its curse over the way back.</summary>
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
            Assert.True(length >= 16f, $"{id}: only {length} m long");
            // A dead end: from the end chamber, the only way out is back along the tunnel to the mouth.
            var end = cave.Chambers[^1].Center;
            var reach = LevelValidator.Distances(map, map.Start.Position);
            Assert.True(!float.IsPositiveInfinity(LevelValidator.DistanceAt(reach, end)), $"{id}: the end cannot be reached");
            Assert.True(LevelValidator.DistanceAt(reach, end) - LevelValidator.DistanceAt(reach, cave.Mouth) > length * 0.8f, $"{id}: a short cut reaches the end");
        }
    }

    [Fact]
    public void GoingInIsSafeAndTheEndSpringsTheCurseOverTheWayBack()
    {
        var w = World();
        var den = w.Curse!;
        var deep = den.Line[den.Line.Count / 2];
        // On the way in: no dark, no drain.
        Hold(w, deep, 120);
        Assert.True(w.CurseDepth > 5f);
        Assert.Equal(-1f, w.CurseDarkDepth);
        Assert.Equal(100f, w.Player.Hp);
        // The end: its reward out, the curse sprung with a laugh.
        Hold(w, den.End, 1);
        Assert.True(den.Reached && den.Dark);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.CurseLaugh);
        // On the way back: dark, and draining her.
        float hp = w.Player.Hp;
        Hold(w, deep, 120);
        Assert.True(w.CurseDarkDepth > 5f);
        Assert.InRange(hp - w.Player.Hp, CurseTuning.Drain * 2f - 0.2f, CurseTuning.Drain * 2f + 0.2f);
        // Out: the den is done.
        bool cleared = false;
        for (int i = 0; i < 4 && !cleared; i++)
        {
            Hold(w, w.Map.Start.Position, 1);
            cleared = w.Events.Any(e => e.Type == PlaneEventType.CurseCleared);
        }
        Assert.True(cleared);
        Assert.True(den.Cleared && !den.Dark);
        Assert.Equal(-1f, w.CurseDarkDepth);
    }

    [Fact]
    public void TheAdventureHasCurseDensAndTheirCurseDrainsHer()
    {
        var w = new PlaneWorld(Level.Value, new Tuning(), new PlaneRun(null, new Tuning()), new PlaneOptions { Adventure = true });
        w.Mobs.Clear();
        var den = w.Curse!;
        Hold(w, den.End, 1);
        Assert.True(den.Reached && den.Dark);
        float hp = w.Player.Hp;
        Hold(w, den.Line[den.Line.Count / 2], 60);
        Assert.True(w.CurseDarkDepth > 5f);
        Assert.True(w.Player.Hp < hp, "the way back drains her");
    }

    [Fact]
    public void ClearedItNeverDarkensAgainNorGivesMore()
    {
        var w = World();
        var den = w.Curse!;
        den.Reward = CurseReward.Fountain;
        Hold(w, den.End, 60 * 2);
        Hold(w, w.Map.Start.Position, 5);
        Assert.True(den.Cleared);
        int shells = w.Shells.Count + w.Run.Shells;
        // Back in, all the way to the end: safe, dark-free, and the fountain dry.
        Hold(w, den.End, 60 * 4);
        Assert.Equal(-1f, w.CurseDarkDepth);
        Assert.DoesNotContain(w.Events, e => e.Type == PlaneEventType.CurseLaugh);
        Assert.Equal(shells, w.Shells.Count + w.Run.Shells);
        Assert.Equal(100f - CurseTuning.Drain * 2f, w.Player.Hp, 0);
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
    public void TheEndAlwaysHoldsAReward()
    {
        int pearls = 0, fountains = 0;
        foreach (var id in TestLevels.All(p => p.Curses > 0))
        {
            var w = new PlaneWorld(TestLevels.Get(id), new Tuning());
            if (w.Curse!.Reward == CurseReward.Pearl)
            {
                pearls++;
                Assert.Contains(w.Pearls, q => q.Position == w.Curse.End);
            }
            else fountains++;
        }
        Assert.True(pearls + fountains > 0);
        Assert.Equal(new[] { CurseReward.Fountain, CurseReward.Pearl }, Enum.GetValues<CurseReward>());
    }
}
