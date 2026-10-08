using System;
using OctoShoots.Core.Creatures;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>
/// 2D gradient (Perlin) noise and its fractal sums, for terrain that reads as flowing ridges and valleys: value noise
/// leaves axis-aligned blocks in contour lines, gradient noise does not. Pure float math, so deterministic per seed.
/// </summary>
public static class TerrainNoise
{
    /// <summary>Gradient noise in about [-1, 1].</summary>
    public static float Perlin(float x, float y, uint seed)
    {
        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
        float fx = x - x0, fy = y - y0;
        float u = Fade(fx), v = Fade(fy);
        float n00 = Grad(x0, y0, fx, fy, seed), n10 = Grad(x0 + 1, y0, fx - 1f, fy, seed);
        float n01 = Grad(x0, y0 + 1, fx, fy - 1f, seed), n11 = Grad(x0 + 1, y0 + 1, fx - 1f, fy - 1f, seed);
        return MathUtil.Lerp(MathUtil.Lerp(n00, n10, u), MathUtil.Lerp(n01, n11, u), v) * 1.41f;
    }

    /// <summary>Fractal sum of <paramref name="octaves"/> octaves (lacunarity 2, gain 0.5), normalised to about [-1, 1].</summary>
    public static float Fbm(float x, float y, uint seed, int octaves)
    {
        float sum = 0f, amp = 1f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += amp * Perlin(x, y, seed + (uint)i * 1013u);
            norm += amp;
            amp *= 0.5f;
            x = x * 2f + 17.3f;
            y = y * 2f - 9.1f;
        }
        return sum / norm;
    }

    static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    static float Grad(int ix, int iy, float dx, float dy, uint seed)
    {
        uint h = unchecked((uint)ix * 0x8da6b343u ^ (uint)iy * 0xd8163841u ^ seed * 0x9E3779B9u);
        h ^= h >> 13;
        h *= 0x5bd1e995u;
        h ^= h >> 15;
        float a = (h & 4095u) / 4096f * MathF.Tau;
        return MathF.Cos(a) * dx + MathF.Sin(a) * dy;
    }
}
