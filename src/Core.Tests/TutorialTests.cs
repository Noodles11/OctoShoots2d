using System.Linq;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The tutorial (Depth 0, the Sunlit Shallows): two levels, every kind of place, an open rift, then Depth 1.</summary>
public class TutorialTests
{
    public static TheoryData<string> Seeds => new() { "KELP7Q2Z", "REEF2345", "DEEPSEA9", "CRABSHLL" };

    [Theory]
    [MemberData(nameof(Seeds))]
    public void ItsTwoLevelsHoldEveryKindOfPlaceAndEndInAnOpenRift(string seed)
    {
        var code = SeedCode.Parse(seed);
        var one = LevelPlan.For(code, LevelId.First);
        var two = LevelPlan.For(code, LevelPlan.NextOf(code, LevelId.First));
        Assert.Equal(new LevelId(1, LevelPlan.Tutorial, 2), two.Id);
        Assert.Equal(1, one.Treasures);
        Assert.False(one.HasBoss);
        Assert.True(two.HasBoss && two.Rift);
        Assert.True(one.Treasures + two.Treasures > 0 && one.Secrets + two.Secrets > 0 && one.Curses + two.Curses > 0
                    && one.Shops + two.Shops > 0 && one.Caches + two.Caches > 0 && one.Ambushes + two.Ambushes > 0);
        Assert.Equal(0f, one.Menace);
        Assert.Equal(0f, two.Menace);
        // Out of the rift: the first real depth, as gentle as runs have always started.
        Assert.Equal(LevelId.FirstRun, two.Next);
        Assert.Equal(0f, LevelPlan.MenaceOf(LevelId.FirstRun));
        Assert.Equal(1f, LevelPlan.MenaceOf(new LevelId(1, LevelPlan.Depths, 1)), 4);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void BothLevelsGenerateAndTheRiftHasNoBossAndAnOpenCrack(string seed)
    {
        var code = SeedCode.Parse(seed);
        foreach (var id in new[] { LevelId.First, new LevelId(1, LevelPlan.Tutorial, 2) })
        {
            var map = TopDownGenerator.Generate(new RunStreams(code), id);
            var plan = LevelPlan.For(code, id);
            Assert.Equal(plan.Treasures, map.Pois.Count(p => p.Kind == PoiKind.TreasureCave));
            Assert.Equal(plan.Shops, map.Pois.Count(p => p.Kind == PoiKind.Shop));
            Assert.Equal(plan.Curses, map.Pois.Count(p => p.Kind == PoiKind.CurseDen));
            if (!plan.Rift) continue;
            var w = new PlaneWorld(map, new Tuning(), new PlaneRun(null, new Tuning()), new PlaneOptions { Corruption = true });
            Assert.Null(w.Boss);
            Assert.True(w.ExitOpen);
            Assert.Null(w.ArenaArea);
        }
    }

    [Fact]
    public void LaterLoopsSkipItAndItIsDepthZeroToThePlayer()
    {
        var code = SeedCode.Parse("KELP7Q2Z");
        var last = new LevelId(1, LevelPlan.Depths, LevelPlan.BossLevel(code, 1, LevelPlan.Depths));
        var next = LevelPlan.NextOf(code, last);
        Assert.Equal(new LevelId(2, LevelPlan.Tutorial + 1, 1), next);
        Assert.Equal(last, LevelPlan.PreviousOf(code, next));
        Assert.Null(LevelPlan.PreviousOf(code, LevelId.First));
        Assert.Equal("Depth 0 · Level 1", LevelId.First.ToString());
        Assert.Equal("Depth 1 · Level 1", LevelId.FirstRun.ToString());
        Assert.True(LevelId.First.IsTutorial);
        Assert.False(LevelId.FirstRun.IsTutorial);
    }
}
