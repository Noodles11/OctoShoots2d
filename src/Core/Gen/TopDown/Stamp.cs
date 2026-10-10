using System;
using System.Collections.Generic;
using System.Numerics;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>
/// The rock around a hole (DESIGN-TOPDOWN §4.6): a small patch of terrain generated on its own from its seed and pinned
/// into two levels — around the hole on the level above and around the start on the level below — at the same height
/// above each level's swim plane. Diving down the hole, she arrives among the same cliffs that ringed it, one level
/// lower. A clearing in the middle (the hole, or the start) with canyons leaving it in two or three directions; the
/// level's routes run out through them. An arena stamp (the boss level's Crack) is the arena's level floor.
/// Heights are kept exactly within <see cref="Pin"/> metres of the centre and blend into the level by <see cref="Blend"/>.
/// </summary>
public sealed class Stamp
{
    public const float Pin = 14f;
    public const float Blend = 20f;
    /// <summary>The clearing in the middle (an arena stamp is open all the way out).</summary>
    public const float ClearingRadius = 8f;
    /// <summary>Half the width of a canyon leaving the clearing: wider than any route, so a route fits inside it.</summary>
    public const float OpeningHalfWidth = 6f;
    /// <summary>The patch runs a little past the blend so bilinear sampling at its edge stays inside.</summary>
    const int Half = (int)Blend + 2;
    const int Samples = 2 * Half + 1;
    const float WallSlope = 3.5f;

    readonly float[] _heights = new float[Samples * Samples];

    public bool Arena { get; private init; }
    /// <summary>Directions (radians, map space) of the canyons leaving the clearing; none for an arena.</summary>
    public IReadOnlyList<float> Openings { get; private init; } = Array.Empty<float>();

    public static Stamp Generate(ulong seed, bool arena)
    {
        var rng = new Rng(seed);
        var openings = new List<float>();
        if (!arena)
        {
            int count = 2 + (rng.NextFloat() < 0.5f ? 1 : 0);
            for (int tries = 0; tries < 200 && openings.Count < count; tries++)
            {
                float a = rng.Range(0f, MathF.Tau);
                bool apart = true;
                foreach (float o in openings)
                    if (MathF.Abs(AngleBetween(a, o)) < 1.4f) apart = false;
                if (apart) openings.Add(a);
            }
        }
        var stamp = new Stamp { Arena = arena, Openings = openings };
        uint noise = (uint)(rng.NextU64() >> 32);
        for (int j = 0; j < Samples; j++)
        for (int i = 0; i < Samples; i++)
        {
            var p = new Vector2(i - Half, j - Half);
            float dip = 0.5f + 0.5f * TerrainNoise.Fbm((p.X + 400f) / 16f, (p.Y + 400f) / 16f, noise, 2);
            float floor = -1f - 0.6f * dip;
            float h;
            if (arena) h = -1f;
            else
            {
                float open = p.Length() - ClearingRadius;
                foreach (float a in openings)
                {
                    var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
                    open = MathF.Min(open, Geo.SegmentDistance(p, Vector2.Zero, dir * (Half + 4f)) - OpeningHalfWidth);
                }
                // The same brush as the level's walls: up from the floor over a few metres to a rolling top.
                float top = 6f + 7f * (0.5f + 0.5f * TerrainNoise.Fbm((p.X + 900f) / 14f, (p.Y + 900f) / 14f, noise + 7u, 3));
                float rise = 1f - ReefTerrain.Falloff(MathF.Max(open - 1.5f, 0f) / WallSlope);
                h = floor + rise * (top - floor);
            }
            stamp._heights[j * Samples + i] = h;
        }
        return stamp;
    }

    /// <summary>The stamp's height at an offset from its centre (bilinear; clamped to the patch).</summary>
    public float HeightAt(Vector2 offset)
    {
        float x = Math.Clamp(offset.X + Half, 0f, Samples - 1.001f), y = Math.Clamp(offset.Y + Half, 0f, Samples - 1.001f);
        int ix = (int)x, iy = (int)y;
        float tx = x - ix, ty = y - iy;
        float a = MathUtil.Lerp(_heights[iy * Samples + ix], _heights[iy * Samples + ix + 1], tx);
        float b = MathUtil.Lerp(_heights[(iy + 1) * Samples + ix], _heights[(iy + 1) * Samples + ix + 1], tx);
        return MathUtil.Lerp(a, b, ty);
    }

    /// <summary>How much of the stamp (1) rather than the level (0) a point this far from its centre takes.</summary>
    public static float Weight(float distance) => 1f - MathUtil.Smoothstep(MathUtil.Clamp01((distance - Pin) / (Blend - Pin)));

    /// <summary>The opening that points closest to a direction, and how far off it is (radians, 0…π).</summary>
    public (float Angle, float Off) Toward(Vector2 direction)
    {
        float want = MathF.Atan2(direction.Y, direction.X), best = 0f, off = MathF.PI;
        foreach (float a in Openings)
        {
            float d = MathF.Abs(AngleBetween(a, want));
            if (d < off)
            {
                off = d;
                best = a;
            }
        }
        return (best, off);
    }

    public static float AngleBetween(float a, float b)
    {
        float d = (a - b) % MathF.Tau;
        if (d > MathF.PI) d -= MathF.Tau;
        if (d < -MathF.PI) d += MathF.Tau;
        return d;
    }
}
