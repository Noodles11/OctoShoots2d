using System;
using System.Numerics;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>
/// The reef walls' strata (DESIGN-TOPDOWN §4.1 stage 2), rolled once per level: the walls rise in 2–4 layers, each
/// boundary a ledge height that drifts with a slow noise, and the low-frequency warp that makes the wall outlines
/// wander like real reef edges. The lowest stratum is a cliff (50–60°); each one above leans back further. Shared by
/// the walls' raising (<c>RaiseCaveWalls</c>), their slopes (<c>SoftenSlopes</c>) and what grows on the ledges.
/// </summary>
public sealed class WallStrata
{
    /// <summary>The soft knee at every boundary: the step from one layer to the next spreads over ±Knee metres of height.</summary>
    public const float Knee = 0.4f;
    /// <summary>The lowest stratum's slope range (tan 50°…60°), up per metre in from the water's edge.</summary>
    public const float CliffMin = 1.19f, CliffMax = 1.73f;
    /// <summary>How much rise each ledge takes out (metres of height), by how many layers: the bench is that rise laid flat.</summary>
    static readonly float[] Bench = { 0f, 0f, 2.4f, 1.8f, 1.4f };
    /// <summary>How far (± metres of height) a stratum's shifted test may move its edge.</summary>
    const float Wander = 0.8f;
    /// <summary>How far a boundary drifts (±metres of height) and over what span (metres).</summary>
    const float Drift = 0.9f, DriftSpan = 18f;
    /// <summary>The warp's span (metres): it bends the wall tops over tens of metres.</summary>
    const float WarpSpan = 26f;
    /// <summary>Each stratum above the first tests the rock this share of the warp further along, so every edge wanders its own way.</summary>
    const float Offset = 0.3f;

    /// <summary>How many layers the walls rise in (2–4).</summary>
    public readonly int Layers;
    /// <summary>How far the warp moves the wall tops' noise (3–6 m).</summary>
    public readonly float WarpAmplitude;
    /// <summary>The share of the walls left as sheer cliffs (0.08–0.25, rolled per level).</summary>
    public readonly float CliffShare;
    /// <summary>Each stratum above the first leans back by this factor of the one below (0.55–0.75).</summary>
    public readonly float Lean;
    readonly float[] _boundaries;
    readonly uint _seed;

    public WallStrata(Rng rng)
    {
        Layers = 2 + rng.Int(3);
        WarpAmplitude = rng.Range(3f, 6f);
        CliffShare = rng.Range(0.08f, 0.25f);
        Lean = rng.Range(0.55f, 0.75f);
        _seed = (uint)(rng.NextU64() >> 32);
        // The first ledge 2.6–4.2 m up, the rest spread evenly to 9–11 m.
        _boundaries = new float[Layers - 1];
        float first = rng.Range(2.6f, 4.2f), last = rng.Range(9f, 11f);
        for (int j = 0; j < _boundaries.Length; j++)
            _boundaries[j] = _boundaries.Length == 1 ? first : first + j * (last - first) / (_boundaries.Length - 1);
    }

    /// <summary>The boundaries between layers (one fewer than the layers).</summary>
    public int Boundaries => _boundaries.Length;

    /// <summary>The height of boundary j (0 the lowest ledge) at a point, drifting.</summary>
    public float Boundary(int j, float x, float y) =>
        _boundaries[j] + Drift * TerrainNoise.Fbm(x / DriftSpan + 13f * j, y / DriftSpan - 7f * j, _seed + 11u + (uint)j, 2);

    /// <summary>The low-frequency warp at a point (metres).</summary>
    public Vector2 Warp(float x, float y) => new(
        WarpAmplitude * TerrainNoise.Fbm(x / WarpSpan + 3.1f, y / WarpSpan - 8.7f, _seed + 1u, 3),
        WarpAmplitude * TerrainNoise.Fbm(x / WarpSpan - 41f, y / WarpSpan + 23f, _seed + 2u, 3));

    /// <summary>Where stratum j's edge tests the rock from a point: its own share of the warp along.</summary>
    public Vector2 Shift(int j, float x, float y) => Warp(x, y) * (Offset * j);

    /// <summary>The rise each ledge lays flat (metres of height).</summary>
    public float BenchRise => Bench[Layers];

    /// <summary>
    /// The terraced height of rock whose raw profile is <paramref name="h"/> at (x, y). At every boundary the rock
    /// pauses: the next <see cref="BenchRise"/> metres of its rise are laid flat into a ledge, then it climbs on, each
    /// corner eased over the knee. On a wall's face that is a bench between two cliffs; over rolling tops, a wide
    /// plateau at the ledge's height. Each stratum above the first finds its edge by testing the rock at its own shift
    /// of the warp (moving it by at most ±0.8 m of height), so every stratum's edge is differently irregular.
    /// </summary>
    public float Terrace(float h, float x, float y, Func<float, float, float> rawAt)
    {
        float g = h, bench = BenchRise;
        for (int j = 0; j < Boundaries; j++)
        {
            float c = Boundary(j, x, y);
            float e = h;
            if (j > 0)
            {
                var s = Shift(j, x, y);
                e += Math.Clamp(rawAt(x + s.X, y + s.Y) - h, -Wander, Wander);
            }
            g -= SmoothMax(e - c, Knee) - SmoothMax(e - c - bench, Knee);
        }
        return g;
    }

    /// <summary>
    /// The slope cap at a distance in from the water's edge: up the lowest stratum at its cliff slope (between
    /// <see cref="CliffMin"/> and <see cref="CliffMax"/> by <paramref name="steep"/>, 0–1) to the first ledge, then each
    /// layer above at <see cref="Lean"/> times the one below.
    /// </summary>
    public float SlopeCap(float distance, float steep, float x, float y)
    {
        float slope = CliffMin + (CliffMax - CliffMin) * Math.Clamp(steep, 0f, 1f);
        float at = 0f, left = distance;
        for (int j = 0; ; j++)
        {
            float top = j < Boundaries ? Boundary(j, x, y) : float.MaxValue;
            float need = (top - at) / slope;
            if (left <= need) return at + slope * left;
            left -= need;
            at = top;
            slope *= Lean;
        }
    }

    /// <summary>The boundary nearest a height at a point (a ledge's height), or -1 with none within reach.</summary>
    public float NearestLedge(float height, float x, float y, float reach)
    {
        float best = -1f, bestD = reach;
        for (int j = 0; j < Boundaries; j++)
        {
            float c = Boundary(j, x, y);
            if (MathF.Abs(c - height) <= bestD)
            {
                bestD = MathF.Abs(c - height);
                best = c;
            }
        }
        return best;
    }

    /// <summary>max(a, 0), rounded over k (a polynomial smooth maximum).</summary>
    static float SmoothMax(float a, float k)
    {
        float d = MathF.Max(k - MathF.Abs(a), 0f);
        return MathF.Max(a, 0f) + d * d / (4f * k);
    }
}
