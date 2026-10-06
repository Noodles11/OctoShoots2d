using System;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Placeholder plane combat: Clementine's shots, the shooting-dot mob, its spawns, and the rift's gateway.</summary>
public class PlaneCombatTests
{
    static readonly Lazy<LevelMap> Shared = new(() => TopDownGenerator.Generate(new RunStreams(SeedCode.Parse("KELP7Q2Z")), 1, 1));

    static PlaneWorld World() => new(Shared.Value, new Tuning());

    [Fact]
    public void EveryLevelHasMobSpawnsClearOfTheStart()
    {
        var w = World();
        Assert.InRange(w.Mobs.Count, 1, PlaneCombatTuning.MaxMobs);
        foreach (var m in w.Mobs)
        {
            Assert.True(Vector2.Distance(m.Position, w.Map.Start.Position) >= PlaneCombatTuning.MobStartClearance);
            Assert.True(w.Clear(m.Position, PlaneCombatTuning.MobRadius));
        }
        for (int i = 0; i < w.Mobs.Count; i++)
        for (int j = i + 1; j < w.Mobs.Count; j++)
            Assert.True(Vector2.Distance(w.Mobs[i].Position, w.Mobs[j].Position) >= PlaneCombatTuning.MobSpacing);
    }

    [Fact]
    public void HerShotsDefeatAMobAndItNeverComesBack()
    {
        var w = World();
        var mob = w.Mobs[0];
        // Put her a few metres from it in open water, aiming at it.
        w.Player.Position = w.Player.PrevPosition = mob.Position + Vector2.Normalize(new Vector2(1f, 0.3f)) * 3f;
        if (!w.Clear(w.Player.Position, w.Radius)) w.Player.Position = w.Player.PrevPosition = mob.Position;
        bool defeated = false;
        for (int i = 0; i < 600 && !defeated; i++)
        {
            Vector2 aim = mob.Position - w.Player.Position;
            w.Step(new PlaneInput { Aim = aim.LengthSquared() > 1e-4f ? Vector2.Normalize(aim) : Vector2.UnitX, Fire = true });
            defeated = w.Events.Any(e => e.Type == PlaneEventType.MobDefeated);
        }
        Assert.True(defeated, "three shots should defeat a mob");
        Assert.False(mob.Alive);
        // Long after, it is still down.
        for (int i = 0; i < 60 * 30; i++) w.Step(default);
        Assert.False(mob.Alive);
        Assert.Equal(1, w.Mobs.Count(m => !m.Alive));
    }

    [Fact]
    public void AMobNoticesHerFollowsAndShoots()
    {
        var w = World();
        var mob = w.Mobs[0];
        var start = mob.Position;
        w.Player.Position = w.Player.PrevPosition = FindOpen(w, mob.Position, 10f);
        bool hit = false;
        for (int i = 0; i < 60 * 8; i++)
        {
            w.Step(default);
            hit |= w.Events.Any(e => e.Type == PlaneEventType.PlayerHit);
        }
        Assert.True(mob.Aggro);
        Assert.True(Vector2.Distance(mob.Position, w.Player.Position) < Vector2.Distance(start, w.Player.Position), "it closes in");
        Assert.True(hit || w.Player.Hp < PlaneCombatTuning.PlayerMaxHp, "its shots reach her");
    }

    [Fact]
    public void AMobFarAwayStaysPut()
    {
        var w = World();
        var far = w.Mobs.OrderByDescending(m => Vector2.Distance(m.Position, w.Player.Position)).First();
        var at = far.Position;
        for (int i = 0; i < 120; i++) w.Step(default);
        Assert.False(far.Aggro);
        Assert.True(Vector2.Distance(far.Position, at) < 0.01f);
    }

    [Fact]
    public void TheGatewayInTheRiftLeadsOn()
    {
        var w = World();
        w.Player.Position = w.Player.PrevPosition = w.GatewayPosition;
        foreach (var m in w.Mobs) m.Hp = 0f;
        // Shut while Queen Clam guards it, open once she is freed.
        w.Step(default);
        Assert.DoesNotContain(w.Events, e => e.Type == PlaneEventType.GatewayEntered);
        w.GatewayOpen = true;
        w.Step(default);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.GatewayEntered);
    }

    [Fact]
    public void RunningOutOfHpEndsTheRun()
    {
        var w = World();
        w.Player.Hp = 5f;
        var at = w.Player.Position;
        w.Shots.Add(new PlaneShot { Position = at, Velocity = Vector2.Zero, Life = 1f });
        w.Step(default);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.PlayerDefeated);
        Assert.True(w.Defeated);
        Assert.Equal(0f, w.Player.Hp);
        // The world stands still from then on.
        w.Step(new PlaneInput { Move = Vector2.UnitX });
        Assert.Equal(at, w.Player.Position);
        Assert.Empty(w.Events);
    }

    [Fact]
    public void AnAmbushSpringsOnceAroundHer()
    {
        var w = World();
        var ambush = w.Ambushes.First();
        int before = w.Mobs.Count;
        w.Player.Position = w.Player.PrevPosition = ambush.Center;
        w.Step(default);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.AmbushSprung);
        Assert.True(ambush.Sprung);
        var sprung = w.Mobs.Skip(before).ToList();
        Assert.InRange(sprung.Count, 3, PlaneCombatTuning.AmbushMax);
        Assert.All(sprung, m =>
        {
            Assert.True(m.Aggro, "they attack at once");
            Assert.InRange(Vector2.Distance(m.Position, ambush.Center), 1f, PlaneCombatTuning.AmbushRing + 1f);
        });

        // She swims off and comes back: nothing more spawns, and the ambushers are still there.
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        w.Step(default);
        w.Player.Position = w.Player.PrevPosition = ambush.Center;
        w.Step(default);
        Assert.DoesNotContain(w.Events, e => e.Type == PlaneEventType.AmbushSprung);
        Assert.Equal(before + sprung.Count, w.Mobs.Count);
    }

    [Fact]
    public void BubblesSlowToAStopAndPop()
    {
        var w = World();
        foreach (var m in w.Mobs) m.Hp = 0f;
        // Into the longest open stretch from the start.
        var from = w.Player.Position;
        Vector2 best = Vector2.UnitX;
        float bestRun = 0f;
        for (int i = 0; i < 32; i++)
        {
            var d = new Vector2(MathF.Cos(i * MathF.Tau / 32f), MathF.Sin(i * MathF.Tau / 32f));
            float run = 0f;
            while (run < 20f && w.Map.IsOpen(from + d * run)) run += 0.5f;
            if (run > bestRun) { bestRun = run; best = d; }
        }
        w.Step(new PlaneInput { Aim = best, Fire = true });
        var bubble = w.Shots.Single(s => s.FromPlayer);
        float first = bubble.Velocity.Length();
        float lastSpeed = first;
        bool popped = false;
        for (int i = 0; i < 400 && !popped; i++)
        {
            w.Step(new PlaneInput { Aim = best });
            popped = w.Events.Any(e => e.Type == PlaneEventType.ShotPopped);
            if (!popped)
            {
                float v = bubble.Velocity.Length();
                Assert.True(v <= lastSpeed + 1e-3f, "it only ever slows down");
                lastSpeed = v;
            }
        }
        Assert.True(popped, "it pops");
        Assert.DoesNotContain(w.Shots, s => s == bubble);
        if (bestRun >= PlaneCombatTuning.BubbleRange + 1f)
            Assert.InRange(bubble.Traveled, PlaneCombatTuning.BubbleRange * 0.9f, PlaneCombatTuning.BubbleRange + 0.5f);
    }

    static PlaneShot Bubble(Vector2 at, int volley, float life = 3f) => new()
    {
        Position = at, Line = at, Velocity = Vector2.UnitX, Speed0 = 1f, Range = 11f, Life = life,
        FromPlayer = true, Damage = 10f, Volley = volley,
    };

    [Fact]
    public void TouchingBubblesMergeOneByOneGrowingAndAddingDamage()
    {
        var w = World();
        foreach (var m in w.Mobs) m.Hp = 0f;
        var at = w.Player.Position;
        w.Shots.Add(Bubble(at, 1, life: 2f));
        w.Shots.Add(Bubble(at + new Vector2(0.2f, 0f), 2, life: 2.5f));
        w.Step(default);
        var one = Assert.Single(w.Shots);
        Assert.Equal(20f, one.Damage);
        Assert.Equal(2, one.Bubbles);
        Assert.Equal(PlaneCombatTuning.ShotRadius * 1.5f, one.Radius, 4);
        Assert.True(one.Life > 2.4f, "it keeps the longer life");
        Assert.DoesNotContain(w.Events, e => e.Type == PlaneEventType.ShotPopped);

        w.Shots.Add(Bubble(one.Position, 3));
        w.Step(default);
        one = Assert.Single(w.Shots);
        Assert.Equal(30f, one.Damage);
        Assert.Equal(PlaneCombatTuning.ShotRadius * 2f, one.Radius, 4);
    }

    [Fact]
    public void AFullBubbleStillMergesButStopsGrowing()
    {
        var w = World();
        foreach (var m in w.Mobs) m.Hp = 0f;
        var at = w.Player.Position;
        var full = Bubble(at, 1, life: 1f);
        full.Bubbles = PlaneCombatTuning.BubbleCap - 1;
        full.Damage = 10f * full.Bubbles;
        w.Shots.Add(full);
        var fast = Bubble(at + new Vector2(0.1f, 0f), 2, life: 2.5f);
        fast.Bubbles = 3;
        fast.Damage = 30f;
        fast.Velocity = Vector2.UnitY * 2f;
        fast.Speed0 = 2f;
        w.Shots.Add(fast);
        w.Step(default);
        var one = Assert.Single(w.Shots);
        Assert.Equal(PlaneCombatTuning.BubbleCap, one.Bubbles);
        Assert.Equal(10f * PlaneCombatTuning.BubbleCap, one.Damage, 3);
        float radius = one.Radius;
        Assert.True(one.Life > 2.4f, "it takes the longer life");

        w.Shots.Add(Bubble(one.Position, 3));
        w.Step(default);
        one = Assert.Single(w.Shots);
        Assert.Equal(PlaneCombatTuning.BubbleCap, one.Bubbles);
        Assert.Equal(10f * PlaneCombatTuning.BubbleCap, one.Damage, 3);
        Assert.Equal(radius, one.Radius, 4);
    }

    [Fact]
    public void BubblesOfOneVolleyDoNotMerge()
    {
        var w = World();
        foreach (var m in w.Mobs) m.Hp = 0f;
        var at = w.Player.Position;
        w.Shots.Add(Bubble(at, 1));
        w.Shots.Add(Bubble(at + new Vector2(0f, 0.1f), 1));
        w.Step(default);
        Assert.Equal(2, w.Shots.Count);
    }

    [Fact]
    public void AStoppedBubbleRestsAMomentBeforePopping()
    {
        var w = World();
        foreach (var m in w.Mobs) m.Hp = 0f;
        var bubble = Bubble(w.Player.Position, 1);
        bubble.Traveled = bubble.Range;
        w.Shots.Add(bubble);
        var at = bubble.Position;
        int frames = 0;
        while (w.Shots.Contains(bubble) && frames < 120)
        {
            w.Step(default);
            frames++;
            if (w.Shots.Contains(bubble)) Assert.Equal(at, bubble.Position);
        }
        Assert.InRange(frames * PlaneWorld.Dt, PlaneCombatTuning.BubbleRest - 0.02f, PlaneCombatTuning.BubbleRest + 0.05f);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.ShotPopped);
    }

    [Fact]
    public void ABubblePopsOnAMobAndTheMobFlashesBriefly()
    {
        var w = World();
        var mob = w.Mobs[0];
        w.Shots.Add(new PlaneShot { Position = mob.Position, Line = mob.Position, Velocity = Vector2.UnitX * 5f, Speed0 = 5f, Range = 11f, Life = 3f, FromPlayer = true, Damage = 1f });
        w.Step(default);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.ShotPopped);
        Assert.True(mob.HitFlash > 0f);
        for (int i = 0; i < 12; i++) w.Step(default);
        Assert.True(mob.HitFlash <= 0f, "the flash is over within a fifth of a second");
    }

    [Fact]
    public void CombatIsDeterministic()
    {
        ulong Run()
        {
            var w = World();
            var mob = w.Mobs[0];
            w.Player.Position = w.Player.PrevPosition = FindOpen(w, mob.Position, 9f);
            for (int i = 0; i < 600; i++)
            {
                Vector2 aim = mob.Position - w.Player.Position;
                w.Step(new PlaneInput { Move = new Vector2(MathF.Sin(i * 0.05f), MathF.Cos(i * 0.03f)) * 0.5f, Aim = Vector2.Normalize(aim), Fire = i % 3 == 0 });
            }
            return w.StateHash();
        }
        Assert.Equal(Run(), Run());
    }

    /// <summary>An open spot about `distance` from `from`, in plain sight of it.</summary>
    static Vector2 FindOpen(PlaneWorld w, Vector2 from, float distance)
    {
        for (int i = 0; i < 64; i++)
        {
            float a = i * MathF.Tau / 64f;
            var p = from + new Vector2(MathF.Cos(a), MathF.Sin(a)) * distance;
            if (w.Clear(p, w.Radius + 0.3f) && w.LineOfSight(from, p)) return p;
        }
        return from + Vector2.UnitX * 2f;
    }
}
