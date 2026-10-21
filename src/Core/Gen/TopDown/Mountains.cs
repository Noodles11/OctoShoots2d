using System;
using System.Collections.Generic;
using System.Numerics;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>
/// Fills the level's unreachable water with mountains (DESIGN-TOPDOWN §4.1): open floor she can never swim to — pockets
/// sealed off by rock, or behind gaps too narrow for her — is raised into rock that meets the walls round it at their
/// own height and rises toward ridged peaks in its middle, so the reef reads as one range of mountains rather than walls
/// with flat holes between them. It runs on a finished, validated level, so it never changes what she can reach.
/// </summary>
public static class Mountains
{
    /// <summary>Open floor within this many metres of water she can reach stays open (the walls' own foot).</summary>
    public const int Keep = 2;
    /// <summary>The peaks rise this much per metre in from the nearest open water, up to Peak above the surrounding rock.</summary>
    public const float Rise = 0.9f, Peak = 7f;
    /// <summary>Ridged noise over the filled rock, this high at most.</summary>
    public const float Ridges = 2.2f;

    /// <summary>Diagnostics: off, levels keep their unreachable water (to compare before and after).</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>Raises every unreachable stretch of open floor into mountains. Returns how many grid points it raised.</summary>
    public static int Fill(LevelMap map)
    {
        if (!Enabled) return 0;
        const int N = LevelMap.Samples;
        int cells = LevelMap.Size;
        // Where she can swim, at her own size (weak rock counts as open: secret rooms are hers to find).
        var dist = LevelValidator.Distances(map, map.Start.Position);
        var reach = new bool[N * N];
        for (int y = 0; y < cells; y++)
        for (int x = 0; x < cells; x++)
        {
            if (!float.IsFinite(dist[y * cells + x])) continue;
            // A cell's four corners are grid points she swims over.
            reach[y * N + x] = reach[y * N + x + 1] = reach[(y + 1) * N + x] = reach[(y + 1) * N + x + 1] = true;
        }
        // Keep open floor near reachable water (the foot of every wall she swims past), and the shaft.
        var keep = new bool[N * N];
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            if (!reach[y * N + x]) continue;
            for (int dy = -Keep; dy <= Keep; dy++)
            for (int dx = -Keep; dx <= Keep; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < N && ny < N) keep[ny * N + nx] = true;
            }
        }
        var fill = new bool[N * N];
        int filled = 0;
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            int i = y * N + x;
            if (keep[i] || map.Heights[i] > LevelMap.BlockHeight) continue;
            if (map.Shaft.Contains(new Vector2(x, y), 2f)) continue;
            fill[i] = true;
            filled++;
        }
        if (filled == 0) return 0;

        // How far in from open water each filled point lies (its peaks rise with it).
        var inward = new float[N * N];
        var queue = new Queue<int>();
        for (int i = 0; i < N * N; i++)
        {
            inward[i] = fill[i] ? float.PositiveInfinity : 0f;
            if (!fill[i]) queue.Enqueue(i);
        }
        while (queue.Count > 0)
        {
            int i = queue.Dequeue(), x = i % N, y = i / N;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                if (nx < 0 || ny < 0 || nx >= N || ny >= N) continue;
                int j = ny * N + nx;
                if (inward[j] <= inward[i] + 1f) continue;
                inward[j] = inward[i] + 1f;
                queue.Enqueue(j);
            }
        }

        // The base: the surrounding rock's height carried smoothly over the pocket (open water's edge just above the
        // swim level), so the fill meets every wall at its own height.
        var h = (float[])map.Heights.Clone();
        for (int i = 0; i < N * N; i++)
            if (fill[i]) h[i] = 2f;
        for (int pass = 0; pass < 60; pass++)
        for (int y = 1; y < N - 1; y++)
        for (int x = 1; x < N - 1; x++)
        {
            int i = y * N + x;
            if (!fill[i]) continue;
            float sum = 0f;
            for (int k = 0; k < 4; k++)
            {
                int j = k switch { 0 => i + 1, 1 => i - 1, 2 => i + N, _ => i - N };
                // Open water round the pocket counts as rock just above the swim level: the slope starts at her plane.
                sum += fill[j] ? h[j] : MathF.Max(h[j], LevelMap.SwimBand + 0.6f);
            }
            h[i] = sum * 0.25f;
        }

        // The peaks: rising with the distance in, ridged, so the filled rock is a range, not a plateau.
        var rng = new Rng(map.Seed ^ 0x40C7A1A5UL ^ ((ulong)map.Level << 20) ^ ((ulong)map.Depth << 28));
        float ox = rng.Range(0f, 100f), oy = rng.Range(0f, 100f);
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            int i = y * N + x;
            if (!fill[i]) continue;
            float d = inward[i];
            float rise = MathF.Min(Rise * d, Peak);
            float ridge = Ridged((x + ox) * 0.09f, (y + oy) * 0.09f) * Ridges * MathF.Min(1f, d / 3f);
            map.Heights[i] = Math.Clamp(MathF.Max(h[i], LevelMap.SwimBand + 0.6f) + rise + ridge, LevelMap.BlockHeight + 0.4f, ReefTerrain.HMax);
            // Rock where there was water: no roof hangs over it.
            map.Lid[i] = float.NaN;
            map.Roof[i] = float.NaN;
        }
        return filled;
    }

    /// <summary>Ridged value noise, 0..1 (sharp crests, soft valleys), two octaves.</summary>
    static float Ridged(float x, float y)
    {
        float a = 1f - MathF.Abs(2f * Value(x, y) - 1f);
        float b = 1f - MathF.Abs(2f * Value(x * 2.1f + 7f, y * 2.1f + 3f) - 1f);
        return a * a * 0.7f + b * b * 0.3f;
    }

    static float Value(float x, float y)
    {
        int ix = (int)MathF.Floor(x), iy = (int)MathF.Floor(y);
        float fx = x - ix, fy = y - iy;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float H(int a, int b)
        {
            uint n = (uint)(a * 374761393 + b * 668265263);
            n = (n ^ (n >> 13)) * 1274126177u;
            return ((n ^ (n >> 16)) & 0xFFFFFF) / 16777215f;
        }
        float top = H(ix, iy) + (H(ix + 1, iy) - H(ix, iy)) * fx;
        float bottom = H(ix, iy + 1) + (H(ix + 1, iy + 1) - H(ix, iy + 1)) * fx;
        return top + (bottom - top) * fy;
    }
}
