using System;
using System.Linq;

namespace OctoShoots.Core.Bosses;

/// <summary>
/// The validator (docs/BOSS-GENERATOR.md §9): a roll that cannot be won, read or recognised as one creature is re-rolled.
/// It never judges taste beyond that. Returns why a spec fails, or null.
/// </summary>
public static class BossValidator
{
    /// <summary>The shortest warning an attack may have: 0.8 s at Depth 1, 0.35 s at Depth 6 (never lower).</summary>
    public static float TelegraphFloor(float menace) => MathF.Max(0.35f, 0.8f - 0.45f * MathF.Min(menace, 1f));

    /// <summary>The shortest weak-spot window, and its least share of the cycle.</summary>
    public static float WindowFloor(float menace) => 1.2f - 0.2f * MathF.Min(menace, 1f);
    public static float ShareFloor(float menace) => 0.3f - 0.1f * MathF.Min(menace, 1f);

    /// <summary>The most of its pearls in the water at once.</summary>
    public static int InFlightBudget(float menace) => 45 + (int)(30f * MathF.Min(menace, 1f));

    /// <summary>The longest fight against a steady attacker (s).</summary>
    public static float LengthBudget(float menace) => 150f + 60f * MathF.Min(menace, 1f);

    /// <summary>A steady attacker's damage per second while the pearl is open (her plain shots, about two in three landing).</summary>
    public const float SteadyDps = 30f;

    /// <summary>The share of a cycle in which the pearl can be struck, and the window's length.</summary>
    public static (float Share, float Window) Openness(BossSpec s)
    {
        float act = s.Volleys * s.VolleyInterval;
        return s.Exposure switch
        {
            Exposure.OpenMoments => (act / (s.Rest + s.Telegraph + act), act),
            Exposure.Stagger => (s.Window / (s.Rest + s.Telegraph + act + s.Window), s.Window),
            // Always open, but only where the pearl is now: about half her shots find it.
            Exposure.MovingWindow => (0.5f, float.PositiveInfinity),
            // The sacs first, then a lasting window: treat as open between its acts.
            _ => (s.Window / (s.Rest + s.Telegraph + act + s.Window), s.Window),
        };
    }

    /// <summary>The most pearls one of its attacks puts in the water.</summary>
    public static int PeakInFlight(BossSpec s)
    {
        float life = 4f * 0.72f;
        int Of(BossAttack a) => a switch
        {
            BossAttack.Ring => s.RingCount,
            BossAttack.Wall => s.WallCount,
            BossAttack.Helix => (int)(s.HelixArms * 5f * life),
            BossAttack.SacBurst => s.Sites.Count(x => x.Sac) * 6,
            BossAttack.Homing => 1,
            _ => 0,
        };
        var kit = s.Kit.Distinct().ToList();
        // A volley and, at most, the homing pearl alongside it.
        return kit.Max(Of) + (kit.Contains(BossAttack.Homing) ? 1 : 0);
    }

    public static string? Validate(BossSpec s)
    {
        // Winnable.
        if (s.Pearl.Out > 1f) return "the pearl is off the body";
        var (share, window) = Openness(s);
        if (window < WindowFloor(s.Menace)) return $"window {window:0.00} s under {WindowFloor(s.Menace):0.00} s";
        if (share < ShareFloor(s.Menace)) return $"open {share:P0} of the cycle, under {ShareFloor(s.Menace):P0}";
        if (s.Exposure == Exposure.CoaxOut && !s.HasSacs) return "nothing to coax the pearl out with";
        float sacs = s.Exposure == Exposure.CoaxOut ? s.Sites.Count(x => x.Sac) * s.SacHp : 0f;
        float length = (s.Hp + sacs) / (SteadyDps * share);
        if (length > LengthBudget(s.Menace)) return $"a {length:0} s fight (over {LengthBudget(s.Menace):0} s)";
        if (PeakInFlight(s) > InFlightBudget(s.Menace)) return $"{PeakInFlight(s)} pearls in flight (over {InFlightBudget(s.Menace)})";

        // Readable.
        if (s.Telegraph < TelegraphFloor(s.Menace) - 1e-4f) return "telegraph under the floor";
        foreach (var a in s.Kit)
        {
            if (!BossGenerator.Can(s.Form, a)) return $"{s.Form} cannot make a {a}";
            if (a == BossAttack.SacBurst && !s.HasSacs) return "a sac burst without sacs";
        }
        if (s.Kit.Count(a => a is not (BossAttack.Snap or BossAttack.Homing)) - s.Kit.Distinct().Count(a => a is not (BossAttack.Snap or BossAttack.Homing)) > 0)
            return "an attack twice in the kit";

        // Coherent: one dominant theme the body can grow, a minor one unlike it, a body in its size range.
        var themes = BossGenerator.ThemesOf(s.Form);
        if (!themes.Contains(s.Theme)) return $"{s.Form} cannot grow {s.Theme}";
        if (s.Minor is { } minor && (minor == s.Theme || !themes.Contains(minor))) return "a minor theme unlike the body";
        var (rMin, rMax) = BossGenerator.SizeOf(s.Form);
        if (!s.Reference && (s.Radius < rMin * 0.95f || s.Radius > rMax * 1.4f)) return "a body out of its size range";
        if (s.Sites.Count == 0) return "no mutation (the Read-at-Range test needs it)";

        // Theme: no villain in the name.
        if (!BossNames.Allowed(s.Name)) return $"the name '{s.Name}'";
        return null;
    }
}
