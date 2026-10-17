using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>
/// The Inkwarden (docs/CORRUPTION.md): a corrupted sea anemone that shelters its kin as an anemone shelters clownfish.
/// Passive: it never attacks. Every corrupted creature within its reach takes no harm while it stands; a blocked hit
/// shows as an ink tether from each warden guarding it. Wardens cannot guard each other, so they are always the way in.
/// From Depth 2 (the Sunken Galleon) one sits beside each Blightroot; deeper, more roam toward wherever its kin gather.
/// </summary>
public static class WardenTuning
{
    /// <summary>From internal depth 3 (shown Depth 2): beside each Blightroot; from the next depth also Roamers + Menace roaming; at most Max.</summary>
    public const int FirstDepth = 3, Max = 6;
    public const float Roamers = 1f;
    /// <summary>How far it guards; its body (for hits and moving).</summary>
    public const float Reach = 8f, Radius = 0.6f;
    /// <summary>Beside a Blightroot: this far from it.</summary>
    public const float NearMin = 3f, NearMax = 5f;
    /// <summary>Roaming: it drifts at Drift m/s toward where most of its kin gather (looked for every Look seconds, within Seek), settling within Settle.</summary>
    public const float Drift = 0.6f, Look = 2f, Seek = 25f, Settle = 3f;
    /// <summary>Waking with the level (roamers): this far from her start, and apart.</summary>
    public const float FromStart = 20f, Spacing = 12f;
    /// <summary>Its corruption meter: 60 (+20% per Menace), regrowing at Regen once Delay passes without a hit.</summary>
    public const float Meter = 60f, MeterPerMenace = 0.2f, Regen = 10f, Delay = 1f;
    /// <summary>Freed: its light washes this wide, and it leaves ShellsMin–Max shells.</summary>
    public const float BurstRadius = 3f;
    public const int ShellsMin = 1, ShellsMax = 2;
}

/// <summary>An Inkwarden: a corrupted anemone guarding the corrupted creatures round it.</summary>
public sealed class Inkwarden
{
    public Vector2 Position, Velocity, Target;
    public bool Alive = true, Roams;
    public float Seed, LookTimer;
    public CorruptionMeter Corruption = new(WardenTuning.Meter, WardenTuning.Regen, WardenTuning.Delay);
}

public sealed partial class PlaneWorld
{
    public List<Inkwarden> Inkwardens { get; } = new();

    /// <summary>A level's Inkwardens: one beside each Blightroot (Depth 2 on), and roamers deeper.</summary>
    void PlaceInkwardens()
    {
        if (Map.Depth < WardenTuning.FirstDepth) return;
        foreach (var root in Blightroots)
        {
            if (Inkwardens.Count >= WardenTuning.Max) break;
            for (int tries = 0; tries < 24; tries++)
            {
                float a = _blightRng.Range(0f, MathF.Tau), r = _blightRng.Range(WardenTuning.NearMin, WardenTuning.NearMax);
                var at = root.Position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
                if (!Clear(at, WardenTuning.Radius + 0.2f)) continue;
                Inkwardens.Add(NewWarden(at, roams: false));
                break;
            }
        }
        if (Map.Depth <= WardenTuning.FirstDepth) return;
        int roamers = (int)MathF.Round(WardenTuning.Roamers + Menace);
        var reach = Gen.TopDown.LevelValidator.Distances(Map, Map.Start.Position);
        for (int tries = 0; tries < 400 && roamers > 0 && Inkwardens.Count < WardenTuning.Max; tries++)
        {
            var at = new Vector2(_blightRng.Range(4f, Gen.TopDown.LevelMap.Size - 4f), _blightRng.Range(4f, Gen.TopDown.LevelMap.Size - 4f));
            if (!Clear(at, WardenTuning.Radius + 0.4f) || Vector2.Distance(at, Map.Start.Position) < WardenTuning.FromStart) continue;
            if (Map.HasBoss && Vector2.Distance(at, ArenaCenter) < ArenaRadius + 4f) continue;
            if (Map.Shaft.Contains(at, 3f) || float.IsPositiveInfinity(Gen.TopDown.LevelValidator.DistanceAt(reach, at))) continue;
            if (Inkwardens.Any(w => Vector2.Distance(w.Position, at) < WardenTuning.Spacing)) continue;
            Inkwardens.Add(NewWarden(at, roams: true));
            roamers--;
        }
    }

    Inkwarden NewWarden(Vector2 at, bool roams)
    {
        var w = new Inkwarden { Position = at, Target = at, Roams = roams, Seed = _blightRng.Range(0f, 100f), LookTimer = _blightRng.Range(0f, WardenTuning.Look) };
        w.Corruption.Max = w.Corruption.Value = WardenTuning.Meter * (1f + WardenTuning.MeterPerMenace * Menace);
        w.Corruption.Regen *= 1f + 0.5f * Menace;
        return w;
    }

    /// <summary>
    /// The light that gets through to a corrupted creature at <paramref name="at"/>: none while any Inkwarden (other than
    /// itself) guards it — each guarding warden shows its tether (a Warded event: from the warden, the way to the creature).
    /// </summary>
    float Ward(object kin, Vector2 at, float light)
    {
        if (light <= 0f || kin is Inkwarden || kin is PlaneBoss) return light;
        bool warded = false;
        foreach (var w in Inkwardens)
        {
            if (!w.Alive || Vector2.Distance(w.Position, at) > WardenTuning.Reach) continue;
            warded = true;
            Events.Add(new PlaneEvent(PlaneEventType.Warded, w.Position, at - w.Position, light));
        }
        return warded ? 0f : light;
    }

    void StepInkwardens()
    {
        foreach (var w in Inkwardens)
        {
            if (!w.Alive) continue;
            w.Corruption.Step(Dt);
            if (!w.Roams) continue;
            // Roaming: toward where most of its kin gather, settling among them.
            w.LookTimer -= Dt;
            if (w.LookTimer <= 0f)
            {
                w.LookTimer = WardenTuning.Look;
                w.Target = KinGathering(w) ?? w.Position;
            }
            var to = w.Target - w.Position;
            float d = to.Length();
            var want = d > WardenTuning.Settle ? to / d * WardenTuning.Drift : Vector2.Zero;
            w.Velocity = MoveToward(w.Velocity, want, 1f * Dt);
            Move(ref w.Position, ref w.Velocity, WardenTuning.Radius, report: false, barrier: ArenaBarrier.Outside);
        }

        // Her shots of light: the warden itself has no guard.
        foreach (var shot in Shots)
        {
            if (!shot.FromPlayer || shot.Life <= 0f) continue;
            foreach (var w in Inkwardens)
            {
                if (!w.Alive || shot.Struck == w || Vector2.Distance(w.Position, shot.Position) > WardenTuning.Radius + shot.Radius) continue;
                float light = HitDamage(shot);
                bool emptied = w.Corruption.Hit(light);
                Events.Add(new PlaneEvent(emptied ? PlaneEventType.MobDefeated : PlaneEventType.MobHit, w.Position, SafeNormalize(shot.Velocity), light));
                if (emptied) FreeWarden(w);
                if (shot.Pierce) shot.Struck = w;
                else
                {
                    Pop(shot, w.Position - shot.Position);
                    break;
                }
            }
        }
        Inkwardens.RemoveAll(w => !w.Alive);
    }

    /// <summary>The corrupted creature (within Seek) with the most kin round it: where a roaming warden settles.</summary>
    Vector2? KinGathering(Inkwarden w)
    {
        var kin = new List<Vector2>();
        foreach (var m in Murklings)
            if (m.Alive && !m.Budding && !m.Clinging) kin.Add(m.Position);
        foreach (var b in Blotters)
            if (b.Alive) kin.Add(b.Position);
        foreach (var t in Thumpsquirts)
            if (t.Alive) kin.Add(t.Position);
        foreach (var r in Blightroots)
            if (r.Alive) kin.Add(r.Position);
        Vector2? best = null;
        int most = 0;
        foreach (var k in kin)
        {
            if (Vector2.Distance(k, w.Position) > WardenTuning.Seek) continue;
            int near = kin.Count(o => Vector2.Distance(o, k) < WardenTuning.Reach);
            if (near > most)
            {
                most = near;
                best = k;
            }
        }
        // Beside them, not on top of them.
        return best is { } at ? at + SafeNormalize(w.Position - at) * 2f : null;
    }

    /// <summary>Freed: its tethers snap, its light washes the ground round it, and it leaves shells.</summary>
    void FreeWarden(Inkwarden w)
    {
        w.Alive = false;
        Stats.MobsDefeated++;
        Corruption?.Cleanse(w.Position, WardenTuning.BurstRadius, 1f);
        int shells = WardenTuning.ShellsMin + _drops.Int(WardenTuning.ShellsMax - WardenTuning.ShellsMin + 1);
        for (int i = 0; i < shells; i++) Shells.Add(new PlaneShell { Position = w.Position, Velocity = Fling(2f, 4f) });
        Events.Add(new PlaneEvent(PlaneEventType.WardenFreed, w.Position, Vector2.Zero, WardenTuning.BurstRadius));
    }
}
