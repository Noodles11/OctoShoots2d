using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Bosses;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Drippus Prime (docs/CORRUPTION.md, "Drippus Prime"): the brood boss that spends itself.</summary>
public class DrippusTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static LevelMap Arena => TestLevels.Get(new LevelId(1, 2, LevelPlan.BossLevel(TestLevels.Seed, 1, 2)));

    static PlaneWorld World(bool corruption = true) =>
        new(Arena, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions { Corruption = corruption, Pufferlings = false, Boss = BossGenerator.DrippusPrime(1UL, 1, 1) });

    /// <summary>In she goes, and waits (healed every step, out of its bounce's reach) until it has risen and fights.</summary>
    static PlaneBoss Begin(PlaneWorld w, float stand = 9f)
    {
        w.Player.Position = w.Player.PrevPosition = w.ArenaCenter + new Vector2(0f, stand);
        for (int i = 0; i < 60 * 6 && w.Boss!.Stage != BossStage.Fight; i++) Hold(w, stand);
        Assert.Equal(BossStage.Fight, w.Boss!.Stage);
        return w.Boss;
    }

    /// <summary>A step with her held this far south of it, healed.</summary>
    static void Hold(PlaneWorld w, float stand, PlaneInput input = default)
    {
        w.Player.Hp = w.Run.MaxHp;
        var boss = w.Boss!;
        var at = boss.Position + new Vector2(0f, stand);
        if (Vector2.Distance(at, w.ArenaCenter) > w.ArenaRadius - 1.5f) at = w.ArenaCenter + new Vector2(0f, stand);
        w.Player.Position = w.Player.PrevPosition = at;
        w.Player.Velocity = Vector2.Zero;
        w.Step(input);
    }

    [Fact]
    public void EveryDepthMeetsDrippusPrime_AndItRisesOutOfTheArenasInk()
    {
        for (int depth = 1; depth <= 6; depth++) Assert.Equal(BossConcept.TheBrood, BossRoster.For(1UL, 1, depth).Concept);
        Assert.True(BossRoster.For(1UL, 1, 5).Hp > BossRoster.For(1UL, 1, 1).Hp);
        var w = World(corruption: true);
        float before = w.Corruption!.At(w.ArenaCenter);
        var boss = Begin(w);
        Assert.Equal(DrippusTuning.Mass, boss.Mass);
        Assert.Equal(DrippusTuning.Radius, boss.Radius, 3);
        // It drank the arena's ink where it rose.
        Assert.True(w.Corruption.At(w.ArenaCenter) < before - 0.5f);
    }

    [Fact]
    public void EveryBirthCostsItMassAndMeter_ItShrinksAndStopsAtItsFloor()
    {
        var w = World();
        var boss = Begin(w);
        float start = boss.Hp;
        int births = 0;
        for (int i = 0; i < 60 * 240 && boss.Mass > DrippusTuning.Floor; i++)
        {
            int mass = boss.Mass;
            // Its brood is let go each step (freed off-screen), so it may keep brooding.
            foreach (var m in w.Murklings.Where(m => m.Brood)) m.Alive = false;
            Hold(w, 9f);
            births += w.Events.Count(e => e.Type == PlaneEventType.BossBrood);
            Assert.Equal(mass - w.Events.Count(e => e.Type == PlaneEventType.BossBrood), boss.Mass);
            Assert.True(boss.Mass >= DrippusTuning.Floor);
        }
        Assert.Equal(DrippusTuning.Floor, boss.Mass);
        Assert.Equal(DrippusTuning.Mass - DrippusTuning.Floor, births);
        // Each cost BirthCost of its meter; its radius follows √mass.
        Assert.Equal(start - births * DrippusTuning.BirthCost * boss.MaxHp, boss.Hp, 1);
        Assert.Equal(DrippusTuning.Radius * MathF.Sqrt(DrippusTuning.Floor / (float)DrippusTuning.Mass), boss.Radius, 3);
        // Its births counted toward its phases.
        Assert.True(boss.Phase >= 2);
        // At its floor it broods no more.
        for (int i = 0; i < 60 * 20; i++)
        {
            foreach (var m in w.Murklings.Where(m => m.Brood)) m.Alive = false;
            Hold(w, 9f);
            Assert.DoesNotContain(w.Events, e => e.Type == PlaneEventType.BossBrood);
        }
    }

    [Fact]
    public void ItsBroodNeverOutnumbersItsCap()
    {
        var w = World();
        var boss = Begin(w);
        int cap = (int)(DrippusTuning.BroodCap + DrippusTuning.BroodCapPerMenace * boss.Spec.Menace), most = 0;
        for (int i = 0; i < 60 * 60; i++)
        {
            Hold(w, 9f);
            most = Math.Max(most, w.Murklings.Count(m => m.Alive && m.Brood));
        }
        Assert.Equal(cap, most);
    }

    [Fact]
    public void TooCloseItDimplesThenBouncesHerOff_ADashSlipsIt_ThenItRests()
    {
        var w = World();
        var boss = Begin(w);
        float skin = boss.Radius * DrippusTuning.Body;
        // Within its reach: the warning, and only then the spring.
        float near = skin + 0.84f / 2f + 0.6f;
        w.Player.Position = w.Player.PrevPosition = boss.Position + new Vector2(0f, near);
        w.Player.Hp = w.Run.MaxHp;
        w.Step(default);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.BossBounceWarn);
        bool sprang = false;
        float hp = w.Player.Hp;
        for (int i = 0; i < 60 && !sprang; i++)
        {
            w.Player.Position = w.Player.PrevPosition = boss.Position + new Vector2(0f, near);
            w.Player.Velocity = Vector2.Zero;
            w.Step(default);
            sprang = w.Events.Any(e => e.Type == PlaneEventType.BossBounce);
        }
        Assert.True(sprang);
        Assert.True(w.Player.Hp < hp);
        Assert.True(w.Player.TumbleTimer > 0f);
        Assert.True(Vector2.Dot(w.Player.Velocity, Vector2.UnitY) > DrippusTuning.Speed * 0.9f);
        // Tumbling, she has no say in her way.
        var v = w.Player.Velocity;
        w.Step(new PlaneInput { Move = -Vector2.UnitY });
        Assert.True(Vector2.Dot(w.Player.Velocity, Vector2.UnitY) > 0f);
        // It rests before it can bounce again.
        for (int i = 0; i < 20; i++)
        {
            w.Player.Position = w.Player.PrevPosition = boss.Position + new Vector2(0f, near);
            w.Step(default);
            Assert.DoesNotContain(w.Events, e => e.Type == PlaneEventType.BossBounceWarn);
        }
        // A dash out through the warning slips it.
        for (int i = 0; i < 120 && boss.Bounce < 0f; i++)
        {
            w.Player.Position = w.Player.PrevPosition = boss.Position + new Vector2(0f, near);
            w.Player.Velocity = Vector2.Zero;
            w.Step(default);
        }
        Assert.True(boss.Bounce >= 0f);
        hp = w.Player.Hp = w.Run.MaxHp;
        w.Player.DashCooldownTimer = 0f;
        w.Step(new PlaneInput { Move = Vector2.UnitY, Dash = true });
        for (int i = 0; i < 30; i++) w.Step(new PlaneInput { Move = Vector2.UnitY });
        Assert.Equal(hp, w.Player.Hp);
    }

    [Fact]
    public void ItsWholeBodyTakesHerLight_ThroughATearDouble()
    {
        var w = World();
        var boss = Begin(w);
        boss.Crust = 0;
        // A shot on its rim, away from its middle, counts.
        var rim = boss.Position + new Vector2(boss.Radius * DrippusTuning.Body - 0.3f, 0f);
        float hp = boss.Hp;
        w.Shots.Add(new PlaneShot { Position = rim, Line = rim, Velocity = -Vector2.UnitX * 0.01f, Life = 1f, FromPlayer = true, Damage = 10f });
        w.Step(default);
        float first = hp - boss.Hp;
        Assert.True(first > 0f);
        Assert.Single(boss.Tears);
        // The next, through the tear the first opened, counts double (the crust held off as for the first).
        hp = boss.Hp;
        boss.Crust = 0;
        w.Shots.Add(new PlaneShot { Position = rim, Line = rim, Velocity = -Vector2.UnitX * 0.01f, Life = 1f, FromPlayer = true, Damage = 10f });
        w.Step(default);
        Assert.Equal(first * DrippusTuning.Through, hp - boss.Hp, 1);
        // The tear closes.
        for (int i = 0; i < 60 * 2; i++) Hold(w, 12f);
        Assert.Empty(boss.Tears);
    }

    [Fact]
    public void Freed_ItsBroodPopsWithIt_AndTheFishItTookSwimOff()
    {
        var w = World(corruption: true);
        var boss = Begin(w);
        for (int i = 0; i < 60 * 30 && !w.Murklings.Any(m => m.Alive && m.Brood); i++) Hold(w, 9f);
        Assert.Contains(w.Murklings, m => m.Alive && m.Brood);
        int fish = w.ReefFish.Count;
        boss.Crust = 0;
        boss.Hp = 1f;
        w.Shots.Add(new PlaneShot { Position = boss.Position, Line = boss.Position, Velocity = Vector2.UnitX * 0.01f, Life = 1f, FromPlayer = true, Damage = 10f });
        w.Step(default);
        Assert.True(boss.Freed);
        Assert.DoesNotContain(w.Murklings, m => m.Alive && m.Brood);
        Assert.True(w.Corruption!.CleansedOf(w.ArenaArea!.Cells) > 0.99f);
        for (int i = 0; i < 30; i++) w.Step(default);
        Assert.True(boss.Gone);
        var taken = w.ReefFish.Skip(fish).ToList();
        Assert.Equal(DrippusTuning.Chromis + DrippusTuning.Tangs, taken.Count);
        Assert.All(taken, f => Assert.False(f.Huntable));
        // They swim off and are gone once far from her.
        var start = taken.Select(f => f.Position).ToList();
        for (int i = 0; i < 60 * 5; i++) w.Step(default);
        Assert.True(taken.Select((f, k) => Vector2.Distance(f.Position, start[k])).Average() > 3f);
        for (int i = 0; i < 60 * (int)ReefFishTuning.LeaveMax; i++) w.Step(default);
        Assert.All(taken, f => Assert.True(f.Gone));
    }

    [Fact]
    public void ASteadyAttackerFreesItInFairTime()
    {
        foreach (int depth in new[] { 1, 5 })
        {
            var spec = BossRoster.For(777UL, 1, depth);
            var w = new PlaneWorld(Arena, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions { Corruption = true, Pufferlings = false, Boss = spec });
            var boss = Begin(w);
            float? took = null;
            for (int i = 0; i < 60 * 240 && took is null; i++)
            {
                var aim = boss.Position - w.ArenaCenter - new Vector2(0f, 7f);
                Hold(w, 7f, new PlaneInput { Aim = -Vector2.UnitY, AimReach = 8f, Fire = true });
                if (boss.Freed) took = i * PlaneWorld.Dt;
            }
            Assert.NotNull(took);
            Assert.InRange(took!.Value, 20f, BossValidator.LengthBudget(spec.Menace) * 1.5f);
        }
    }
}
