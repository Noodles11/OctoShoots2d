using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Bosses;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using Xunit;
using Xunit.Abstractions;

namespace OctoShoots.Core.Tests;

/// <summary>Generated bosses in the fight sim (docs/BOSS-GENERATOR.md §7, §14): every concept can be freed, fairly.</summary>
public class BossFightTests
{
    readonly ITestOutputHelper _out;

    public BossFightTests(ITestOutputHelper output) => _out = output;

    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly Lazy<LevelMap> Arena = new(() =>
    {
        var seed = SeedCode.Parse("KELP7Q2Z");
        return TopDownGenerator.Generate(new RunStreams(seed), new LevelId(1, 2, LevelPlan.BossLevel(seed, 1, 2)));
    });

    static PlaneWorld World(BossSpec spec) =>
        new(Arena.Value, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions { Corruption = false, Pufferlings = false, Boss = spec });

    /// <summary>
    /// A steady attacker: it stands 6 m off the boss on the side of what it can hurt (a live sac, else the pearl), keeps
    /// out of harm's way (healed every step), and fires at it. Returns the seconds to the freeing, or null.
    /// </summary>
    static float? Fight(BossSpec spec, float limit)
    {
        var w = World(spec);
        var boss = w.Boss!;
        w.Player.Position = w.Player.PrevPosition = w.ArenaCenter + new Vector2(0f, 7f);
        for (int i = 0; i < (int)(limit * PlaneWorld.TickRate); i++)
        {
            w.Player.Hp = w.Run.MaxHp;
            Vector2 target = boss.Sacs.Where(s => s.Alive).Select(boss.SacAt).DefaultIfEmpty(boss.PearlAt)
                .OrderBy(t => Vector2.Distance(t, w.Player.Position)).First();
            if (boss.Stage == BossStage.Fight)
            {
                var side = Vector2.Normalize(target - boss.Position + new Vector2(0f, 1e-3f));
                var stand = boss.Position + side * (boss.Radius + 4f);
                if (Vector2.Distance(stand, w.ArenaCenter) > w.ArenaRadius - 1.5f) stand = boss.Position + Vector2.UnitY * (boss.Radius + 4f);
                w.Player.Position = w.Player.PrevPosition = stand;
                w.Player.Velocity = Vector2.Zero;
            }
            var aim = target - w.Player.Position;
            w.Step(new PlaneInput { Aim = aim.LengthSquared() > 1e-6f ? Vector2.Normalize(aim) : Vector2.UnitY, AimReach = aim.Length() + 1f, Fire = true });
            if (boss.Freed) return i * PlaneWorld.Dt;
        }
        return null;
    }

    [Theory]
    [InlineData(BossConcept.TheLid)]
    [InlineData(BossConcept.SnappingGate)]
    [InlineData(BossConcept.TurningStar)]
    [InlineData(BossConcept.SlamAndSigh)]
    [InlineData(BossConcept.PulseBell)]
    [InlineData(BossConcept.TrailingCurtain)]
    [InlineData(BossConcept.TheSpreader)]
    [InlineData(BossConcept.ShyShell)]
    [InlineData(BossConcept.BoilHeart)]
    public void EveryConceptCanBeFreedWithinItsBudget(BossConcept concept)
    {
        foreach (int depth in new[] { 1, 5 })
        {
            var spec = BossGenerator.ForConcept(777UL, 1, depth, concept);
            Assert.Null(BossValidator.Validate(spec));
            float budget = BossValidator.LengthBudget(spec.Menace) * 1.5f;
            float? took = Fight(spec, budget);
            _out.WriteLine($"{spec.Name} ({concept}, Depth {depth}): {(took is { } t ? $"{t:0} s" : "not freed")}");
            Assert.NotNull(took);
        }
    }

    [Fact]
    public void AMovingWindowPearlTravelsAndACoaxOutPearlSurfacesOnlyOnceTheSacsAreBurst()
    {
        var star = World(BossGenerator.ForConcept(5UL, 1, 1, BossConcept.TurningStar));
        star.Player.Position = star.Player.PrevPosition = star.ArenaCenter + new Vector2(0f, 7f);
        for (int i = 0; i < 60 * 8 && star.Boss!.Stage != BossStage.Fight; i++) star.Step(default);
        var from = star.Boss!.PearlAt;
        for (int i = 0; i < 60 * 2; i++)
        {
            star.Player.Hp = star.Run.MaxHp;
            star.Step(default);
        }
        Assert.True(Vector2.Distance(from, star.Boss.PearlAt) > 0.5f, "the pearl travels round the body");
        Assert.True(star.Boss.Open);

        var heart = World(BossGenerator.ForConcept(5UL, 1, 1, BossConcept.BoilHeart));
        heart.Player.Position = heart.Player.PrevPosition = heart.ArenaCenter + new Vector2(0f, 7f);
        for (int i = 0; i < 60 * 8 && heart.Boss!.Stage != BossStage.Fight; i++) heart.Step(default);
        var boss = heart.Boss!;
        Assert.NotEmpty(boss.Sacs);
        Assert.False(boss.Open);
        foreach (var sac in boss.Sacs) sac.Hp = 0.01f;
        foreach (var sac in boss.Sacs)
            heart.Shots.Add(new PlaneShot { Position = boss.SacAt(sac), Line = boss.SacAt(sac), Velocity = Vector2.UnitX * 0.01f, Speed0 = 5f, Range = 11f, Life = 3f, FromPlayer = true, Damage = 10f, Volley = 999 });
        heart.Player.Hp = heart.Run.MaxHp;
        heart.Step(default);
        Assert.All(boss.Sacs, s => Assert.False(s.Alive));
        Assert.True(boss.Open);
    }
}
