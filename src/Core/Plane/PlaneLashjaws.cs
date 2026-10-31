using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>
/// The Lashjaw (docs/CORRUPTION.md): a corrupted moray lodged in a hole in the reef wall, from Depth 3 (the Coral
/// Carnival) on. It never moves: it watches a fan of water before it and lashes its tongue of ink at her, or at a reef
/// fish, and reels in what it catches. Her only way out is light on its head: the first time its meter is emptied she is
/// let go and it is stunned; the second time it is freed. Dragged all the way in, she is bitten and thrown clear.
/// </summary>
public static class LashTuning
{
    /// <summary>From internal depth 4 (shown Depth 3): 1–2 there, 2–3 deeper; at most Max.</summary>
    public const int FirstDepth = 4, Max = 3;
    /// <summary>Its head (what her light can strike), and where its jaws are (where she is bitten).</summary>
    public const float Head = 0.55f, Jaws = 1f;
    /// <summary>What it watches: Reach metres before it, within Fan radians either side of its facing, in sight.</summary>
    public const float Reach = 10f, Fan = 70f * MathF.PI / 180f;
    /// <summary>Placement: on a wall face looking over open water, this far from her start and from each other and from safe places.</summary>
    public const float FromStart = 25f, Spacing = 20f, FromPlaces = 10f;
    /// <summary>The lash: jaws gape and its eye flares for Windup (the telegraph), the tongue flies at Speed; a miss retracts over Retract and it waits Pause.</summary>
    public const float Windup = 0.6f, Speed = 25f, Retract = 0.5f, Pause = 2f;
    /// <summary>Caught: it reels her in at Pull m/s; swimming against it slows it (Resist per m/s of her pace against it) to no less than MinPull; a dash against it holds it still.</summary>
    public const float Pull = 3.5f, Resist = 0.45f, MinPull = 1f;
    /// <summary>Reeled all the way in: Bite and a throw clear at BiteKnock; then it chews (Chew seconds) and cannot lash.</summary>
    public const float Bite = 25f, BiteKnock = 10f, Chew = 4f;
    /// <summary>A fish caught is reeled in at FishReel m/s and swallowed; it chews it Chew seconds.</summary>
    public const float FishReel = 8f;
    /// <summary>Its meter: Meter (+20% per Menace), regrowing Regen/s after Delay without a hit. Emptied the first time it is stunned Stun seconds.</summary>
    public const float Meter = 50f, MeterPerMenace = 1.2f, Regen = 15f, Delay = 1.5f, Stun = 3f;
    /// <summary>Freed: it leaves ShellsMin–Max shells, and its light washes Burst metres.</summary>
    public const int ShellsMin = 3, ShellsMax = 4;
    public const float Burst = 3f;
}

public enum LashState { Watch, Windup, Lash, Retract, Hold, Reel, Chew, Stunned }

/// <summary>A Lashjaw: a moray's head at the mouth of its hole, and its tongue.</summary>
public sealed class Lashjaw
{
    /// <summary>Its head (just off the wall) and the way it faces (out of the wall).</summary>
    public Vector2 Position, Facing;
    public LashState State;
    public float Timer, Seed;
    /// <summary>Its tongue's tip, and where the lash is aimed.</summary>
    public Vector2 Tip, Aim;
    /// <summary>The fish on its tongue (reeled in), and the fish it has swallowed (they swim out when it is freed).</summary>
    public PlaneReefFish? Fish;
    public readonly List<PlaneReefFish> Swallowed = new();
    /// <summary>1 until its meter is first emptied, 2 after (emptied again, it is freed).</summary>
    public int Phase = 1;
    public bool Alive = true;
    public CorruptionMeter Corruption = new(LashTuning.Meter, LashTuning.Regen, LashTuning.Delay);
    /// <summary>Its jaws (where she is bitten): at its head.</summary>
    public Vector2 Mouth => Position + Facing * 0.3f;
    /// <summary>Whether its tongue is out (from its mouth to its tip).</summary>
    public bool TongueOut => State is LashState.Lash or LashState.Retract or LashState.Hold or LashState.Reel;
}

public sealed partial class PlaneWorld
{
    public List<Lashjaw> Lashjaws { get; } = new();

    /// <summary>The Lashjaw holding her, if any.</summary>
    public Lashjaw? HeldBy => Lashjaws.FirstOrDefault(l => l.Alive && l.State == LashState.Hold);

    /// <summary>A level's Lashjaws: on wall faces over open water, far from her start, apart, away from places.</summary>
    void PlaceLashjaws()
    {
        if (Map.Depth < LashTuning.FirstDepth) return;
        var cast = CastOf(Gen.TopDown.CastKind.Lashjaw);
        if (cast is { None: true }) return;
        int want = cast is not null ? CastCount(cast, LashTuning.Max)
            : Math.Min(LashTuning.Max, Map.Depth == LashTuning.FirstDepth ? 1 + _blightRng.Int(2) : 2 + _blightRng.Int(2));
        var reach = Gen.TopDown.LevelValidator.Distances(Map, Map.Start.Position);
        for (int tries = 0; tries < 4000 && Lashjaws.Count < want; tries++)
        {
            var at = new Vector2(_blightRng.Range(4f, Gen.TopDown.LevelMap.Size - 4f), _blightRng.Range(4f, Gen.TopDown.LevelMap.Size - 4f));
            if (!Clear(at, 0.5f) || Vector2.Distance(at, Map.Start.Position) < LashTuning.FromStart) continue;
            if (!Anchored(cast, Lashjaws.Count, at)) continue;
            if (Map.HasBoss && Vector2.Distance(at, ArenaCenter) < ArenaRadius + 6f) continue;
            if (Map.Shaft.Contains(at, 4f) || float.IsPositiveInfinity(Gen.TopDown.LevelValidator.DistanceAt(reach, at))) continue;
            if (Map.Pois.Any(p => Vector2.Distance(p.Position, at) < p.Radius + LashTuning.FromPlaces)) continue;
            if (Lashjaws.Any(l => Vector2.Distance(l.Position, at) < LashTuning.Spacing)) continue;
            // Never under an arch or overhang (it would be hidden), nor in a cave.
            if (Map.Canopies.Any(c => c.Contains(at))) continue;
            // A true wall right behind it (rising well over her plane), and open water before it for most of its reach.
            Vector2? facing = null;
            for (int k = 0; k < 16 && facing is null; k++)
            {
                float a = k * MathF.Tau / 16f;
                var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
                // Facing the camera or sideways (the camera looks north, down at the reef): a wall between it and the eye would hide it.
                if (d.Y < -0.3f) continue;
                if (Map.IsOpen(at - d * 1.1f) || Map.HeightAt(at - d * 1.8f) < Gen.TopDown.LevelMap.SwimBand + 1f) continue;
                bool open = true;
                for (float r = 1f; r <= 5f && open; r += 1f) open = Map.IsOpen(at + d * r);
                if (open) facing = d;
            }
            if (facing is not { } f) continue;
            var l = new Lashjaw { Position = at, Facing = f, Seed = _blightRng.Range(0f, 100f), Timer = _blightRng.Range(0f, 1f) };
            l.Tip = l.Mouth;
            l.Corruption.Max = l.Corruption.Value = LashMeter;
            l.Corruption.Regen *= 1f + 0.5f * Menace;
            Lashjaws.Add(l);
        }
    }

    float LashMeter => LashTuning.Meter * (1f + LashTuning.MeterPerMenace * Menace);

    /// <summary>Whether a point lies in a Lashjaw's watched fan, in reach and in sight.</summary>
    bool Watches(Lashjaw l, Vector2 at)
    {
        var to = at - l.Mouth;
        float d = to.Length();
        if (d > LashTuning.Reach || d < 0.3f) return false;
        if (Vector2.Dot(to / d, l.Facing) < MathF.Cos(LashTuning.Fan)) return false;
        return LineOfSight(l.Mouth, at);
    }

    void StepLashjaws()
    {
        var p = Player;
        foreach (var l in Lashjaws)
        {
            if (!l.Alive) continue;
            l.Corruption.Step(Dt);
            l.Timer += Dt;
            switch (l.State)
            {
                case LashState.Watch:
                {
                    if (l.Timer < LashTuning.Pause) break;
                    // Her, if she is in its fan and nobody holds her; else the nearest fish in it.
                    Vector2? aim = null;
                    if (!Defeated && HeldBy is null && Watches(l, p.Position)) aim = p.Position;
                    else
                    {
                        float best = float.MaxValue;
                        foreach (var f in ReefFish)
                        {
                            if (!f.Huntable || !Watches(l, f.Position)) continue;
                            float d = Vector2.Distance(f.Position, l.Mouth);
                            if (d < best)
                            {
                                best = d;
                                aim = f.Position;
                            }
                        }
                    }
                    if (aim is { } a)
                    {
                        l.Aim = a;
                        Enter(l, LashState.Windup);
                        Events.Add(new PlaneEvent(PlaneEventType.LashWindup, l.Position, SafeNormalize(a - l.Mouth), LashTuning.Windup));
                    }
                    break;
                }
                case LashState.Windup:
                    if (l.Timer < LashTuning.Windup) break;
                    // The lash: straight at where it aimed, as far as its reach.
                    l.Tip = l.Mouth;
                    l.Aim = l.Mouth + SafeNormalize(l.Aim - l.Mouth) * LashTuning.Reach;
                    Enter(l, LashState.Lash);
                    Events.Add(new PlaneEvent(PlaneEventType.LashOut, l.Mouth, SafeNormalize(l.Aim - l.Mouth)));
                    break;
                case LashState.Lash:
                {
                    var was = l.Tip;
                    var dir = SafeNormalize(l.Aim - l.Mouth);
                    l.Tip += dir * LashTuning.Speed * Dt;
                    bool spent = Vector2.Distance(l.Tip, l.Mouth) >= LashTuning.Reach || !Map.IsOpen(l.Tip);
                    // It catches her (a dash slips it), or the first fish it meets.
                    if (!Defeated && HeldBy is null && p.DashInvulnerableTimer <= 0f && Gen.TopDown.Geo.SegmentDistance(p.Position, was, l.Tip) < Radius + 0.25f)
                    {
                        l.Tip = p.Position;
                        Enter(l, LashState.Hold);
                        Events.Add(new PlaneEvent(PlaneEventType.LashCatch, p.Position, -dir));
                        break;
                    }
                    var fish = ReefFish.FirstOrDefault(f => f.Huntable && Gen.TopDown.Geo.SegmentDistance(f.Position, was, l.Tip) < 0.45f);
                    if (fish is not null)
                    {
                        l.Fish = fish;
                        fish.Corrupted = true;
                        l.Tip = fish.Position;
                        Enter(l, LashState.Reel);
                        Events.Add(new PlaneEvent(PlaneEventType.LashCatch, fish.Position, -dir, 1f));
                        break;
                    }
                    if (spent) Enter(l, LashState.Retract);
                    break;
                }
                case LashState.Retract:
                    l.Tip = Vector2.Lerp(l.Tip, l.Mouth, MathF.Min(1f, Dt / MathF.Max(LashTuning.Retract - l.Timer + Dt, Dt)));
                    if (l.Timer >= LashTuning.Retract)
                    {
                        l.Tip = l.Mouth;
                        Enter(l, LashState.Watch);
                    }
                    break;
                case LashState.Hold:
                    ReelHer(l);
                    break;
                case LashState.Reel:
                {
                    // The fish on its tongue, reeled in and swallowed.
                    var to = l.Mouth - l.Tip;
                    float d = to.Length();
                    l.Tip += d > 1e-3f ? to / d * MathF.Min(d, LashTuning.FishReel * Dt) : Vector2.Zero;
                    if (l.Fish is { } f) f.Position = l.Tip;
                    if (d < 0.3f)
                    {
                        if (l.Fish is { } eaten) l.Swallowed.Add(eaten);
                        l.Fish = null;
                        l.Tip = l.Mouth;
                        Enter(l, LashState.Chew);
                        Events.Add(new PlaneEvent(PlaneEventType.LashSwallow, l.Mouth, l.Facing));
                    }
                    break;
                }
                case LashState.Chew:
                    if (l.Timer >= LashTuning.Chew) Enter(l, LashState.Watch);
                    break;
                case LashState.Stunned:
                    if (l.Timer >= LashTuning.Stun)
                    {
                        Enter(l, LashState.Watch);
                        l.Timer = LashTuning.Pause * 0.5f;
                    }
                    break;
            }
        }

        // Her light on its head: emptied once, it lets her go and is stunned; emptied again, it is freed.
        foreach (var shot in Shots)
        {
            if (!shot.FromPlayer || shot.Life <= 0f) continue;
            foreach (var l in Lashjaws)
            {
                if (!l.Alive || shot.Struck == l || !Reaches(shot, l.Position, LashTuning.Head)) continue;
                float dealt = Ward(l, l.Position, HitDamage(shot));
                bool emptied = dealt > 0f && l.Corruption.Hit(dealt);
                Events.Add(new PlaneEvent(emptied && l.Phase == 2 ? PlaneEventType.MobDefeated : PlaneEventType.MobHit, l.Position, SafeNormalize(shot.Velocity), dealt));
                if (emptied) BreakLashjaw(l);
                if (shot.Pierce) shot.Struck = l;
                else
                {
                    Pop(shot, l.Position - shot.Position);
                    break;
                }
            }
        }
        Lashjaws.RemoveAll(l => !l.Alive);
    }

    static void Enter(Lashjaw l, LashState state)
    {
        l.State = state;
        l.Timer = 0f;
    }

    /// <summary>
    /// Holding her: it reels her toward its jaws. Swimming against the pull slows it, a dash against it stops it a moment,
    /// but nothing she does breaks the hold; sideways she swims freely. Reeled in, she is bitten and thrown clear.
    /// </summary>
    void ReelHer(Lashjaw l)
    {
        var p = Player;
        var toward = SafeNormalize(l.Mouth - p.Position);
        // This step's own swimming (and currents), split along the pull and across it.
        var moved = (p.Position - p.PrevPosition) / Dt;
        float against = MathF.Max(0f, -Vector2.Dot(moved, toward));
        var across = moved - toward * Vector2.Dot(moved, toward);
        float pull = p.DashTimer > 0f && against > 0.5f ? 0f : MathF.Max(LashTuning.MinPull, LashTuning.Pull - LashTuning.Resist * against);
        p.Position = p.PrevPosition;
        var v = toward * pull + across;
        Move(ref p.Position, ref v, Radius, report: false);
        p.Velocity = v;
        l.Tip = p.Position;
        if (Vector2.Distance(p.Position, l.Mouth) <= LashTuning.Jaws + Radius)
        {
            // Bitten, and thrown clear.
            var out_ = l.Facing;
            if (HurtPlayer(LashTuning.Bite, out_, DamageSource.Lashjaw)) p.Velocity = out_ * LashTuning.BiteKnock;
            l.Tip = l.Mouth;
            Enter(l, LashState.Chew);
            Events.Add(new PlaneEvent(PlaneEventType.LashBite, l.Mouth, out_, LashTuning.Bite));
        }
    }

    /// <summary>Its meter emptied: the first time it lets her (or its fish) go and is stunned, refilling; the second time it is freed.</summary>
    void BreakLashjaw(Lashjaw l)
    {
        if (l.Phase == 1)
        {
            l.Phase = 2;
            if (l.Fish is { } f)
            {
                f.Corrupted = false;
                f.Position = l.Tip;
                l.Fish = null;
            }
            l.Tip = l.Mouth;
            Enter(l, LashState.Stunned);
            l.Corruption.Value = l.Corruption.Max;
            Events.Add(new PlaneEvent(PlaneEventType.LashStun, l.Position, l.Facing, LashTuning.Stun));
            return;
        }
        l.Alive = false;
        Stats.MobsDefeated++;
        Corruption?.Cleanse(l.Position, LashTuning.Burst, 1f);
        int shells = LashTuning.ShellsMin + _drops.Int(LashTuning.ShellsMax - LashTuning.ShellsMin + 1);
        for (int i = 0; i < shells; i++) Shells.Add(new PlaneShell { Position = l.Position + l.Facing, Velocity = Fling(2f, 4f) });
        // The fish it swallowed swim back out, alive, and off away from her.
        var all = l.Fish is { } held ? l.Swallowed.Append(held) : l.Swallowed;
        int k = 0;
        foreach (var fish in all)
        {
            fish.Corrupted = false;
            fish.Freed = true;
            fish.Position = l.Mouth + l.Facing * (0.8f + 0.2f * k++);
            fish.Velocity = l.Facing * 2f;
            fish.Leaving = SafeNormalize(fish.Position - Player.Position);
            if (fish.Leaving == Vector2.Zero) fish.Leaving = l.Facing;
            fish.Left = 0f;
        }
        Events.Add(new PlaneEvent(PlaneEventType.LashFreed, l.Position, l.Facing, LashTuning.Burst));
    }
}
