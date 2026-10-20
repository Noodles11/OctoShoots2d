using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Naves and dens (DESIGN-TOPDOWN §4.9): every side chamber holds something, and clearing a den is rewarded.</summary>
public class DenTests
{
    static readonly PlaneOptions War = new() { Corruption = true, Pufferlings = false };

    static PlaneWorld World(LevelMap map) => new(map, new Tuning(), null, War);

    [Fact]
    public void ADepthHasFiveLevelsTheBossOnTheLast()
    {
        for (int depth = LevelPlan.Tutorial + 1; depth <= LevelPlan.Depths; depth++)
        {
            Assert.Equal(5, LevelPlan.BossLevel(TestLevels.Seed, 1, depth));
            Assert.Equal(5, LevelPlan.DepthTemplates(TestLevels.Seed, 1, depth).Templates.Length);
            Assert.True(LevelPlan.For(TestLevels.Seed, new LevelId(1, depth, 5)).HasBoss);
            Assert.False(LevelPlan.For(TestLevels.Seed, new LevelId(1, depth, 4)).HasBoss);
        }
    }

    [Fact]
    public void EveryNaveHoldsAPlaceOrADen()
    {
        int naves = 0, safe = 0;
        foreach (var id in TestLevels.All(p => !p.HasBoss && p.Id.Depth <= 4).Take(8))
        {
            var map = TestLevels.Get(id);
            var w = World(map);
            // The naves are the chambers off the main route, every one of them.
            Assert.Equal(map.Chambers.Count(c => !c.Main), map.Naves.Count);
            foreach (var nave in map.Naves)
            {
                naves++;
                if (!nave.Den)
                {
                    var poi = map.Pois[nave.Poi];
                    Assert.True(poi.Kind is not (PoiKind.Start or PoiKind.Exit));
                    continue;
                }
                // One in a safe place's clean water holds none.
                if (w.Corruption!.Guard[CorruptionField.Index((int)(nave.At.X / CorruptionTuning.Cell), (int)(nave.At.Y / CorruptionTuning.Cell))] < 0.5f)
                {
                    Assert.DoesNotContain(w.Dens, d => d.At == nave.At);
                    safe++;
                    continue;
                }
                var den = Assert.Single(w.Dens, d => d.At == nave.At);
                Assert.NotEmpty(den.Members);
                Assert.All(den.Members, alive => Assert.True(alive()));
                // Its ground lies thick with ink.
                Assert.True(w.Corruption!.At(nave.At) > 0.5f, $"{id}: den ground {w.Corruption.At(nave.At):0.00}");
            }
            // No place is counted in two naves.
            var held = map.Naves.Where(n => !n.Den).Select(n => n.Poi).ToList();
            Assert.Equal(held.Count, held.Distinct().Count());
        }
        Assert.True(naves > 20, $"only {naves} naves over the levels");
        Assert.True(safe < naves / 4, $"{safe} of {naves} naves in safe water");
    }

    [Fact]
    public void ADensBattleOverCleansesItsNaveAndPaysOut()
    {
        // A level with a den of two or more.
        var w = TestLevels.All(p => !p.HasBoss && p.Id.Level > 1).Select(id => World(TestLevels.Get(id))).First(x => x.Dens.Any(d => d.Members.Count >= 2));
        var den = w.Dens.First(d => d.Members.Count >= 2);
        int shells = w.Shells.Count, orbs = w.Hearts.Count;
        // Half of it freed: not over yet.
        foreach (var m in w.Murklings.Where(m => m.Home == den.At).Take(1)) m.Alive = false;
        for (int i = 0; i < 12; i++) w.Step(default);
        Assert.False(den.Cleared);
        // All of it freed: over, at once.
        foreach (var m in w.Murklings.Where(m => m.Home == den.At)) m.Alive = false;
        foreach (var r in w.Blightroots.Where(r => Vector2.Distance(r.Position, den.At) < den.Radius)) r.Alive = false;
        foreach (var t in w.Thumpsquirts.Where(t => Vector2.Distance(t.Position, den.At) < den.Radius)) t.Alive = false;
        foreach (var x in w.Inkwardens.Where(x => Vector2.Distance(x.Position, den.At) < den.Radius)) x.Alive = false;
        foreach (var m in w.Murklings.Where(m => Vector2.Distance(m.Position, den.At) < den.Radius)) m.Alive = false;
        bool cleared = false;
        for (int i = 0; i < 12 && !cleared; i++)
        {
            w.Step(default);
            cleared = w.Events.Any(e => e.Type == PlaneEventType.DenCleared && e.Position == den.At);
        }
        Assert.True(cleared);
        Assert.True(den.Cleared);
        // Its whole nave is cleansed, and it pays out.
        Assert.True(w.Corruption!.At(den.At) < CorruptionTuning.Clean);
        Assert.True(w.Corruption.At(den.At + new Vector2(den.Radius * 0.8f, 0f)) < CorruptionTuning.Clean || !w.Clear(den.At + new Vector2(den.Radius * 0.8f, 0f), 0.2f));
        Assert.True(w.Shells.Count + w.Run.Shells > shells + 2 || w.Hearts.Count > orbs);
    }
}
