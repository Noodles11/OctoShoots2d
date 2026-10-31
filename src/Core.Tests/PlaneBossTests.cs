using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Queen Clam and her arena: the battle's start, its blockade, the landing wave, the fight, and freeing her.</summary>
public class PlaneBossTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    /// <summary>The seed's first boss level (level 4 or 5 of the first depth).</summary>
    static readonly Lazy<LevelMap> Shared = new(() =>
    {
        var seed = SeedCode.Parse("KELP7Q2Z");
        return TopDownGenerator.Generate(new RunStreams(seed), new LevelId(1, 2, LevelPlan.BossLevel(seed, 1, 2)));
    });

    /// <summary>Queen Clam (the reference roll), so these tests pin her fight.</summary>
    static readonly PlaneOptions Queen = new() { Corruption = false, Pufferlings = true, Vases = true, Boss = OctoShoots.Core.Bosses.BossGenerator.QueenClam(1UL, 1, 1) };

    static PlaneWorld World() => new(Shared.Value, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), Queen);

    /// <summary>Puts her inside the arena, 7 m south of the Crack's middle.</summary>
    static void Enter(PlaneWorld w)
    {
        var at = w.ArenaCenter + new Vector2(0f, 7f);
        w.Player.Position = w.Player.PrevPosition = at;
    }

    static int StepUntil(PlaneWorld w, PlaneEventType type, int max = 60 * 10, PlaneInput input = default)
    {
        for (int i = 1; i <= max; i++)
        {
            w.Step(input);
            if (w.Events.Any(e => e.Type == type)) return i;
        }
        return -1;
    }

    [Fact]
    public void TheCrackIsShutUntilSheIsFreed()
    {
        var w = World();
        Assert.False(w.ExitOpen);
        Assert.Equal(BossStage.Waiting, w.Boss!.Stage);
    }

    [Fact]
    public void EnteringBeginsTheBattleAndSheLandsAfterTheBanner()
    {
        var w = World();
        Enter(w);
        w.Step(default);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.BattleBegan);
        Assert.True(w.BossBattle);
        int ticks = StepUntil(w, PlaneEventType.BossLanded);
        float intro = PlaneBossTuning.DropDelay + PlaneBossTuning.ShadowSeconds;
        Assert.InRange(ticks * PlaneWorld.Dt, intro - 0.05f, intro + 0.05f);
        Assert.Equal(BossStage.Fight, w.Boss!.Stage);
    }

    [Fact]
    public void WithoutABlockadeSheMayLeaveTheArenaMidFight()
    {
        var spec = OctoShoots.Core.Bosses.BossGenerator.QueenClam(1UL, 1, 1);
        spec.Blockade = false;
        var w = new PlaneWorld(Shared.Value, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions { Corruption = false, Boss = spec });
        // A way out: open water just inside the rim and well beyond it.
        var dir = Enumerable.Range(0, 72).Select(i => new Vector2(MathF.Cos(i * MathF.Tau / 72f), MathF.Sin(i * MathF.Tau / 72f)))
            .First(d => Enumerable.Range(0, 12).All(k => w.Clear(w.ArenaCenter + d * (w.ArenaRadius - 2f + k * 0.5f), w.Radius + 0.2f)));
        w.Player.Position = w.Player.PrevPosition = w.ArenaCenter + dir * (w.ArenaRadius - 2f);
        Assert.True(StepUntil(w, PlaneEventType.BossLanded) > 0);
        w.Player.Position = w.Player.PrevPosition = w.ArenaCenter + dir * (w.ArenaRadius - 2f);
        for (int i = 0; i < 60; i++) w.Step(new PlaneInput { Move = dir });
        Assert.True(Vector2.Distance(w.Player.Position, w.ArenaCenter) > w.ArenaRadius + 1f);
        // The fight carries on while she is out; nothing resets.
        Assert.True(w.BossBattle);
        Assert.Equal(BossStage.Fight, w.Boss!.Stage);
        Assert.DoesNotContain(w.Events, e => e.Type == PlaneEventType.SealClosed);
    }

    [Fact]
    public void ABarredArenasWayInRisesBehindHerAndSlidesDownOnceSheIsFreed()
    {
        var w = World();
        foreach (var m in w.Mobs) m.Hp = 0f;
        var seal = w.Seals.First(s => s.Kind == SealKind.Boss && w.Map.IsOpen(s.Rock.Center));
        for (int k = 0; k < 60 && !seal.Open; k++) w.DebugStrikeSeal(seal);
        Assert.True(seal.Open);
        Assert.True(w.Clear(seal.Rock.Center, 0.4f));
        Enter(w);
        w.Step(default);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.SealClosed && e.Position == seal.Rock.Center);
        Assert.True(seal.Barred);
        Assert.False(w.Clear(seal.Rock.Center, 0.4f));
        // Her light cannot open it while the fight runs.
        for (int k = 0; k < SealTuning.BossHits + 2; k++) w.DebugStrikeSeal(seal);
        Assert.False(seal.Open);
        Assert.Equal(0, seal.Hits);
        // Freed, the boss lets it slide back down.
        Assert.True(StepUntil(w, PlaneEventType.BossLanded) > 0);
        Assert.True(StepUntil(w, PlaneEventType.BossVolley) > 0);
        w.Boss!.Phase = 2;
        w.Boss.Hp = 5f;
        w.Shots.Add(BubbleAt(w.Boss.Position));
        Assert.True(StepUntil(w, PlaneEventType.BossFreed, 60) > 0);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.SealOpened && e.Position == seal.Rock.Center);
        Assert.True(seal.Open);
        Assert.False(seal.Barred);
        Assert.True(w.Clear(seal.Rock.Center, 0.4f));
    }

    [Fact]
    public void EveryDepthMeetsDrippusPrimeTougherTheDeeper()
    {
        var bosses = Enumerable.Range(1, 6).Select(d => OctoShoots.Core.Bosses.BossRoster.For(7UL, 1, d)).ToList();
        Assert.All(bosses, b => Assert.Equal("Drippus Prime", b.Name));
        Assert.All(bosses, b => Assert.True(b.Blockade));
        Assert.Equal(DrippusTuning.Meter, bosses[0].Hp);
        for (int i = 1; i < bosses.Count; i++) Assert.True(bosses[i].Hp > bosses[i - 1].Hp);
    }

    static PlaneShot BubbleAt(Vector2 at) => new()
    {
        Position = at, Line = at, Velocity = Vector2.UnitX * 0.01f, Speed0 = 5f, Range = 11f, Life = 3f, FromPlayer = true, Damage = 10f, Volley = 999,
    };

    static PlaneWorld Landed()
    {
        var w = World();
        foreach (var m in w.Mobs) m.Hp = 0f;
        Enter(w);
        w.Step(default);
        Assert.True(StepUntil(w, PlaneEventType.BossLanded) > 0);
        return w;
    }

    [Fact]
    public void SheIsHurtOnlyWhileOpen()
    {
        var w = Landed();
        var boss = w.Boss!;
        Assert.False(boss.Open);
        w.Shots.Add(BubbleAt(boss.Position));
        w.Step(default);
        Assert.Equal(PlaneBossTuning.Hp, boss.Hp);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.ShotPopped);

        Assert.True(StepUntil(w, PlaneEventType.BossVolley) > 0);
        Assert.True(boss.Open);
        w.Shots.Add(BubbleAt(boss.Position));
        w.Step(default);
        Assert.Equal(PlaneBossTuning.Hp - 10f, boss.Hp);
    }

    [Fact]
    public void HerPearlsFlyInARingWithAGapAndHerLightCannotClearThem()
    {
        var w = Landed();
        Assert.True(StepUntil(w, PlaneEventType.BossVolley) > 0);
        var pearls = w.Shots.Where(s => s.BossPearl).ToList();
        Assert.Equal(PlaneBossTuning.RingCount - PlaneBossTuning.RingGap, pearls.Count);
        var pearl = pearls[0];
        // Out in the water, clear of her shell.
        pearl.Position = w.Boss!.Position + Vector2.Normalize(pearl.Velocity) * 7f;
        var bubble = BubbleAt(pearl.Position + pearl.Velocity * PlaneWorld.Dt);
        w.Shots.Add(bubble);
        w.Step(default);
        // Corruption: her light passes through it, and it flies on.
        Assert.Contains(pearl, w.Shots);
        Assert.Contains(bubble, w.Shots);
    }

    [Fact]
    public void AtHalfHpSheStaggersIntoHerSecondPhase()
    {
        var w = Landed();
        var boss = w.Boss!;
        Assert.True(StepUntil(w, PlaneEventType.BossVolley) > 0);
        boss.Hp = PlaneBossTuning.Hp * 0.5f + 5f;
        w.Shots.Add(BubbleAt(boss.Position));
        w.Step(default);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.BossStagger);
        Assert.Equal(2, boss.Phase);
        Assert.False(boss.Open);
        // The next opening brings the royal pearl and the helix.
        Assert.True(StepUntil(w, PlaneEventType.BossVolley) > 0);
        Assert.Contains(w.Shots, s => s.Royal);
        for (int i = 0; i < 60 * 2 && boss.Act != BossAct.Helix; i++) w.Step(default);
        Assert.Equal(BossAct.Helix, boss.Act);
    }

    [Fact]
    public void FreeingHerDropsOnePearlAndOpensTheArenaAndTheGateway()
    {
        var w = Landed();
        var boss = w.Boss!;
        Assert.True(StepUntil(w, PlaneEventType.BossVolley) > 0);
        boss.Phase = 2;
        boss.Hp = 5f;
        int pearlsBefore = w.Pearls.Count;
        w.Shots.Add(BubbleAt(boss.Position));
        w.Step(default);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.BossDefeated);
        Assert.DoesNotContain(w.Shots, s => s.BossPearl);
        Assert.True(StepUntil(w, PlaneEventType.BossFreed, 60) > 0);
        Assert.False(w.BossBattle);
        Assert.True(w.ExitOpen);
        Assert.Equal(pearlsBefore + 1, w.Pearls.Count);
        Assert.Contains(w.Pearls[^1].ItemId, PlaneRun.PortedPearls);
        // Her own pearl, cleansed, is the reward: left exactly where it sat in her, as she leaps away.
        Assert.Equal(w.ArenaCenter, w.Pearls[^1].Position);
        Assert.True(w.Pearls[^1].Placed);
        Assert.Equal(1, w.Stats.BossesFreed);
    }

    [Fact]
    public void ThePhaseTwoSnapHurtsHerUpClose()
    {
        var w = Landed();
        var boss = w.Boss!;
        boss.Phase = 2;
        w.Player.Position = w.Player.PrevPosition = boss.Position + new Vector2(0f, PlaneBossTuning.BodyRadius + 1.5f);
        float hp = w.Player.Hp;
        Assert.True(StepUntil(w, PlaneEventType.BossSnapWarning) > 0);
        int ticks = StepUntil(w, PlaneEventType.BossSnap);
        Assert.InRange(ticks * PlaneWorld.Dt, PlaneBossTuning.SnapWarn - 0.05f, PlaneBossTuning.SnapWarn + 0.05f);
        Assert.True(w.Player.Hp < hp);
    }
}
