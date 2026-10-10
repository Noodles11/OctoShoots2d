using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Plane combat: Clementine's shots, the pufferlings (corrupted and healthy), their spawns, and the dive down the hole.</summary>
public class PlaneCombatTests
{
    static readonly Lazy<LevelMap> Shared = new(() => TopDownGenerator.Generate(new RunStreams(SeedCode.Parse("KELP7Q2Z")), LevelId.First));

    static PlaneWorld World() => new(Shared.Value, new Tuning());

    [Fact]
    public void EveryLevelHasMobSpawnsClearOfTheStart()
    {
        var w = World();
        Assert.InRange(w.Mobs.Count, 1, (int)(PlaneCombatTuning.MaxMobs * (1f + 0.4f * w.Menace)));
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
        Assert.True(defeated, "six shots should defeat a mob");
        Assert.False(mob.Alive);
        // Freed: a healthy pufferling where it was, which swims off.
        var freed = Assert.Single(w.Fish, f => f.Freed);
        Assert.True(Vector2.Distance(freed.Position, mob.Position) < 1.5f);
        // Long after, it is still down.
        for (int i = 0; i < 60 * 30; i++) w.Step(default);
        Assert.False(mob.Alive);
        Assert.Equal(1, w.Mobs.Count(m => !m.Alive));
    }

    /// <summary>A pufferling alone with her, the others freed: she waits this far from it, in plain sight.</summary>
    static (PlaneWorld W, PlaneMob Mob) Alone(float distance)
    {
        var w = World();
        var mob = w.Mobs[0];
        foreach (var m in w.Mobs.Skip(1)) m.Hp = 0f;
        w.Player.Position = w.Player.PrevPosition = FindOpen(w, mob.Position, distance);
        return (w, mob);
    }

    [Fact]
    public void APufferlingFacesHerBlowsUpAndFiresEightNeedlesInARing()
    {
        var (w, mob) = Alone(6f);
        int swellAt = -1, firedAt = -1;
        List<PlaneShot> needles = new();
        for (int i = 0; i < 60 * 6 && firedAt < 0; i++)
        {
            w.Player.Hp = PlaneCombatTuning.PlayerMaxHp;
            int before = w.Shots.Count;
            w.Step(default);
            if (swellAt < 0 && w.Events.Any(e => e.Type == PlaneEventType.PufferSwells)) swellAt = i;
            if (w.Events.Any(e => e.Type == PlaneEventType.NeedlesFired))
            {
                firedAt = i;
                needles = w.Shots.Where(s => s.Needle).ToList();
            }
        }
        Assert.True(mob.Aggro);
        Assert.True(swellAt >= 0 && firedAt > swellAt, "it blows up, then fires");
        // The telegraph: 0.8 s at Depth 1 (never under 0.45 s); fully blown up when it fires.
        Assert.InRange((firedAt - swellAt) * PlaneWorld.Dt, 0.75f, 0.85f);
        Assert.Equal(1f, mob.Inflate, 3);
        Assert.Equal(0f, mob.Spines);
        // Eight, evenly round it, a little faster than her bubbles are thrown.
        Assert.Equal(PufferlingTuning.Needles, needles.Count);
        var angles = needles.Select(n => MathF.Atan2(n.Velocity.Y, n.Velocity.X)).OrderBy(a => a).ToList();
        for (int k = 1; k < angles.Count; k++) Assert.Equal(MathF.Tau / 8f, angles[k] - angles[k - 1], 3);
        Assert.All(needles, n => Assert.True(n.Velocity.Length() > PlaneCombatTuning.ShotSpeed));
    }

    [Fact]
    public void AfterFiringItBacksAwayShrinksAndRegrowsItsSpines()
    {
        var (w, mob) = Alone(6f);
        for (int i = 0; i < 60 * 6 && !w.Events.Any(e => e.Type == PlaneEventType.NeedlesFired); i++)
        {
            w.Player.Hp = PlaneCombatTuning.PlayerMaxHp;
            w.Step(default);
        }
        float near = Vector2.Distance(mob.Position, w.Player.Position);
        for (int i = 0; i < (int)((PufferlingTuning.RoundSeconds + PufferlingTuning.Cooldown - 0.2f) * 60f); i++)
        {
            w.Player.Hp = PlaneCombatTuning.PlayerMaxHp;
            w.Step(default);
        }
        Assert.Equal(PufferState.Cooldown, mob.State);
        Assert.True(Vector2.Distance(mob.Position, w.Player.Position) > near + 1.5f, "it backs away");
        Assert.Equal(0f, mob.Inflate, 3);
        Assert.Equal(1f, mob.Spines, 3);
    }

    [Fact]
    public void EachRingOfNeedlesIsTurnedAtRandom()
    {
        var (w, mob) = Alone(6f);
        var turns = new List<float>();
        for (int i = 0; i < 60 * 20 && turns.Count < 3; i++)
        {
            w.Player.Hp = PlaneCombatTuning.PlayerMaxHp;
            w.Step(default);
            foreach (var e in w.Events.Where(e => e.Type == PlaneEventType.NeedlesFired)) turns.Add(MathF.Atan2(e.Direction.Y, e.Direction.X));
        }
        Assert.Equal(3, turns.Count);
        Assert.True(turns.Distinct().Count() == 3, "never the same ring twice in a row");
    }

    [Fact]
    public void OutOfReachItDriftsCloser()
    {
        var (w, mob) = Alone(PufferlingTuning.NoticeRange - 0.5f);
        float start = Vector2.Distance(mob.Position, w.Player.Position);
        for (int i = 0; i < 60; i++) w.Step(default);
        Assert.True(mob.Aggro);
        Assert.True(Vector2.Distance(mob.Position, w.Player.Position) < start - 0.4f, "it drifts toward her");
    }

    [Fact]
    public void NeedlesPopBubblesAndFlyOn()
    {
        var w = World();
        foreach (var m in w.Mobs) m.Hp = 0f;
        var at = FindOpen(w, w.Player.Position, 4f);
        var needle = new PlaneShot { Position = at, Velocity = Vector2.UnitX * PufferlingTuning.NeedleSpeed, Life = 1f, Needle = true, Radius = PufferlingTuning.NeedleRadius };
        var bubble = new PlaneShot { Position = at + Vector2.UnitX * 0.3f, Line = at, Velocity = Vector2.Zero, Speed0 = 0.1f, Range = 11f, Life = 3f, FromPlayer = true, Damage = 10f };
        w.Shots.Add(needle);
        w.Shots.Add(bubble);
        w.Step(default);
        Assert.True(bubble.Life <= 0f, "the bubble is popped");
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.ShotPopped);
        Assert.True(needle.Life > 0f, "the needle flies on");
    }

    [Fact]
    public void BlownUpItsSpinesHurtOnTouch()
    {
        var (w, mob) = Alone(6f);
        mob.State = PufferState.Round;
        mob.Inflate = 1f;
        w.Player.Position = w.Player.PrevPosition = mob.Position + new Vector2(mob.Radius + 0.2f, 0f);
        float hp = w.Player.Hp;
        w.Step(default);
        Assert.Equal(hp - PufferlingTuning.SpineDamage, w.Player.Hp);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.PlayerHit && e.Source == DamageSource.PufferSpines);
    }

    [Fact]
    public void AFarPufferlingWandersAboutItsHome()
    {
        var w = World();
        var far = w.Mobs.OrderByDescending(m => Vector2.Distance(m.Position, w.Player.Position)).First();
        var home = far.Home;
        float moved = 0f;
        var last = far.Position;
        for (int i = 0; i < 60 * 20; i++)
        {
            w.Step(default);
            moved += Vector2.Distance(last, far.Position);
            last = far.Position;
        }
        Assert.False(far.Aggro);
        Assert.True(moved > 2f, "it swims about");
        Assert.True(Vector2.Distance(far.Position, home) < PufferlingTuning.HomeRange + 2f, "near its home");
    }

    [Fact]
    public void HealthyPufferlingsSwimAboutAndAreNeverTargets()
    {
        var w = World();
        Assert.InRange(w.Fish.Count, PufferlingTuning.HealthyMin, PufferlingTuning.HealthyMax);
        Assert.All(w.Fish, f => Assert.False(f.Freed));
        var fish = w.Fish[0];
        var bubble = new PlaneShot { Position = fish.Position, Line = fish.Position, Velocity = Vector2.UnitX * 5f, Speed0 = 5f, Range = 11f, Life = 3f, FromPlayer = true, Damage = 10f };
        w.Shots.Add(bubble);
        w.Step(default);
        Assert.True(bubble.Life > 0f, "her bubbles pass through them");
        var at = fish.Position;
        for (int i = 0; i < 60 * 10; i++) w.Step(default);
        Assert.Contains(w.Fish, f => Vector2.Distance(f.Position, at) > 0.5f);
    }

    [Fact]
    public void AFreedPufferlingSwimsOffAndIsGoneOutOfSight()
    {
        var (w, mob) = Alone(6f);
        mob.Hp = 1f;
        w.Shots.Add(new PlaneShot { Position = mob.Position, Line = mob.Position, Velocity = Vector2.UnitX, Speed0 = 1f, Range = 11f, Life = 3f, FromPlayer = true, Damage = 10f });
        w.Step(default);
        var freed = Assert.Single(w.Fish, f => f.Freed);
        // She swims far away: once it is off her screen it is gone.
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        for (int i = 0; i < 60 * 2 && w.Fish.Contains(freed); i++) w.Step(default);
        if (Vector2.Distance(freed.Position, w.Player.Position) > PufferlingTuning.FreedGone) Assert.DoesNotContain(freed, w.Fish);
    }

    [Fact]
    public void SheDivesOnlyWhenOverTheHoleAndAsked()
    {
        var w = World();
        foreach (var m in w.Mobs) m.Hp = 0f;
        Assert.True(w.ExitOpen, "a level without a boss has its hole open");
        // Away from the hole, the dive does nothing.
        w.Step(new PlaneInput { Dive = true });
        Assert.DoesNotContain(w.Events, e => e.Type == PlaneEventType.Dived);
        w.Player.Position = w.Player.PrevPosition = w.Map.Exit.Position;
        Assert.True(w.OverShaft);
        w.Step(default);
        Assert.DoesNotContain(w.Events, e => e.Type == PlaneEventType.Dived);
        w.Step(new PlaneInput { Dive = true });
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.Dived);
    }

    [Fact]
    public void NoDivingInTheMiddleOfABattle()
    {
        var w = World();
        foreach (var m in w.Mobs) m.Hp = 0f;
        w.Player.Position = w.Player.PrevPosition = w.Map.Exit.Position;
        // A creature that has noticed her, close by: she cannot leave.
        w.Mobs.Add(new PlaneMob { Position = w.Map.Exit.Position + new Vector2(6f, 0f), Home = w.Map.Exit.Position, Aggro = true, State = PufferState.Face, FireTimer = 99f, Rng = new Rng(1) });
        Assert.True(w.InBattle);
        w.Step(new PlaneInput { Dive = true });
        Assert.DoesNotContain(w.Events, e => e.Type == PlaneEventType.Dived);
        // Once it is freed she may go.
        w.Mobs[^1].Hp = 0f;
        Assert.False(w.InBattle);
        w.Step(new PlaneInput { Dive = true });
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.Dived);
    }

    [Fact]
    public void RunningOutOfHpEndsTheRun()
    {
        var w = World();
        w.Player.Hp = 5f;
        var at = w.Player.Position;
        w.Shots.Add(new PlaneShot { Position = at, Velocity = Vector2.Zero, Life = 1f, Needle = true });
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
        Assert.True(lastSpeed > 0f, "it was still moving when it popped");
        Assert.DoesNotContain(w.Shots, s => s == bubble);
        if (bestRun >= 14f)
            Assert.InRange(bubble.Traveled, 10.5f, 12.5f);
    }

    static PlaneShot Bubble(Vector2 at, int volley, float life = 3f) => new()
    {
        Position = at, Line = at, Velocity = Vector2.UnitX, Speed0 = 1f, Range = 11f, Life = life,
        FromPlayer = true, Damage = 10f, Volley = volley,
    };

    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    /// <summary>A world where she carries Bubble Coral: her bubbles merge.</summary>
    static PlaneWorld MergingWorld()
    {
        var run = new PlaneRun(Catalog.Value, new Tuning());
        run.Add("bubble_coral");
        return new PlaneWorld(Shared.Value, new Tuning(), run);
    }

    [Fact]
    public void TouchingBubblesBounceApartWithoutMerging()
    {
        var w = World();
        foreach (var m in w.Mobs) m.Hp = 0f;
        var at = w.Player.Position;
        var a = Bubble(at, 1);
        var b = Bubble(at + new Vector2(0.2f, 0f), 2);
        b.Velocity = -Vector2.UnitX;
        w.Shots.Add(a);
        w.Shots.Add(b);
        w.Step(default);
        Assert.Equal(2, w.Shots.Count);
        Assert.All(w.Shots, s => Assert.Equal(1, s.Bubbles));
        Assert.All(w.Shots, s => Assert.Equal(10f, s.Damage));
        // Pushed apart, and turned back from each other.
        Assert.True(Vector2.Distance(a.Position, b.Position) >= a.Radius + b.Radius - 1e-3f);
        Assert.True(a.Velocity.X < 0f && b.Velocity.X > 0f, $"{a.Velocity} {b.Velocity}");
    }

    [Fact]
    public void TouchingBubblesMergeOneByOneGrowingAndAddingDamage()
    {
        var w = MergingWorld();
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
        var w = MergingWorld();
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
        var w = MergingWorld();
        foreach (var m in w.Mobs) m.Hp = 0f;
        var at = w.Player.Position;
        w.Shots.Add(Bubble(at, 1));
        w.Shots.Add(Bubble(at + new Vector2(0f, 0.1f), 1));
        w.Step(default);
        Assert.Equal(2, w.Shots.Count);
    }

    [Fact]
    public void ASlowBubbleHoversItsOwnTimeDriftingThenPops()
    {
        var w = World();
        foreach (var m in w.Mobs) m.Hp = 0f;
        var bubble = Bubble(w.Player.Position, 1);
        bubble.Velocity = Vector2.UnitX * 0.5f;
        bubble.Hover = 0.95f;
        w.Shots.Add(bubble);
        int frames = 0;
        float lastSpeed = bubble.Velocity.Length();
        while (w.Shots.Contains(bubble) && frames < 120)
        {
            var was = bubble.Position;
            w.Step(default);
            frames++;
            if (!w.Shots.Contains(bubble)) break;
            // Never a dead stop: it drifts on, slower each step.
            float v = bubble.Velocity.Length();
            Assert.True(v > 0f && v < lastSpeed, $"{v} after {lastSpeed}");
            Assert.NotEqual(was, bubble.Position);
            lastSpeed = v;
        }
        Assert.InRange(frames * PlaneWorld.Dt, 0.95f - 0.02f, 0.95f + 0.05f);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.ShotPopped);
    }

    [Fact]
    public void EachBubbleRollsItsOwnHoverScaledByTheStat()
    {
        var plain = World();
        var run = new PlaneRun(Catalog.Value, new Tuning());
        run.Add("bubble_coral");
        var coral = new PlaneWorld(Shared.Value, new Tuning(), run);
        var hovers = new System.Collections.Generic.List<float>();
        var longer = new System.Collections.Generic.List<float>();
        for (int i = 0; i < 12; i++)
        {
            foreach (var (w, into) in new[] { (plain, hovers), (coral, longer) })
            {
                w.Shots.Clear();
                w.Player.ShotTimer = 0f;
                w.Step(new PlaneInput { Aim = Vector2.UnitX, Fire = true });
                into.Add(w.Shots.Single(s => s.FromPlayer).Hover);
            }
        }
        Assert.All(hovers, h => Assert.InRange(h, PlaneCombatTuning.BubbleHoverMin, PlaneCombatTuning.BubbleHoverMax));
        Assert.True(hovers.Distinct().Count() > 6, "each bubble its own");
        Assert.All(longer, h => Assert.InRange(h, PlaneCombatTuning.BubbleHoverMin * 1.5f, PlaneCombatTuning.BubbleHoverMax * 1.5f));
    }

    [Fact]
    public void BubblesBumpingIntoEachOtherSometimesPop()
    {
        // Many head-on meetings: some pairs survive (bouncing), some lose a bubble; a pop tears open where they met.
        int pops = 0, meetings = 0;
        var w = World();
        foreach (var m in w.Mobs) m.Hp = 0f;
        var at = w.Player.Position;
        for (int i = 0; i < 60; i++)
        {
            w.Shots.Clear();
            var a = Bubble(at, 1000 + 2 * i);
            var b = Bubble(at + new Vector2(0.2f, 0f), 1001 + 2 * i);
            b.Velocity = -Vector2.UnitX;
            w.Shots.Add(a);
            w.Shots.Add(b);
            w.Step(default);
            meetings++;
            var popped = w.Events.Where(e => e.Type == PlaneEventType.ShotPopped).ToList();
            pops += popped.Count;
            foreach (var e in popped) Assert.True(MathF.Abs(e.Direction.X) > 0.9f, "it tears open on the side it was struck");
        }
        float share = pops / (2f * meetings);
        Assert.InRange(share, 0.2f, 0.6f);
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
