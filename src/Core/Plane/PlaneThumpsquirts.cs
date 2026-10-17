using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>
/// The Thumpsquirt (docs/CORRUPTION.md): a corrupted sea squirt, a tube of ink standing on the seabed, from Depth 2
/// (the Sunken Galleon) on. It goes round a cycle: three great leaps at her (each landing a knockback ring of ink, like
/// a Blightroot's pulse), a rest, a roll on its rim chasing her and trailing ink, a rest, and leaps again. In the air it
/// cannot be harmed. Freed, it is a pale sea squirt again.
/// </summary>
public static class ThumpTuning
{
    /// <summary>From internal depth 3 (shown Depth 2): one there, 1–2 at the next, then 2; never more than Max.</summary>
    public const int FirstDepth = 3, Max = 3;
    /// <summary>Its body for hits (and her touching it), and for moving; it wakes with her within Wake, sleeps past Sleep.</summary>
    public const float Radius = 0.5f, Body = 0.45f, Wake = 16f, Sleep = 26f;
    /// <summary>Waking with the level: this far from her start, and apart.</summary>
    public const float FromStart = 20f, Spacing = 18f;
    /// <summary>Its corruption meter: 90 (+20% per Menace); it regrows at Regen once Delay passes without a hit.</summary>
    public const float Meter = 90f, MeterPerMenace = 0.2f, Regen = 20f, Delay = 1f;

    /// <summary>
    /// A leap: it contracts for Windup (the telegraph), locks onto where she is then (at most Range away), flies
    /// Flight seconds, and lands squashed for Squash. Leaps a set.
    /// </summary>
    public const float Windup = 0.6f, Flight = 0.9f, Squash = 0.35f, Range = 12f;
    public const int Leaps = 3;
    /// <summary>Landing on her: Crush damage and a hard shove; within Crushed of where it lands.</summary>
    public const float Crush = 14f, Crushed = 1.2f, CrushKnock = 12f;
    /// <summary>
    /// The landing's ring: out to Reach at RingSpeed; passing over her it corrupts her for RingDamage (+ per Menace) and
    /// shoves her off at RingKnock (a dash slips through it); it stains cleansed ground it crosses back to RingStain.
    /// </summary>
    public const float Reach = 4f, RingSpeed = 12f, RingBand = 0.9f, RingDamage = 6f, RingDamagePerMenace = 2f, RingKnock = 8f, RingStain = 0.35f;
    /// <summary>Landing on uncorrupted ground stains StainRadius of it toward StainInk.</summary>
    public const float StainRadius = 2f, StainInk = 0.7f;

    /// <summary>The roll: it tips onto its rim (Tip), chases her for Roll seconds at RollSpeed (+ per Menace), turning at most Turn rad/s, and rights itself (Tip).</summary>
    public const float Tip = 0.4f, Roll = 4f, RollSpeed = 4.5f, RollSpeedPerMenace = 0.5f, Turn = 2.2f;
    /// <summary>Rolling into her: Bump damage and a shove; its trail inks TrailRadius toward TrailInk.</summary>
    public const float Bump = 10f, BumpKnock = 8f, TrailRadius = 0.5f, TrailInk = 0.6f;
    /// <summary>Resting between its attacks (the best moment to strike it).</summary>
    public const float Rest = 2.5f;

    /// <summary>Freed: its light washes this wide, and it leaves ShellsMin–Max shells.</summary>
    public const float BurstRadius = 4f;
    public const int ShellsMin = 2, ShellsMax = 3;
}

public enum ThumpState { Idle, Windup, Flight, Landing, Rest, Tipping, Rolling, Rising }

/// <summary>A Thumpsquirt: a tube of ink that leaps at her and rolls after her.</summary>
public sealed class Thumpsquirt
{
    public Vector2 Position, Velocity;
    public ThumpState State;
    /// <summary>Seconds into the present state.</summary>
    public float Timer;
    /// <summary>A leap's take-off and landing; leaps left in this set; what follows the next rest (a roll, or leaps).</summary>
    public Vector2 From, To;
    public int LeapsLeft;
    public bool RollNext;
    /// <summary>Rolling: which way it rolls.</summary>
    public Vector2 Heading = Vector2.UnitX;
    /// <summary>Its landing's ring (−1: none), and which ring it is (her hit once per ring).</summary>
    public float Ring = -1f;
    public int RingId;
    public bool Alive = true;
    public float Seed;
    public CorruptionMeter Corruption = new(ThumpTuning.Meter, ThumpTuning.Regen, ThumpTuning.Delay);
    /// <summary>In the air: it cannot be harmed.</summary>
    public bool Airborne => State == ThumpState.Flight;
    /// <summary>How far through its leap it is (0..1).</summary>
    public float LeapT => State == ThumpState.Flight ? MathF.Min(Timer / ThumpTuning.Flight, 1f) : 0f;
}

public sealed partial class PlaneWorld
{
    public List<Thumpsquirt> Thumpsquirts { get; } = new();
    int _rings2, _ringHit;

    /// <summary>A level's Thumpsquirts (Depth 2 on): on open floor far from her start, apart, out of the arena.</summary>
    void PlaceThumpsquirts()
    {
        if (Map.Depth < ThumpTuning.FirstDepth) return;
        int want = Map.Depth == ThumpTuning.FirstDepth ? 1 : Map.Depth == ThumpTuning.FirstDepth + 1 ? 1 + _blightRng.Int(2) : 2;
        want = Math.Min(want, ThumpTuning.Max);
        var reach = Gen.TopDown.LevelValidator.Distances(Map, Map.Start.Position);
        for (int tries = 0; tries < 400 && Thumpsquirts.Count < want; tries++)
        {
            var at = new Vector2(_blightRng.Range(4f, Gen.TopDown.LevelMap.Size - 4f), _blightRng.Range(4f, Gen.TopDown.LevelMap.Size - 4f));
            if (!Clear(at, 1.2f) || Vector2.Distance(at, Map.Start.Position) < ThumpTuning.FromStart) continue;
            if (Map.HasBoss && Vector2.Distance(at, ArenaCenter) < ArenaRadius + 4f) continue;
            if (Map.Shaft.Contains(at, 3f) || float.IsPositiveInfinity(Gen.TopDown.LevelValidator.DistanceAt(reach, at))) continue;
            if (Thumpsquirts.Any(t => Vector2.Distance(t.Position, at) < ThumpTuning.Spacing)) continue;
            var t = new Thumpsquirt { Position = at, Seed = _blightRng.Range(0f, 100f), Timer = _blightRng.Range(0f, 2f) };
            t.Corruption.Max = t.Corruption.Value = ThumpTuning.Meter * (1f + ThumpTuning.MeterPerMenace * Menace);
            t.Corruption.Regen *= 1f + 0.5f * Menace;
            Thumpsquirts.Add(t);
        }
    }

    void StepThumpsquirts(CorruptionField field)
    {
        var p = Player;
        foreach (var t in Thumpsquirts)
        {
            if (!t.Alive) continue;
            t.Corruption.Step(Dt);
            t.Timer += Dt;
            StepThumpRing(t, field);
            float her = Vector2.Distance(p.Position, t.Position);
            switch (t.State)
            {
                case ThumpState.Idle:
                    if (her < ThumpTuning.Wake && !Defeated)
                    {
                        // Awake: it starts with a set of leaps.
                        t.LeapsLeft = ThumpTuning.Leaps;
                        BeginWindup(t);
                    }
                    break;
                case ThumpState.Windup:
                    if (t.Timer < ThumpTuning.Windup) break;
                    // It locks on where she is now (or as far as it can leap toward her), onto open floor.
                    var to = p.Position;
                    if (Vector2.Distance(to, t.Position) > ThumpTuning.Range) to = t.Position + SafeNormalize(to - t.Position) * ThumpTuning.Range;
                    for (int k = 0; k < 8 && !Clear(to, ThumpTuning.Body); k++) to = Vector2.Lerp(to, t.Position, 0.2f);
                    if (ArenaSealed && Vector2.Distance(to, ArenaCenter) < ArenaRadius + ThumpTuning.Body) to = OutsideArena(to);
                    t.From = t.Position;
                    t.To = Clear(to, ThumpTuning.Body) ? to : t.Position;
                    Enter(t, ThumpState.Flight);
                    Events.Add(new PlaneEvent(PlaneEventType.ThumpLeap, t.From, t.To - t.From, ThumpTuning.Flight));
                    break;
                case ThumpState.Flight:
                    t.Position = Vector2.Lerp(t.From, t.To, t.LeapT);
                    if (t.Timer >= ThumpTuning.Flight) Land(t, field);
                    break;
                case ThumpState.Landing:
                    if (t.Timer < ThumpTuning.Squash) break;
                    if (--t.LeapsLeft > 0 && her < ThumpTuning.Sleep) BeginWindup(t);
                    else
                    {
                        t.RollNext = true;
                        Enter(t, ThumpState.Rest);
                    }
                    break;
                case ThumpState.Rest:
                    if (t.Timer < ThumpTuning.Rest) break;
                    if (her > ThumpTuning.Sleep || Defeated) Enter(t, ThumpState.Idle);
                    else if (t.RollNext)
                    {
                        t.Heading = SafeNormalize(p.Position - t.Position);
                        if (t.Heading == Vector2.Zero) t.Heading = Vector2.UnitX;
                        Enter(t, ThumpState.Tipping);
                    }
                    else
                    {
                        t.LeapsLeft = ThumpTuning.Leaps;
                        BeginWindup(t);
                    }
                    break;
                case ThumpState.Tipping:
                    if (t.Timer >= ThumpTuning.Tip)
                    {
                        Enter(t, ThumpState.Rolling);
                        Events.Add(new PlaneEvent(PlaneEventType.ThumpRoll, t.Position, t.Heading));
                    }
                    break;
                case ThumpState.Rolling:
                    RollAfterHer(t, field);
                    if (t.Timer >= ThumpTuning.Roll)
                    {
                        t.Velocity = Vector2.Zero;
                        Enter(t, ThumpState.Rising);
                    }
                    break;
                case ThumpState.Rising:
                    if (t.Timer >= ThumpTuning.Tip)
                    {
                        t.RollNext = false;
                        Enter(t, ThumpState.Rest);
                    }
                    break;
            }
        }

        // Her shots of light: anything but a leaping one takes their light; emptied, it is freed.
        foreach (var shot in Shots)
        {
            if (!shot.FromPlayer || shot.Life <= 0f) continue;
            foreach (var t in Thumpsquirts)
            {
                if (!t.Alive || t.Airborne || shot.Struck == t || Vector2.Distance(t.Position, shot.Position) > ThumpTuning.Radius + shot.Radius) continue;
                float light = HitDamage(shot);
                float dealt = Ward(t, t.Position, light);
                bool emptied = dealt > 0f && t.Corruption.Hit(dealt);
                Events.Add(new PlaneEvent(emptied ? PlaneEventType.MobDefeated : PlaneEventType.MobHit, t.Position, SafeNormalize(shot.Velocity), dealt));
                if (emptied) BurstThump(t);
                if (shot.Pierce) shot.Struck = t;
                else
                {
                    Pop(shot, t.Position - shot.Position);
                    break;
                }
            }
        }
        Thumpsquirts.RemoveAll(t => !t.Alive);
    }

    static void Enter(Thumpsquirt t, ThumpState state)
    {
        t.State = state;
        t.Timer = 0f;
    }

    void BeginWindup(Thumpsquirt t)
    {
        Enter(t, ThumpState.Windup);
        Events.Add(new PlaneEvent(PlaneEventType.ThumpWindup, t.Position, SafeNormalize(Player.Position - t.Position), ThumpTuning.Windup));
    }

    /// <summary>It lands: on her, a crushing blow; uncorrupted ground stained; a ring of ink runs out from it.</summary>
    void Land(Thumpsquirt t, CorruptionField field)
    {
        var p = Player;
        t.Position = t.To;
        Enter(t, ThumpState.Landing);
        if (field.At(t.Position) < CorruptionTuning.Clean) field.Spread(t.Position, ThumpTuning.StainRadius, ThumpTuning.StainInk, 1f);
        t.Ring = 0.4f;
        t.RingId = ++_rings2;
        Events.Add(new PlaneEvent(PlaneEventType.ThumpLand, t.Position, Vector2.Zero, ThumpTuning.Reach));
        if (Vector2.Distance(p.Position, t.Position) < ThumpTuning.Crushed + Radius)
        {
            var away = SafeNormalize(p.Position - t.Position);
            if (away == Vector2.Zero) away = SafeNormalize(t.To - t.From);
            if (HurtPlayer(ThumpTuning.Crush, away, DamageSource.Thumpsquirt)) p.Velocity += away * ThumpTuning.CrushKnock;
            _ringHit = t.RingId;
        }
    }

    /// <summary>Its landing's ring runs out: staining cleansed ground it crosses, and shoving and corrupting her once.</summary>
    void StepThumpRing(Thumpsquirt t, CorruptionField field)
    {
        if (t.Ring < 0f) return;
        float r0 = t.Ring, r1 = t.Ring = r0 + ThumpTuning.RingSpeed * Dt;
        // Stains the cleansed ground it crosses back toward a thin ink (only ground that was corrupted to begin with).
        foreach (int cell in field.CellsWithin(t.Position, r1))
        {
            var c = CorruptionField.CellCentre(cell % CorruptionField.N, cell / CorruptionField.N);
            if (Vector2.Distance(c, t.Position) < r0) continue;
            field.Raise(cell, MathF.Min(ThumpTuning.RingStain, field.Initial[cell]), 1f);
        }
        var p = Player;
        if (_ringHit != t.RingId && p.DashTimer <= 0f)
        {
            float her = Vector2.Distance(p.Position, t.Position);
            if (MathF.Abs(her - (r0 + r1) * 0.5f) <= ThumpTuning.RingBand * 0.5f + Radius + (r1 - r0) * 0.5f)
            {
                _ringHit = t.RingId;
                var away = SafeNormalize(p.Position - t.Position);
                if (HurtPlayer(ThumpTuning.RingDamage + ThumpTuning.RingDamagePerMenace * Menace, away, DamageSource.Thumpsquirt)) p.Velocity += away * ThumpTuning.RingKnock;
            }
        }
        if (r1 >= ThumpTuning.Reach) t.Ring = -1f;
    }

    /// <summary>Rolling on its rim after her, turning only so fast, inking a trail; rolling into her bumps her off.</summary>
    void RollAfterHer(Thumpsquirt t, CorruptionField field)
    {
        var p = Player;
        var want = SafeNormalize(p.Position - t.Position);
        if (want != Vector2.Zero)
        {
            float cross = t.Heading.X * want.Y - t.Heading.Y * want.X;
            float angle = MathF.Atan2(cross, Vector2.Dot(t.Heading, want));
            float turn = Math.Clamp(angle, -ThumpTuning.Turn * Dt, ThumpTuning.Turn * Dt);
            float c = MathF.Cos(turn), s = MathF.Sin(turn);
            t.Heading = SafeNormalize(new Vector2(t.Heading.X * c - t.Heading.Y * s, t.Heading.X * s + t.Heading.Y * c));
        }
        t.Velocity = t.Heading * (ThumpTuning.RollSpeed + ThumpTuning.RollSpeedPerMenace * Menace);
        Move(ref t.Position, ref t.Velocity, ThumpTuning.Body, report: false, barrier: ArenaBarrier.Outside);
        field.Spread(t.Position, ThumpTuning.TrailRadius, ThumpTuning.TrailInk, 4f * Dt);
        if (Vector2.Distance(p.Position, t.Position) < ThumpTuning.Radius + Radius)
        {
            var away = SafeNormalize(p.Position - t.Position);
            if (away == Vector2.Zero) away = t.Heading;
            if (HurtPlayer(ThumpTuning.Bump, away, DamageSource.Thumpsquirt)) p.Velocity += away * ThumpTuning.BumpKnock;
        }
    }

    /// <summary>Freed: its light washes the ground round it, it leaves shells, and it is a pale sea squirt again.</summary>
    void BurstThump(Thumpsquirt t)
    {
        t.Alive = false;
        Stats.MobsDefeated++;
        Corruption?.Cleanse(t.Position, ThumpTuning.BurstRadius, 1f);
        int shells = ThumpTuning.ShellsMin + _drops.Int(ThumpTuning.ShellsMax - ThumpTuning.ShellsMin + 1);
        for (int i = 0; i < shells; i++) Shells.Add(new PlaneShell { Position = t.Position, Velocity = Fling(2f, 4f) });
        Events.Add(new PlaneEvent(PlaneEventType.ThumpBurst, t.Position, Vector2.Zero, ThumpTuning.BurstRadius));
    }
}
