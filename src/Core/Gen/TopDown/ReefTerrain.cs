using System;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>
/// The reef generator prototype's terrain (reef_generator.html, "Phase-1 map core"), ported exactly: a seeded value-noise
/// fBm over the 150×150 cells, level 0 set at the (1 − 40%) quantile so at most 40% of cells stand above it, heights
/// scaled ×14 and clamped to −7…+14; and its editing brush, a cosine-falloff bump that moves a whole neighbourhood
/// smoothly. The same seed text gives the same heights as the prototype (JS 32-bit integer maths and doubles reproduced).
/// </summary>
public static class ReefTerrain
{
    /// <summary>The prototype's grid: cells 0…149 on each side.</summary>
    public const int Cells = 150;
    /// <summary>Hard cap: the share of cells above level 0.</summary>
    public const double MaxAbove = 0.40;
    public const float HMin = -7f, HMax = 14f;
    const double Frequency = 0.045;
    const double Scale = 14;

    /// <summary>FNV-1a over the seed text's UTF-16 code units (the prototype's strToSeed).</summary>
    public static uint StrToSeed(string s)
    {
        int x = unchecked((int)2166136261u);
        foreach (char c in s)
        {
            x ^= c;
            x = unchecked(x * 16777619);
        }
        return unchecked((uint)x);
    }

    static double Hash2(int ix, int iy, uint seed)
    {
        unchecked
        {
            int n = ix * 374761393 ^ iy * 668265263 ^ (int)seed * (int)2246822519u;
            n = (n ^ (int)((uint)n >> 13)) * 1274126177;
            return (uint)(n ^ (int)((uint)n >> 16)) / 4294967296.0;
        }
    }

    static double Smooth(double t) => t * t * (3 - 2 * t);

    static double VNoise(double x, double y, uint seed)
    {
        double fx0 = Math.Floor(x), fy0 = Math.Floor(y);
        int ix = (int)fx0, iy = (int)fy0;
        double fx = Smooth(x - fx0), fy = Smooth(y - fy0);
        double a = Hash2(ix, iy, seed), b = Hash2(ix + 1, iy, seed);
        double c = Hash2(ix, iy + 1, seed), d = Hash2(ix + 1, iy + 1, seed);
        return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
    }

    static double Fbm(double x, double y, uint seed)
    {
        double v = 0, amp = 0.5, f = 1, norm = 0;
        for (int o = 0; o < 4; o++)
        {
            v += amp * VNoise(x * f, y * f, unchecked(seed + (uint)(o * 101)));
            norm += amp;
            amp *= 0.5;
            f *= 2.03;
        }
        return v / norm;
    }

    /// <summary>
    /// Fills a (Cells+1)² grid of heights for the seed text: the prototype's map on cells 0…149 (the extra row and column
    /// continue the same noise). Level 0 sits at the quantile taken over the prototype's 150×150 cells.
    /// </summary>
    public static void Generate(float[] heights, int samples, string seedText)
    {
        uint seed = StrToSeed(seedText);
        var raw = new double[samples * samples];
        for (int y = 0; y < samples; y++)
        for (int x = 0; x < samples; x++)
            raw[y * samples + x] = Fbm(x * Frequency, y * Frequency, seed);

        var cells = new double[Cells * Cells];
        for (int y = 0; y < Cells; y++)
        for (int x = 0; x < Cells; x++)
            cells[y * Cells + x] = raw[y * samples + x];
        Array.Sort(cells);
        double level = cells[(int)Math.Floor((1 - MaxAbove) * (cells.Length - 1))];

        for (int i = 0; i < raw.Length; i++)
            heights[i] = (float)Math.Max(HMin, Math.Min(HMax, (raw[i] - level) * Scale));
    }

    /// <summary>The brush's cosine falloff: 1 at the centre, 0 at the radius and beyond.</summary>
    public static float Falloff(float d) => d >= 1f ? 0f : 0.5f * (1f + MathF.Cos(MathF.PI * MathF.Max(d, 0f)));
}
