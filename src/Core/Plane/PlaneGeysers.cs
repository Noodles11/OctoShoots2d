using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Bubble geysers (DESIGN-TOPDOWN §4.7): small vents in the seabed that, on the reef director's schedule, fizz for a
/// moment and then throw a spray of bubbles up to the surface for 5–10 s. While one sprays, the water round it is
/// pushed outward like a wave — hardest at the burst, steady after — moving her, the creatures and her shots alike.
/// </summary>
public static class GeyserTuning
{
    /// <summary>Vents a level has (both inclusive).</summary>
    public const int CountMin = 2, CountMax = 4;
    /// <summary>Where they lie: open floor this wide round them, this far from the start, the way down, the places and each other.</summary>
    public const float Clearance = 1.6f, FromStart = 18f, FromExit = 10f, FromPlace = 5f, Apart = 14f;
    /// <summary>The first spray comes this long into the level; each next one this long after the vent's last.</summary>
    public const float FirstMin = 6f, FirstMax = 25f, GapMin = 14f, GapMax = 35f;
    /// <summary>The fizz before a spray (bubbles, no push), and the spray itself.</summary>
    public const float Warn = 1.5f, SprayMin = 5f, SprayMax = 10f;
    /// <summary>The push reaches this far; it eases in over Rise and out over Fall.</summary>
    public const float Reach = 5f, Rise = 0.3f, Fall = 0.8f;
    /// <summary>The push outward (m/s) at the vent: steady, plus a burst at the start that dies away at BurstDecay a second.</summary>
    public const float Push = 4f, Burst = 10f, BurstDecay = 2.2f;
}

/// <summary>Where a level's geysers are: worked out from the map alone (the director schedules them, the view draws them).</summary>
public static class Geysers
{
    public static List<Vector2> Of(LevelMap map)
    {
        var rng = new Rng(map.Seed ^ 0x6E95E5UL ^ ((ulong)map.Level << 18) ^ ((ulong)map.Depth << 26) ^ ((ulong)map.Cycle << 34) ^ ((ulong)map.Attempt << 50));
        int count = GeyserTuning.CountMin + rng.Int(GeyserTuning.CountMax - GeyserTuning.CountMin + 1);
        var candidates = map.Plazas.Select(p => p.Center).ToList();
        foreach (var c in map.Corridors.Where(c => c.Kind is CorridorKind.Main or CorridorKind.Side or CorridorKind.Spur))
            for (int i = 1; i < c.Points.Count; i += 3) candidates.Add(c.Points[i]);
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = rng.Int(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }
        var places = map.Pois.Where(p => p.Kind is not (PoiKind.Start or PoiKind.Exit)).ToList();
        var springs = LightSprings.Of(map);
        var found = new List<Vector2>();
        foreach (var at in candidates)
        {
            if (found.Count >= count) break;
            if (Vector2.Distance(at, map.Start.Position) < GeyserTuning.FromStart) continue;
            if (Vector2.Distance(at, map.Exit.Position) < map.Exit.Radius + GeyserTuning.FromExit) continue;
            if (map.Shaft.Contains(at, 4f) || places.Any(p => Vector2.Distance(at, p.Position) < p.Radius + GeyserTuning.FromPlace)) continue;
            if (springs.Any(s => Vector2.Distance(at, s) < SpringTuning.Radius + 4f)) continue;
            if (found.Any(f => Vector2.Distance(at, f) < GeyserTuning.Apart)) continue;
            bool open = map.IsOpen(at);
            for (int k = 0; k < 8 && open; k++)
            {
                float a = k * MathF.Tau / 8f;
                open = map.IsOpen(at + new Vector2(MathF.Cos(a), MathF.Sin(a)) * GeyserTuning.Clearance);
            }
            if (open) found.Add(at);
        }
        return found;
    }

    /// <summary>A spray's push over its life, 0 → 1 → 0 (nothing during the fizz before it), and how far into the spray it is.</summary>
    public static float Envelope(ReefEvent e, float time, out float sprayTime)
    {
        float t = time - e.Start - GeyserTuning.Warn;
        sprayTime = t;
        float spray = e.Duration - GeyserTuning.Warn;
        if (t <= 0f || t >= spray) return 0f;
        return MathUtil.Smoothstep(MathUtil.Clamp01(t / GeyserTuning.Rise)) * MathUtil.Smoothstep(MathUtil.Clamp01((spray - t) / GeyserTuning.Fall));
    }

    /// <summary>The geyser's push at p (m/s): outward from the vent, full at it and gone at Reach; the burst at the start.</summary>
    public static Vector2 PushAt(Vector2 vent, ReefEvent e, Vector2 p, float time)
    {
        float k = Envelope(e, time, out float t);
        if (k <= 0f) return Vector2.Zero;
        var off = p - vent;
        float d = off.Length();
        if (d >= GeyserTuning.Reach) return Vector2.Zero;
        var dir = d > 1e-3f ? off / d : Vector2.UnitY;
        float falloff = 1f - MathUtil.Smoothstep(d / GeyserTuning.Reach);
        return dir * (GeyserTuning.Push + GeyserTuning.Burst * MathF.Exp(-t * GeyserTuning.BurstDecay)) * k * falloff;
    }
}
