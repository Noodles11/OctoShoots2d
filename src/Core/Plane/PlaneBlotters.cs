using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Blotters (docs/CORRUPTION.md): hermit crabs the corruption took over, a tiny crab under a huge shell of ink, that
/// spread the corruption. Each one creeps toward clean floor and smears ink over the ground it crawls across. They are slow but take a lot of light to cleanse, and murklings bud
/// round them as guards. A few wake with the level; more may be born out of thick corruption as it goes on.
/// </summary>
public static class BlotterTuning
{
    /// <summary>How many wake with the level: Start + PerMenace × Menace, at most Max alive at once.</summary>
    public const float Start = 2f, PerMenace = 1f;
    public const int Max = 6;
    /// <summary>Its corruption meter: 100 in the Shallows (+25% per Menace); it regrows slowly once Delay passes without a hit.</summary>
    public const float Meter = 100f, MeterPerMenace = 0.25f, Regen = 5f, Delay = 2f;
    /// <summary>Its footprint against rock, its creep (m/s, more with Menace) and how quickly it turns.</summary>
    public const float Radius = 1.4f, Speed = 1.2f, SpeedPerMenace = 0.2f, Turn = 1.6f;
    /// <summary>It smears the ground under it this wide toward Ink, by Smear a second at its middle.</summary>
    public const float SmearRadius = 2.6f, Ink = 0.9f, Smear = 1.6f;
    /// <summary>Touching her corrupts her (with the usual hit grace) and shoves her off.</summary>
    public const float ContactDamage = 8f;
    /// <summary>Where it heads: clean floor between Seek and SeekFar metres off, in sight; it re-picks on arrival, after Patience seconds, or when stuck.</summary>
    public const float Seek = 4f, SeekFar = 20f, Arrive = 1.5f, Patience = 10f, Stuck = 2.5f;
    /// <summary>Its guards: every GuardEvery seconds (less with Menace) GuardChance to bud a murkling 2.5–4.5 m from it, up to Guards + Menace alive.</summary>
    public const float GuardEvery = 6f, GuardChance = 0.7f;
    public const int Guards = 2;
    /// <summary>
    /// Born from corruption: every BirthEvery seconds a BirthChance (+ per Menace) roll to rise from thick ground far from
    /// her. It is made of ink: rising, it drinks BirthInk (six murklings' worth, density × m²), the nearest first and then
    /// further out; with less left on the level, it is not born. The ones that wake with the level drink nothing.
    /// </summary>
    public const float BirthEvery = 20f, BirthChance = 0.05f, BirthPerMenace = 0.03f, BirthClearance = 18f, BirthInk = BlightTuning.Ink * 6f;
    /// <summary>Waking with the level: this far from her start, and apart.</summary>
    public const float FromStart = 25f, Spacing = 15f;
    /// <summary>Cleansed, it bursts: its light washes this wide.</summary>
    public const float BurstRadius = 5f;
    /// <summary>Its shell: one round body this wide that takes her hits and touches her (the crab under it is too small to matter).</summary>
    public const float ShellRadius = 1.9f;
    /// <summary>Touching her shoves her off this hard (m/s).</summary>
    public const float ContactKnockback = 7f;
    /// <summary>
    /// Corruption bombs: every BombEvery seconds its shell pushes one out of its top. It flies BombSeconds in an arc and lands
    /// BombNear–BombFar metres off (at a random spot, or, while it is under attack, where she is), splashing corruption
    /// over SplashRadius and, if she is within it, corrupting her for BombDamage and throwing her back.
    /// </summary>
    public const float BombEvery = 3.5f, BombSeconds = 1.1f, BombNear = 3f, BombFar = 7f, BombReach = 13f;
    public const float SplashRadius = 2.2f, BombDamage = 10f, BombKnockback = 9f, BombInk = 0.9f;
    /// <summary>It counts as under attack for this long after her light last struck it.</summary>
    public const float Provoked = 3f;
}

/// <summary>A Blotter's corruption bomb: arcing from the top of its shell to where it will land.</summary>
public sealed class CorruptionBomb
{
    public Vector2 From, To;
    /// <summary>A boss's arc bomb (gone when it is freed).</summary>
    public bool FromBoss;
    public float Age;
    /// <summary>How far along its flight it is, 0..1.</summary>
    public float T => MathF.Min(Age / BlotterTuning.BombSeconds, 1f);
    public Vector2 Position => Vector2.Lerp(From, To, T);
}

/// <summary>A Blotter: a corrupted hermit crab under a huge shell of ink, creeping toward clean floor and corrupting it.</summary>
public sealed class Blotter
{
    /// <summary>Shoved by her hits (PlaneKnock): this velocity, easing off.</summary>
    public Vector2 Knock;
    public Vector2 Position, Velocity, Target;
    /// <summary>Where it faces (unit), turning slowly toward its target.</summary>
    public Vector2 Heading = new(1f, 0f);
    public bool Alive = true;
    public CorruptionMeter Corruption = new(BlotterTuning.Meter, BlotterTuning.Regen, BlotterTuning.Delay);
    public float Seeking, StuckFor, GuardTimer, Seed, BombTimer, SinceStruck = 99f;

    /// <summary>Whether a circle touches its shell.</summary>
    public bool Touches(Vector2 at, float radius) =>
        Vector2.DistanceSquared(Position, at) < (BlotterTuning.ShellRadius + radius) * (BlotterTuning.ShellRadius + radius);
    /// <summary>Rising out of the ground (counting down; above zero it is not yet out), out of RiseTotal.</summary>
    public float Rise, RiseTotal;
    /// <summary>The ink it is drinking as it rises (one born during the level), and the ink it is made of (freed when it bursts).</summary>
    public InkDraw? Draw;
    public float Matter;
}

public sealed partial class PlaneWorld
{
    public List<Blotter> Blotters { get; } = new();
    public List<CorruptionBomb> Bombs { get; } = new();
    float _birthTimer;

    float BlotterMeter => BlotterTuning.Meter * (1f + BlotterTuning.MeterPerMenace * Menace);

    /// <summary>A level's Blotters wake: on thick corruption, far from her start, apart, out of the arena.</summary>
    void PlaceBlotters(CorruptionField field)
    {
        var cast = CastOf(Gen.TopDown.CastKind.Blotter);
        if (cast is { None: true }) return;
        int want = cast is not null ? CastCount(cast, BlotterTuning.Max)
            : Math.Min(BlotterTuning.Max, (int)MathF.Round(BlotterTuning.Start + BlotterTuning.PerMenace * Menace));
        var reach = Gen.TopDown.LevelValidator.Distances(Map, Map.Start.Position);
        for (int tries = 0; tries < (cast is null ? 600 : 4000) && Blotters.Count < want; tries++)
        {
            if (!ThickSpot(field, out var at) || !Anchored(cast, Blotters.Count, at)) continue;
            if (Vector2.Distance(at, Map.Start.Position) < BlotterTuning.FromStart) continue;
            if (float.IsPositiveInfinity(Gen.TopDown.LevelValidator.DistanceAt(reach, at))) continue;
            if (Blotters.Any(b => Vector2.Distance(b.Position, at) < BlotterTuning.Spacing)) continue;
            Blotters.Add(NewBlotter(at, rise: 0f));
        }
        _birthTimer = BlotterTuning.BirthEvery;
    }

    /// <summary>A random thick (≥ 0.6), roomy cell, out of the arena and the shaft.</summary>
    bool ThickSpot(CorruptionField field, out Vector2 at)
    {
        int cell = _blightRng.Int(field.Density.Length);
        at = CorruptionField.CellCentre(cell % CorruptionField.N, cell / CorruptionField.N);
        if (field.Density[cell] < 0.6f || !Clear(at, BlotterTuning.Radius + 0.3f)) return false;
        if (Map.HasBoss && Vector2.Distance(at, ArenaCenter) < ArenaRadius + 3f) return false;
        return !Map.Shaft.Contains(at, 3f);
    }

    Blotter NewBlotter(Vector2 at, float rise)
    {
        var b = new Blotter
        {
            Position = at,
            Target = at,
            Rise = rise,
            RiseTotal = rise,
            Seed = _blightRng.Range(0f, 100f),
            GuardTimer = _blightRng.Range(1f, BlotterTuning.GuardEvery),
        };
        b.Corruption.Max = b.Corruption.Value = BlotterMeter;
        float a = _blightRng.Range(0f, MathF.Tau);
        b.Heading = new Vector2(MathF.Cos(a), MathF.Sin(a));
        b.BombTimer = _blightRng.Range(1.5f, BlotterTuning.BombEvery);
        return b;
    }

    void StepBlotters(CorruptionField field)
    {
        var p = Player;
        // Born from the corruption itself: now and then one rises from thick ground far from her.
        _birthTimer -= Dt;
        // A quiet level (its template's rule) and one whose template casts no Blotters: none is born.
        if (_birthTimer <= 0f && (Quiet || CastOf(Gen.TopDown.CastKind.Blotter) is { None: true })) _birthTimer = BlotterTuning.BirthEvery;
        if (_birthTimer <= 0f)
        {
            _birthTimer = BlotterTuning.BirthEvery;
            if (Blotters.Count(b => b.Alive) < BlotterTuning.Max && _blightRng.NextFloat() < BlotterTuning.BirthChance + BlotterTuning.BirthPerMenace * Menace)
                for (int tries = 0; tries < 40; tries++)
                {
                    if (!ThickSpot(field, out var at) || Vector2.Distance(at, p.Position) < BlotterTuning.BirthClearance) continue;
                    if (field.Plan(at, BlotterTuning.BirthInk) is not { } draught) continue;
                    var born = NewBlotter(at, rise: 1.5f);
                    born.Draw = draught;
                    Blotters.Add(born);
                    // How far out it drinks and how thick that ink is (the view gathers it in).
                    Events.Add(new PlaneEvent(PlaneEventType.BlotterBorn, at, new Vector2(draught.Radius, draught.Ink), 1.5f));
                    break;
                }
        }

        float speed = BlotterTuning.Speed + BlotterTuning.SpeedPerMenace * Menace;
        foreach (var b in Blotters)
        {
            if (!b.Alive) continue;
            b.Corruption.Step(Dt);
            if (b.Rise > 0f)
            {
                b.Rise -= Dt;
                // It drinks the ink round it up into its body as it rises.
                if (b.Draw is { } draw)
                {
                    b.Matter += field.Drink(draw, b.Rise > 0f ? Dt / b.RiseTotal : 1f);
                    if (b.Rise <= 0f) b.Draw = null;
                }
                continue;
            }
            // Where to: clean floor in sight, the nearer the better; re-picked on arrival, when bored, or stuck.
            b.Seeking -= Dt;
            if (b.Seeking <= 0f || Vector2.Distance(b.Position, b.Target) < BlotterTuning.Arrive || b.StuckFor > BlotterTuning.Stuck
                || field.At(b.Target) > 0.5f)
                PickBlotterTarget(b, field);
            // It turns slowly toward it and creeps on, sliding along rock.
            var want = SafeNormalize(b.Target - b.Position);
            if (want != Vector2.Zero)
            {
                float cross = b.Heading.X * want.Y - b.Heading.Y * want.X;
                float angle = MathF.Atan2(cross, Vector2.Dot(b.Heading, want));
                float turn = Math.Clamp(angle, -BlotterTuning.Turn * Dt, BlotterTuning.Turn * Dt);
                float c = MathF.Cos(turn), s = MathF.Sin(turn);
                b.Heading = SafeNormalize(new Vector2(b.Heading.X * c - b.Heading.Y * s, b.Heading.X * s + b.Heading.Y * c));
            }
            // A crab's gait under its load: it surges and eases.
            float gait = 0.7f + 0.3f * MathF.Sin(Time * 2.2f + b.Seed);
            float facing = MathF.Max(0f, Vector2.Dot(b.Heading, want));
            b.Velocity = b.Heading * speed * gait * (0.25f + 0.75f * facing);
            var before = b.Position;
            Move(ref b.Position, ref b.Velocity, BlotterTuning.Radius, report: false);
            float moved = Vector2.Distance(before, b.Position);
            b.StuckFor = moved < speed * Dt * 0.2f ? b.StuckFor + Dt : 0f;
            // Its trail: ink smeared over the ground beneath it.
            field.Spread(b.Position, BlotterTuning.SmearRadius, BlotterTuning.Ink, BlotterTuning.Smear * Dt);

            // Touching its shell corrupts her and shoves her off.
            if (b.Touches(p.Position, Radius))
            {
                var away = SafeNormalize(p.Position - b.Position);
                if (HurtPlayer(BlotterTuning.ContactDamage, away, DamageSource.Blotter)) p.Velocity += away * BlotterTuning.ContactKnockback;
            }

            // Its bombs: pushed up out of the top of its shell, at her while she is attacking it, else anywhere near.
            b.SinceStruck += Dt;
            b.BombTimer -= Dt;
            if (b.BombTimer <= 0f)
            {
                b.BombTimer = BlotterTuning.BombEvery;
                var mouth = b.Position;
                Vector2 to;
                if (b.SinceStruck < BlotterTuning.Provoked && Vector2.Distance(p.Position, mouth) < BlotterTuning.BombReach) to = p.Position;
                else
                {
                    float a = _blightRng.Range(0f, MathF.Tau), r = _blightRng.Range(BlotterTuning.BombNear, BlotterTuning.BombFar);
                    to = b.Position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
                }
                // A sealed arena is closed to it: a bomb bound inside lands just outside instead.
                Bombs.Add(new CorruptionBomb { From = mouth, To = to });
                Events.Add(new PlaneEvent(PlaneEventType.BombThrown, mouth, to - mouth, BlotterTuning.BombSeconds));
            }

            // Its guards: murklings bud round it.
            b.GuardTimer -= Dt;
            if (b.GuardTimer <= 0f)
            {
                b.GuardTimer = BlotterTuning.GuardEvery / (1f + 0.3f * Menace) * _blightRng.Range(0.8f, 1.2f);
                int guards = Murklings.Count(m => m.Alive && m.Guarding == b);
                if (guards < BlotterTuning.Guards + (int)Menace && _blightRng.NextFloat() < BlotterTuning.GuardChance)
                    // A few spots round it, the first in open water (it is often against rock in a narrow cave).
                    for (int tries = 0; tries < 8; tries++)
                    {
                        float a = _blightRng.Range(0f, MathF.Tau), r = _blightRng.Range(2.5f, 4.5f);
                        var at = b.Position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
                        if (!Clear(at, BlightTuning.MurkRadius) || Vector2.Distance(at, p.Position) <= 3f) continue;
                        if (Bud(at, -1) is { } guard) guard.Guarding = b;
                        break;
                    }
            }
        }

        // Her shots of light: each takes its damage off a Blotter's meter and bursts on it; emptied, it bursts.
        foreach (var shot in Shots)
        {
            if (!shot.FromPlayer || shot.Life <= 0f) continue;
            foreach (var b in Blotters)
            {
                if (!b.Alive || b.Rise > 0f || shot.Struck == b || !Reaches(shot, b.Position, BlotterTuning.ShellRadius)) continue;
                float light = HitDamage(shot);
                b.SinceStruck = 0f;
                float dealt = Ward(b, b.Position, light);
                bool emptied = dealt > 0f && b.Corruption.Hit(dealt);
                Events.Add(new PlaneEvent(emptied ? PlaneEventType.MobDefeated : PlaneEventType.MobHit, b.Position, SafeNormalize(shot.Velocity), dealt));
                Shove(ref b.Knock, shot.Velocity, BlotterTuning.ShellRadius);
                if (emptied) BurstBlotter(b, field);
                if (shot.Pierce) shot.Struck = b;
                else
                {
                    Pop(shot, b.Position - shot.Position);
                    break;
                }
            }
        }
        Blotters.RemoveAll(b => !b.Alive);
        StepBombs(field);
    }

    /// <summary>Bombs in flight land with a splash of corruption, corrupting her and throwing her back if she is in it.</summary>
    void StepBombs(CorruptionField field)
    {
        var p = Player;
        foreach (var bomb in Bombs)
        {
            bomb.Age += Dt;
            if (bomb.Age < BlotterTuning.BombSeconds) continue;
            var at = bomb.To;
            field.Spread(at, BlotterTuning.SplashRadius, BlotterTuning.BombInk, 1f);
            Events.Add(new PlaneEvent(PlaneEventType.BombSplash, at, Vector2.Zero, BlotterTuning.SplashRadius));
            float d = Vector2.Distance(p.Position, at);
            if (d < BlotterTuning.SplashRadius + Radius)
            {
                var away = SafeNormalize(p.Position - at);
                if (away == Vector2.Zero) away = SafeNormalize(bomb.To - bomb.From);
                if (HurtPlayer(BlotterTuning.BombDamage, away, DamageSource.Blotter)) p.Velocity += away * BlotterTuning.BombKnockback;
            }
        }
        Bombs.RemoveAll(b => b.Age >= BlotterTuning.BombSeconds);
    }

    void PickBlotterTarget(Blotter b, CorruptionField field)
    {
        b.Seeking = BlotterTuning.Patience;
        b.StuckFor = 0f;
        Vector2? best = null;
        float bestScore = float.MaxValue;
        for (int i = 0; i < 28; i++)
        {
            float a = _blightRng.Range(0f, MathF.Tau), r = _blightRng.Range(BlotterTuning.Seek, BlotterTuning.SeekFar);
            var at = b.Position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
            if (!Map.IsOpen(at) || !Clear(at, BlotterTuning.Radius) || !LineOfSight(b.Position, at)) continue;
            if (Map.HasBoss && Vector2.Distance(at, ArenaCenter) < ArenaRadius + 1f) continue;
            float d = field.At(at);
            // Clean ground it may corrupt is what it wants; a little preference for straight ahead.
            float score = r * (d < CorruptionTuning.Clean ? 1f : 4f + 6f * d) - Vector2.Dot(SafeNormalize(at - b.Position), b.Heading) * 2f;
            if (field.Guard[CorruptionField.Index((int)(at.X / CorruptionTuning.Cell), (int)(at.Y / CorruptionTuning.Cell))] < 0.5f) score += 50f;
            if (score < bestScore)
            {
                bestScore = score;
                best = at;
            }
        }
        b.Target = best ?? b.Position - b.Heading * 3f;
    }

    /// <summary>Cleansed: it bursts, washing the ground round it with light; its guards are left on their own.</summary>
    void BurstBlotter(Blotter b, CorruptionField field)
    {
        b.Alive = false;
        Stats.MobsDefeated++;
        // The ink it was made of is cleansed with it.
        b.Draw = null;
        field.Release(b.Matter);
        field.Cleanse(b.Position, BlotterTuning.BurstRadius, 1f);
        foreach (var m in Murklings)
            if (m.Guarding == b) m.Guarding = null;
        Events.Add(new PlaneEvent(PlaneEventType.BlotterBurst, b.Position, Vector2.Zero, BlotterTuning.BurstRadius));
    }
}
