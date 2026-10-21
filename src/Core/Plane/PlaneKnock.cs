using System;
using System.Numerics;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Her light's hits shove a corrupted creature a little along the shot (docs/CORRUPTION.md): the smaller it is, the
/// further. A creature of Reference radius takes Speed metres a second, a smaller or bigger one Reference/radius times
/// that (within Least–Most of it), easing off at Decay (about Speed/Decay metres all told); hits in quick succession
/// add up to at most Stack shoves' worth. Rooted creatures (Blightroots, Lashjaws) are not moved.
/// </summary>
public static class KnockTuning
{
    public const float Speed = 3f, Reference = 0.5f, Least = 0.25f, Most = 1.4f, Decay = 9f, Stack = 1.5f;
}

public sealed partial class PlaneWorld
{
    /// <summary>The shove (m/s) a shot of her light gives a creature of this radius (her knockback stat included).</summary>
    public float KnockFor(float radius) =>
        KnockTuning.Speed * Math.Clamp(KnockTuning.Reference / MathF.Max(radius, 0.05f), KnockTuning.Least, KnockTuning.Most) * Run.Loadout.Stats[Stat.Knockback];

    /// <summary>A shot along <paramref name="dir"/> shoves a creature of this radius (its knock velocity, capped).</summary>
    void Shove(ref Vector2 knock, Vector2 dir, float radius)
    {
        if (dir.LengthSquared() < 1e-8f) return;
        float speed = KnockFor(radius);
        knock += Vector2.Normalize(dir) * speed;
        float most = speed * KnockTuning.Stack;
        if (knock.LengthSquared() > most * most) knock = Vector2.Normalize(knock) * most;
    }

    /// <summary>Every shoved creature drifts with its knock as it eases off; stopped dead by rock (never pushed into it).</summary>
    void StepKnocks()
    {
        foreach (var m in Murklings)
            if (m.Alive && !m.Clinging && !m.Budding) Drift(ref m.Position, ref m.Knock, m.Body);
        foreach (var b in Blotters)
            if (b.Alive && b.Rise <= 0f) Drift(ref b.Position, ref b.Knock, BlotterTuning.Radius);
        foreach (var w in Inkwardens)
            if (w.Alive) Drift(ref w.Position, ref w.Knock, WardenTuning.Radius);
        foreach (var t in Thumpsquirts)
            if (t.Alive && !t.Airborne) Drift(ref t.Position, ref t.Knock, ThumpTuning.Body);
    }

    void Drift(ref Vector2 position, ref Vector2 knock, float radius)
    {
        if (knock.LengthSquared() < 1e-4f)
        {
            knock = Vector2.Zero;
            return;
        }
        // A small probe: big bodies may already brush the rock they live by; they only move where the water is open.
        var next = position + knock * Dt;
        if (Clear(next, MathF.Min(radius, 0.5f))) position = next;
        else knock = Vector2.Zero;
        knock *= MathF.Exp(-KnockTuning.Decay * Dt);
    }
}
