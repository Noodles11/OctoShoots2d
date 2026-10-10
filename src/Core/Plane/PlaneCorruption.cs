using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>What a world runs with. The game uses <see cref="Default"/>; tests may run the older rules.</summary>
public sealed class PlaneOptions
{
    /// <summary>The corruption war (docs/CORRUPTION.md): the field, Blightroots, murklings, gloomvines, valves, the arena.</summary>
    public bool Corruption = true;
    /// <summary>The pufferling enemies (retracted for now: none are placed and ambushes bud murklings instead).</summary>
    public bool Pufferlings = false;

    public static PlaneOptions Default { get; set; } = new();
}

/// <summary>The corruption's numbers (docs/CORRUPTION.md). Provisional, like the rest of plane combat.</summary>
public static class CorruptionTuning
{
    /// <summary>The field's cells: half a metre.</summary>
    public const float Cell = 0.5f;
    public const int Cells = (int)(LevelMap.Size / Cell);

    /// <summary>Her light cleanses the ground under her: this far, this fast (density per second at her centre).</summary>
    public const float HerRadius = 2.4f, HerRate = 3.2f;
    /// <summary>A popping bubble cleanses a burst round it, wider for a bigger (merged) bubble.</summary>
    public const float PopRadius = 1.4f, PopPerBubble = 0.22f, PopMax = 4.2f, PopStrength = 0.85f;
    /// <summary>A murkling's ember, burst over cleansed ground, blooms a patch of colour this wide.</summary>
    public const float EmberRadius = 2.6f;
    /// <summary>Start, shops and treasure caves are clean this far past their own radius (soft edge beyond).</summary>
    public const float SafeMargin = 3f, SafeFeather = 4f;
    /// <summary>A cell counts as cleansed below this density.</summary>
    public const float Clean = 0.15f;

    /// <summary>Tipping points: budding slows past half the level cleansed; regrowth stops past three quarters.</summary>
    public const float SlowBudding = 0.5f, StopRegrowth = 0.75f;
    /// <summary>The boss's strength falls with the level cleansed at the fight's start (up to this much).</summary>
    public const float BossEase = 0.3f;
    /// <summary>Every fifth of the arena cleansed strips one of the boss's crust layers.</summary>
    public const int CrustLayers = 5;
    /// <summary>The Crack opens when the boss is freed and this much of the arena is cleansed.</summary>
    public const float ArenaToOpen = 0.6f;
}

/// <summary>
/// The corruption field (docs/CORRUPTION.md): ink outgrowth over the level's floor, a density 0..1 per half-metre cell.
/// It never harms Clementine; her light cleanses it (and her bubbles where they pop). Blightroots regrow it inside their
/// reach. Safe places (start, shops, treasure caves) are clean from the start. The arena is the condensation zone: fully
/// corrupted. Totals are kept as it changes, so the level's cleanse share is always at hand.
/// </summary>
public sealed class CorruptionField
{
    public const int N = CorruptionTuning.Cells;
    /// <summary>Density per cell (row-major, x east, y south).</summary>
    public readonly float[] Density = new float[N * N];
    /// <summary>The density each cell started with (what a regrowing Blightroot restores).</summary>
    public readonly float[] Initial = new float[N * N];
    public float InitialTotal { get; private set; }
    public float Total { get; private set; }
    /// <summary>How much of the level's corruption is cleansed, 0..1.</summary>
    public float Cleansed => InitialTotal <= 0f ? 1f : Math.Clamp(1f - Total / InitialTotal, 0f, 1f);
    /// <summary>Bumped on every change, so a view knows when to upload.</summary>
    public int Version { get; private set; }

    /// <summary>The safe places of a level: the start, shops and treasure caves.</summary>
    public static IEnumerable<(Vector2 Centre, float Radius)> SafeOf(LevelMap map) =>
        map.Pois.Where(p => p.Kind is PoiKind.Start or PoiKind.Shop or PoiKind.TreasureCave).Select(p => (p.Position, p.Radius));

    /// <summary>A level's field as it starts, from its map alone (the world lays the same; a view previews the level below).</summary>
    public static CorruptionField ForMap(LevelMap map)
    {
        var field = new CorruptionField();
        field.Lay(map, SafeOf(map), map.HasBoss ? (map.Exit.Position, map.Exit.Radius) : null);
        return field;
    }

    public static int Index(int x, int y) => y * N + x;
    public static Vector2 CellCentre(int x, int y) => new((x + 0.5f) * CorruptionTuning.Cell, (y + 0.5f) * CorruptionTuning.Cell);

    public float At(Vector2 p)
    {
        int x = (int)(p.X / CorruptionTuning.Cell), y = (int)(p.Y / CorruptionTuning.Cell);
        return x < 0 || y < 0 || x >= N || y >= N ? 0f : Density[Index(x, y)];
    }

    /// <summary>Lays the field: open floor corrupted in patches (thicker toward the deeps), safe places clean, the arena full.</summary>
    public void Lay(LevelMap map, IEnumerable<(Vector2 Centre, float Radius)> safe, (Vector2 Centre, float Radius)? arena)
    {
        var safes = safe.ToList();
        float seed = (map.Seed % 997) * 0.37f;
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            var c = CellCentre(x, y);
            float d = 0f;
            if (map.IsOpen(c))
            {
                // Patchy: a slow noise lifts most of the floor to thick outgrowth, with thinner glades between.
                float n = Noise(c.X * 0.09f + seed, c.Y * 0.09f - seed) * 0.65f + Noise(c.X * 0.23f - seed, c.Y * 0.23f + seed * 0.5f) * 0.35f;
                d = Math.Clamp(0.45f + 0.75f * n, 0f, 1f);
                foreach (var (centre, radius) in safes)
                {
                    float r = Vector2.Distance(c, centre) - (radius + CorruptionTuning.SafeMargin);
                    if (r < CorruptionTuning.SafeFeather) d *= Math.Clamp(r / CorruptionTuning.SafeFeather, 0f, 1f);
                }
                if (arena is { } a && Vector2.Distance(c, a.Centre) <= a.Radius) d = 1f;
            }
            Density[Index(x, y)] = Initial[Index(x, y)] = d;
            InitialTotal += d;
        }
        Total = InitialTotal;
        Version++;
    }

    /// <summary>Lowers the density round a point (strength per call at the centre, easing to the rim). Returns the amount removed.</summary>
    public float Cleanse(Vector2 at, float radius, float strength)
    {
        float removed = 0f;
        Span(at, radius, out int x0, out int y0, out int x1, out int y1);
        float r2 = radius * radius;
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            float d2 = Vector2.DistanceSquared(CellCentre(x, y), at);
            if (d2 > r2) continue;
            int i = Index(x, y);
            float v = Density[i];
            if (v <= 0f) continue;
            float k = 1f - d2 / r2;
            float nv = MathF.Max(0f, v - strength * (0.35f + 0.65f * k));
            removed += v - nv;
            Density[i] = nv;
        }
        if (removed > 0f)
        {
            Total -= removed;
            Version++;
        }
        return removed;
    }

    /// <summary>Raises a cell toward a target by at most step (Blightroot regrowth, a pulse's stain).</summary>
    public void Raise(int i, float target, float step)
    {
        float v = Density[i];
        if (v >= target) return;
        float nv = MathF.Min(target, v + step);
        Density[i] = nv;
        Total += nv - v;
        Version++;
    }

    /// <summary>The cells inside a circle (for an area: a Blightroot's reach, the arena).</summary>
    public List<int> CellsWithin(Vector2 centre, float radius, float inner = 0f)
    {
        var list = new List<int>();
        Span(centre, radius, out int x0, out int y0, out int x1, out int y1);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            float d = Vector2.Distance(CellCentre(x, y), centre);
            if (d <= radius && d >= inner && Initial[Index(x, y)] > 0f) list.Add(Index(x, y));
        }
        return list;
    }

    /// <summary>Mean density over some cells (0 when there are none).</summary>
    public float Mean(List<int> cells)
    {
        if (cells.Count == 0) return 0f;
        float sum = 0f;
        foreach (int i in cells) sum += Density[i];
        return sum / cells.Count;
    }

    /// <summary>How much of some cells' starting corruption is cleansed, 0..1.</summary>
    public float CleansedOf(List<int> cells)
    {
        float now = 0f, start = 0f;
        foreach (int i in cells)
        {
            now += Density[i];
            start += Initial[i];
        }
        return start <= 0f ? 1f : Math.Clamp(1f - now / start, 0f, 1f);
    }

    static void Span(Vector2 at, float radius, out int x0, out int y0, out int x1, out int y1)
    {
        x0 = Math.Max(0, (int)((at.X - radius) / CorruptionTuning.Cell));
        y0 = Math.Max(0, (int)((at.Y - radius) / CorruptionTuning.Cell));
        x1 = Math.Min(N - 1, (int)((at.X + radius) / CorruptionTuning.Cell));
        y1 = Math.Min(N - 1, (int)((at.Y + radius) / CorruptionTuning.Cell));
    }

    /// <summary>Smooth value noise, 0..1 (deterministic).</summary>
    static float Noise(float x, float y)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
        return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
    }

    static float Hash(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777216f;
    }
}
