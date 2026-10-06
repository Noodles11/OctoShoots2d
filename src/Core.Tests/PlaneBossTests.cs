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

/// <summary>Queen Clam and her arena: the seal, the landing wave, the fight, and freeing her.</summary>
public class PlaneBossTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly Lazy<LevelMap> Shared = new(() => TopDownGenerator.Generate(new RunStreams(SeedCode.Parse("KELP7Q2Z")), 1, 1));

    static PlaneWorld World() => new(Shared.Value, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()));

    /// <summary>Puts her inside the arena, 7 m south of the rift.</summary>
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
    public void TheGatewayIsShutUntilSheIsFreed()
    {
        var w = World();
        Assert.False(w.GatewayOpen);
        Assert.Equal(BossStage.Waiting, w.Boss!.Stage);
    }

    [Fact]
    public void EnteringSealsTheArenaAndSheLandsAfterTheBanner()
    {
        var w = World();
        Enter(w);
        w.Step(default);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.ArenaSealed);
        Assert.True(w.ArenaSealed);
        int ticks = StepUntil(w, PlaneEventType.BossLanded);
        float intro = PlaneBossTuning.BannerSeconds + PlaneBossTuning.DissolveSeconds + PlaneBossTuning.DropDelay + PlaneBossTuning.ShadowSeconds;
        Assert.InRange(ticks * PlaneWorld.Dt, intro - 0.05f, intro + 0.05f);
        Assert.Equal(BossStage.Fight, w.Boss!.Stage);
    }

    [Fact]
    public void SheCannotLeaveTheSealedArena()
    {
        var w = World();
        Enter(w);
        w.Step(default);
        for (int i = 0; i < 60 * 4; i++)
        {
            w.Step(new PlaneInput { Move = Vector2.UnitY });
            Assert.True(Vector2.Distance(w.Player.Position, w.ArenaCenter) <= w.ArenaRadius - w.Radius + 0.01f);
        }
    }

    [Fact]
    public void TheLandingSweepsMobsOutAndKeepsThemOut()
    {
        var w = World();
        var mob = w.Mobs[0];
        mob.Position = w.ArenaCenter + new Vector2(-5f, 0f);
        Enter(w);
        w.Step(default);
        Assert.True(StepUntil(w, PlaneEventType.BossLanded) > 0);
        for (int i = 0; i < 60; i++) w.Step(default);
        Assert.True(Vector2.Distance(mob.Position, w.ArenaCenter) >= w.ArenaRadius + PlaneCombatTuning.MobRadius - 0.01f);
        Assert.True(mob.Alive);
        // It hunts her but cannot come in.
        mob.Aggro = true;
        for (int i = 0; i < 60 * 5; i++)
        {
            w.Step(default);
            Assert.True(Vector2.Distance(mob.Position, w.ArenaCenter) >= w.ArenaRadius + PlaneCombatTuning.MobRadius - 0.01f);
        }
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
    public void HerPearlsFlyInARingWithAGapAndABubblePopsOne()
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
        Assert.DoesNotContain(pearl, w.Shots);
        Assert.DoesNotContain(bubble, w.Shots);
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
        for (int i = 0; i < 60 * 2 && boss.Act != ClamAct.Helix; i++) w.Step(default);
        Assert.Equal(ClamAct.Helix, boss.Act);
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
        Assert.False(w.ArenaSealed);
        Assert.True(w.GatewayOpen);
        Assert.Equal(pearlsBefore + 1, w.Pearls.Count);
        Assert.Contains(w.Pearls[^1].ItemId, PlaneRun.ShotPearls);
        Assert.True(Vector2.Distance(w.Pearls[^1].Position, w.GatewayPosition) > PlaneCombatTuning.GatewayRadius + 1f);
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
