using System;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Tide Pulse (docs/PEARLS.md), the active pearl she starts every run with: three quick radial waves of water from her
/// bell over its duration, each telegraphed by her bell contracting (WindupShare of the gap before it), each wider and
/// harder than the last. A wave's front runs out from her to its reach (Reach of the pearl's radius) over FrontShare of
/// the duration, easing off; everything loose it meets is swept out ahead of it, sliding along rock, never into it, and
/// thrown on outward at its Kick (m/s): mobs and corrupted creatures, the reef's fish, shells and hearts, loose pearls,
/// pots, every shot and throw of theirs (turned away from her). The last wave's reach is the pearl's radius, so when the
/// waves are done the water round her bell is clear that far. Rooted things (Blightroots, Lashjaws, Hearthguards on their
/// beds), shop stands, the boss and her own light stay put.
/// </summary>
public static class PushTuning
{
    /// <summary>Each wave: when it leaves her (share of the duration), how far it reaches (share of the radius), how hard it throws (m/s).</summary>
    public static readonly float[] At = { 0.22f, 0.52f, 0.82f }, Reach = { 0.6f, 0.8f, 1f }, Kick = { 5f, 7f, 10f };
    /// <summary>How long a wave's front takes to reach its full width, and the bell's contraction before each (shares of the duration).</summary>
    public const float FrontShare = 0.16f, WindupShare = 0.18f;

    /// <summary>Wave k's front now (m from her; −1 before it leaves or once it is spent), with its time, the duration and the radius.</summary>
    public static float Front(int k, float t, float duration, float radius)
    {
        float u = (t - At[k] * duration) / (FrontShare * duration);
        if (u <= 0f || u > 1f) return -1f;
        float e = 1f - (1f - u) * (1f - u);
        return Reach[k] * radius * e;
    }

    /// <summary>
    /// How contracted her bell is now (0 open; a wave's squeeze deeper than the last, the third past a stroke's 1): drawing in
    /// before each wave, snapping open as it leaves.
    /// </summary>
    public static float Contraction(float t, float duration)
    {
        if (t < 0f) return 0f;
        float c = 0f;
        for (int k = 0; k < At.Length; k++)
        {
            float left = At[k] * duration - t;
            float windup = WindupShare * duration;
            float deep = 0.85f + 0.15f * k;
            if (left > 0f && left < windup) c = MathF.Max(c, deep * (1f - left / windup));
            // Snapping open just after it goes.
            else if (left <= 0f && left > -0.06f * duration) c = MathF.Max(c, deep * (1f + left / (0.06f * duration)));
        }
        return c;
    }
}

public sealed partial class PlaneWorld
{
    /// <summary>Tide Pulse used: its waves begin (PushTime counts up from 0 over the duration).</summary>
    void BeginPushWave(float duration, float radius)
    {
        var p = Player;
        p.PushTime = 0f;
        p.PushLength = MathF.Max(duration, 0.1f);
        p.PushRadius = radius;
    }

    /// <summary>The waves under way: each one's front sweeps everything loose out ahead of it, and throws it on.</summary>
    void StepPushWave()
    {
        var p = Player;
        if (p.PushTime < 0f) return;
        float before = p.PushTime;
        p.PushTime += Dt;
        for (int k = 0; k < PushTuning.At.Length; k++)
        {
            if (before < PushTuning.At[k] * p.PushLength && p.PushTime >= PushTuning.At[k] * p.PushLength)
            {
                Events.Add(new PlaneEvent(PlaneEventType.PushWave, p.Position, Vector2.Zero, k));
                // The first wave shakes off whatever clings to her.
                if (k == 0)
                    foreach (var m in Murklings)
                    {
                        if (!m.Alive || !m.Clinging) continue;
                        m.Clinging = false;
                        var away = SafeNormalize(m.ClingOffset);
                        m.Velocity = (away == Vector2.Zero ? -p.Aim : away) * BlightTuning.ShakeSpeed;
                        m.Shaken = BlightTuning.ShakeSeconds;
                    }
            }
            float front = PushTuning.Front(k, p.PushTime, p.PushLength, p.PushRadius);
            if (front > 0f) Sweep(p.Position, front, PushTuning.Kick[k]);
        }
        if (p.PushTime >= p.PushLength) p.PushTime = -1f;
    }

    /// <summary>One wave's front, this far from her: everything loose inside it is carried out to it and thrown on at kick.</summary>
    void Sweep(Vector2 her, float front, float kick)
    {
        foreach (var mob in Mobs)
            if (mob.Alive && Swept(her, front, PufferlingTuning.CalmRadius, ref mob.Position) is { } d) mob.Knock = Throw(mob.Knock, d, kick);
        foreach (var m in Murklings)
            if (m.Alive && !m.Clinging && m.Bud <= 0f && Swept(her, front, m.Body, ref m.Position) is { } d) m.Knock = Throw(m.Knock, d, kick);
        foreach (var b in Blotters)
            if (b.Alive && b.Rise <= 0f && Swept(her, front, BlotterTuning.Radius, ref b.Position) is { } d) b.Knock = Throw(b.Knock, d, kick);
        foreach (var w in Inkwardens)
            if (w.Alive && Swept(her, front, WardenTuning.Radius, ref w.Position) is { } d) w.Knock = Throw(w.Knock, d, kick);
        foreach (var t in Thumpsquirts)
            if (t.Alive && !t.Airborne && Swept(her, front, ThumpTuning.Body, ref t.Position) is { } d) t.Knock = Throw(t.Knock, d, kick);
        foreach (var fish in Fish)
            if (Swept(her, front, PufferlingTuning.CalmRadius, ref fish.Position) is { } d) fish.Velocity = Throw(fish.Velocity, d, kick);
        foreach (var fish in ReefFish)
            if (!fish.Gone && Swept(her, front, 0.25f, ref fish.Position) is { } d) fish.Velocity = Throw(fish.Velocity, d, kick);
        foreach (var shell in Shells)
            if (!shell.Taken && Swept(her, front, 0.1f, ref shell.Position) is { } d) shell.Velocity = Throw(shell.Velocity, d, kick);
        foreach (var heart in Hearts)
            if (!heart.Taken && Swept(her, front, 0.15f, ref heart.Position) is { } d) heart.Velocity = Throw(heart.Velocity, d, kick);
        foreach (var pearl in Pearls)
            if (!pearl.Taken && !pearl.Absorbing) Swept(her, front, 0.2f, ref pearl.Position);
        // Their shots and throws: carried out and turned away from her, as fast as they flew.
        foreach (var shot in Shots)
        {
            if (shot.FromPlayer) continue;
            var was = shot.Position;
            if (Swept(her, front, shot.Radius, ref shot.Position) is not { } d) continue;
            shot.Line += shot.Position - was;
            shot.Velocity = d * MathF.Max(shot.Velocity.Length(), kick);
        }
        foreach (var s in MurkShots)
            if (Swept(her, front, s.Radius, ref s.Position) is { } d) s.Velocity = d * MathF.Max(s.Velocity.Length(), kick);
        foreach (var bomb in Bombs)
        {
            var off = bomb.To - her;
            float dist = off.Length(), edge = front + BlotterTuning.SplashRadius * 0.5f;
            if (dist < edge) bomb.To = her + (dist > 1e-4f ? off / dist : -Vector2.UnitY) * edge;
        }
        foreach (var vase in Vases)
            if (!vase.Broken) SweepVase(vase, her, front);
    }

    /// <summary>
    /// Carries a thing of this radius inside the front out to it (its near side at the front), sliding along rock: the way
    /// it was carried (unit, out from her), or null when the front has not reached it.
    /// </summary>
    Vector2? Swept(Vector2 her, float front, float radius, ref Vector2 at)
    {
        var off = at - her;
        float d = off.Length(), edge = front + radius;
        if (d >= edge) return null;
        var dir = d > 1e-4f ? off / d : -Player.Aim;
        var step = dir * ((edge - d) / Dt);
        Move(ref at, ref step, MathF.Min(radius, 0.5f), report: false);
        return dir;
    }

    /// <summary>A velocity thrown outward along dir at kick at least (what it had along dir kept if faster; across, dropped).</summary>
    static Vector2 Throw(Vector2 velocity, Vector2 dir, float kick) => dir * MathF.Max(Vector2.Dot(velocity, dir), kick);

    /// <summary>A pot inside the front slides out to it where the water is open and no other pot stands.</summary>
    void SweepVase(PlaneVase vase, Vector2 her, float front)
    {
        var off = vase.Position - her;
        float d = off.Length(), edge = front + vase.Radius;
        if (d >= edge) return;
        var dir = d > 1e-4f ? off / d : -Player.Aim;
        float left = edge - d;
        while (left > 1e-4f)
        {
            float step = MathF.Min(left, 0.1f);
            var next = vase.Position + dir * step;
            if (!Map.IsOpen(next) || !Map.IsOpen(next + dir * vase.Radius)) break;
            bool free = true;
            foreach (var rock in _rocks)
                if (rock.Contains(next, vase.Radius)) free = false;
            foreach (var other in Vases)
                if (other != vase && !other.Broken && Vector2.Distance(other.Position, next) < other.Radius + vase.Radius) free = false;
            if (!free) break;
            vase.Position = next;
            left -= step;
        }
    }
}
