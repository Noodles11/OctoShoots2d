using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>What a world runs with. The game uses <see cref="Default"/>; tests may run the older rules.</summary>
public sealed class PlaneOptions
{
    /// <summary>The corruption war (docs/CORRUPTION.md): the field, Blightroots, murklings, the arena.</summary>
    public bool Corruption = true;
    /// <summary>The pufferling enemies (retracted for now: none are placed and ambushes bud murklings instead).</summary>
    public bool Pufferlings = false;
    /// <summary>Sunken pots with loot (retracted with the corruption on: loot lies buried under the ink instead).</summary>
    public bool Vases = false;
    /// <summary>The boss to meet on a boss level (null: the boss generator's for its loop and depth). Tests and debug starts name one.</summary>
    public OctoShoots.Core.Bosses.BossSpec? Boss;
    /// <summary>
    /// The adventure (docs/ADVENTURE.md): vents in the walls pour ink dots into the water, the reef's fish turn as
    /// it reaches them, and the way down opens once every vent is sealed. The older creatures (Blightroots, Blotters,
    /// Lashjaws, Thumpsquirts, dens, curse dens, light springs, Queen Clam) and the ground's starting ink are off.
    /// </summary>
    public bool Adventure;

    public static PlaneOptions Default { get; set; } = new() { Adventure = true };
}

/// <summary>The corruption's numbers (docs/CORRUPTION.md). Provisional, like the rest of plane combat.</summary>
public static class CorruptionTuning
{
    /// <summary>The field's cells: half a metre.</summary>
    public const float Cell = 0.5f, CellArea = Cell * Cell;
    public const int Cells = (int)(LevelMap.Size / Cell);

    /// <summary>Her light cleanses the ground under her: this far, this fast (density per second at her centre).</summary>
    public const float HerRadius = 2.4f, HerRate = 3.2f;
    /// <summary>A popping bubble cleanses a burst round it, wider for a bigger (merged) bubble.</summary>
    public const float PopRadius = 1.4f, PopPerBubble = 0.22f, PopMax = 4.2f, PopStrength = 0.85f;
    /// <summary>A murkling's ember, burst over cleansed ground, blooms a patch of colour this wide.</summary>
    public const float EmberRadius = 2.6f;
    /// <summary>Start, shops and treasure caves are clean this far past their own radius (soft edge beyond).</summary>
    public const float SafeMargin = 3f, SafeFeather = 4f;
    /// <summary>
    /// At the start, this share of the open floor is corrupted (more with Menace, up to CoverMax): condensed toward the
    /// level's deep end (its boss arena, else its exit) and scattered in patches everywhere else.
    /// </summary>
    public const float Cover = 0.5f, CoverPerMenace = 0.1f, CoverMax = 0.85f;
    /// <summary>How strongly the corruption gathers toward the deep end (0: evenly patchy).</summary>
    public const float Condense = 0.18f;
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
/// corrupted. The ink is the matter the creatures born during the level are made of: a birth drinks its cost from the
/// ground (<see cref="Plan"/>, <see cref="Drink"/>) and the creature holds it (<see cref="Held"/>) until she frees it
/// (<see cref="Release"/>). Totals are kept as it changes, so the level's cleanse share is always at hand.
/// </summary>
public sealed class CorruptionField
{
    public const int N = CorruptionTuning.Cells;
    /// <summary>Density per cell (row-major, x east, y south).</summary>
    public readonly float[] Density = new float[N * N];
    /// <summary>The density each cell started with (what a regrowing Blightroot restores).</summary>
    public readonly float[] Initial = new float[N * N];
    public float InitialTotal { get; private set; }
    /// <summary>Per cell: 1 where corruption may spread (open floor), easing to 0 at safe places, which it never reaches.</summary>
    public readonly float[] Guard = new float[N * N];
    public float Total { get; private set; }
    /// <summary>The ink living creatures are made of (drunk from the ground, not yet freed), in the same units as Total.</summary>
    public float Held { get; private set; }
    /// <summary>Per cell, all the ink drunk from it so far (a view tells a creature's drinking from her cleansing).</summary>
    public readonly float[] Drunk = new float[N * N];
    /// <summary>How much of the level's corruption is cleansed, 0..1: what lies on the ground and what creatures hold both count.</summary>
    public float Cleansed => InitialTotal <= 0f ? 1f : Math.Clamp(1f - (Total + Held) / InitialTotal, 0f, 1f);
    /// <summary>Bumped on every change, so a view knows when to upload.</summary>
    public int Version { get; private set; }

    /// <summary>The safe places of a level: the start, shops, treasure caves and light springs.</summary>
    public static IEnumerable<(Vector2 Centre, float Radius)> SafeOf(LevelMap map) =>
        map.Pois.Where(p => p.Kind is PoiKind.Start or PoiKind.Shop or PoiKind.TreasureCave).Select(p => (p.Position, p.Radius))
            .Concat(LightSprings.Of(map).Select(at => (at, SpringTuning.Radius)));

    /// <summary>A level's field as it starts, from its map alone (the world lays the same; a view previews the level below).</summary>
    public static CorruptionField ForMap(LevelMap map)
    {
        var field = new CorruptionField();
        field.Lay(map, SafeOf(map), map.HasBoss && !map.Rift ? (map.Exit.Position, map.Exit.Radius) : null, Focus(map));
        return field;
    }

    /// <summary>Where a level's corruption gathers: its boss arena, or else its exit (the way deeper).</summary>
    public static Vector2 Focus(LevelMap map) => map.Exit.Position;

    public static int Index(int x, int y) => y * N + x;
    public static Vector2 CellCentre(int x, int y) => new((x + 0.5f) * CorruptionTuning.Cell, (y + 0.5f) * CorruptionTuning.Cell);

    public float At(Vector2 p)
    {
        int x = (int)(p.X / CorruptionTuning.Cell), y = (int)(p.Y / CorruptionTuning.Cell);
        return x < 0 || y < 0 || x >= N || y >= N ? 0f : Density[Index(x, y)];
    }

    /// <summary>
    /// Lays the field: a share of the open floor (Cover, more with Menace) corrupted, gathered toward the focus (the
    /// deep end) and scattered in patches elsewhere; safe places clean; the arena full.
    /// </summary>
    public void Lay(LevelMap map, IEnumerable<(Vector2 Centre, float Radius)> safe, (Vector2 Centre, float Radius)? arena, Vector2? focus = null)
    {
        var safes = safe.ToList();
        float seed = (map.Seed % 997) * 0.37f;
        // (The level's template may lay more: its ink rule.)
        float cover = MathF.Min(CorruptionTuning.Cover + CorruptionTuning.CoverPerMenace * MathF.Max(map.Menace, 0f) + (map.Template?.Rules.Ink ?? 0f), CorruptionTuning.CoverMax);
        var score = new float[N * N];
        var open = new List<float>(N * N / 2);
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            var c = CellCentre(x, y);
            int i = Index(x, y);
            score[i] = float.NegativeInfinity;
            if (!map.IsOpen(c)) continue;
            float guard = 1f;
            foreach (var (centre, radius) in safes)
            {
                float r = Vector2.Distance(c, centre) - (radius + CorruptionTuning.SafeMargin);
                if (r < CorruptionTuning.SafeFeather) guard *= Math.Clamp(r / CorruptionTuning.SafeFeather, 0f, 1f);
            }
            Guard[i] = guard;
            // Patchy noise, lifted toward the focus: thick there, scattered patches and glades elsewhere.
            float n = Noise(c.X * 0.09f + seed, c.Y * 0.09f - seed) * 0.65f + Noise(c.X * 0.23f - seed, c.Y * 0.23f + seed * 0.5f) * 0.35f;
            float near = focus is { } f ? 1f - Math.Clamp(Vector2.Distance(c, f) / (LevelMap.Size * 0.75f), 0f, 1f) : 0.5f;
            score[i] = n * (1f - CorruptionTuning.Condense) + near * CorruptionTuning.Condense;
            if (guard > 0f) open.Add(score[i]);
        }
        // The threshold that corrupts the wanted share of the open floor.
        open.Sort();
        float cut = open.Count == 0 ? 1f : open[Math.Clamp((int)((1f - cover) * open.Count), 0, open.Count - 1)];
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            int i = Index(x, y);
            float d = 0f;
            if (!float.IsNegativeInfinity(score[i]))
            {
                // Just past the edge it is thin; well inside, thick.
                if (score[i] >= cut) d = Math.Clamp(0.5f + (score[i] - cut) * 5f, 0.5f, 1f) * Guard[i];
                if (arena is { } a && Vector2.Distance(CellCentre(x, y), a.Centre) <= a.Radius) d = 1f;
            }
            Density[i] = Initial[i] = d;
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

    /// <summary>
    /// A birth's draught: the ink (density·m²) a creature costs, from anywhere on the level — the nearest corrupted cells
    /// first, then further and further out until it has it. Null when the whole level holds less than that.
    /// </summary>
    public InkDraw? Plan(Vector2 at, float cost)
    {
        float want = cost / CorruptionTuning.CellArea;
        var draw = new InkDraw();
        float got = 0f, weighted = 0f;
        var ring = new List<(float D, int I)>();
        // Outward in rings (2 m at a time), each taken nearest first.
        float maxReach = LevelMap.Size * 1.5f;
        for (float inner = -1f, outer = 2f; got < want - 1e-4f && inner < maxReach; inner = outer, outer += 2f)
        {
            ring.Clear();
            Span(at, outer, out int x0, out int y0, out int x1, out int y1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float d = Vector2.Distance(CellCentre(x, y), at);
                int i = Index(x, y);
                if (d > inner && d <= outer && Density[i] > 0.02f) ring.Add((d, i));
            }
            ring.Sort((l, r) => l.D != r.D ? l.D.CompareTo(r.D) : l.I.CompareTo(r.I));
            foreach (var (d, i) in ring)
            {
                float take = MathF.Min(Density[i], want - got);
                draw.Cells.Add(i);
                draw.Amounts.Add(take);
                draw.Taken.Add(0f);
                got += take;
                weighted += Density[i];
                draw.Radius = d + CorruptionTuning.Cell * 0.5f;
                if (got >= want - 1e-4f) break;
            }
        }
        if (got < want - 1e-4f) return null;
        draw.Total = got;
        draw.Ink = weighted / draw.Cells.Count;
        return draw;
    }

    /// <summary>Drinks a share of a draught (0..1 of each cell's part) into the creature: off the ground, held by it. Returns the ink taken.</summary>
    public float Drink(InkDraw draw, float share)
    {
        float taken = 0f;
        for (int k = 0; k < draw.Cells.Count; k++)
        {
            int i = draw.Cells[k];
            float t = MathF.Min(Density[i], MathF.Min(draw.Amounts[k] * share, draw.Amounts[k] - draw.Taken[k]));
            if (t <= 0f) continue;
            Density[i] -= t;
            Drunk[i] += t;
            draw.Taken[k] += t;
            taken += t;
        }
        if (taken > 0f)
        {
            Total -= taken;
            Held += taken;
            Version++;
        }
        return taken;
    }

    /// <summary>A creature made of ink is freed: the ink it held is cleansed.</summary>
    public void Release(float matter)
    {
        if (matter <= 0f) return;
        Held = MathF.Max(0f, Held - matter);
        Version++;
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

    /// <summary>
    /// Corruption spreading (a Blotter's trail): raises the open floor round a point toward target by step a call at its
    /// centre, never into safe places. What it spreads counts as corruption to cleanse (the level's total grows) and is
    /// what a Blightroot regrows. Returns how much it spread.
    /// </summary>
    public float Spread(Vector2 at, float radius, float target, float step)
    {
        float added = 0f;
        Span(at, radius, out int x0, out int y0, out int x1, out int y1);
        float r2 = radius * radius;
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            float d2 = Vector2.DistanceSquared(CellCentre(x, y), at);
            if (d2 > r2) continue;
            int i = Index(x, y);
            float cap = target * Guard[i];
            float v = Density[i];
            if (v >= cap) continue;
            float nv = MathF.Min(cap, v + step * (0.4f + 0.6f * (1f - d2 / r2)));
            Density[i] = nv;
            added += nv - v;
            if (nv > Initial[i])
            {
                InitialTotal += nv - Initial[i];
                Initial[i] = nv;
            }
        }
        if (added > 0f)
        {
            Total += added;
            Version++;
        }
        return added;
    }

    /// <summary>Wipes some cells wholly clean (the boss's pearl bursting over her arena); it stays clean (nothing to regrow).</summary>
    public void Clear(List<int> cells)
    {
        float removed = 0f;
        foreach (int i in cells)
        {
            removed += Density[i];
            Density[i] = 0f;
        }
        if (removed <= 0f) return;
        Total -= removed;
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

/// <summary>
/// A creature's draught of ink (CorruptionField.Plan): the cells it drinks from, nearest first, and how much of each;
/// how far out it had to reach (Radius, its farthest cell) and how thick the ink was (Ink, mean density 0..1).
/// </summary>
public sealed class InkDraw
{
    public readonly List<int> Cells = new();
    public readonly List<float> Amounts = new();
    /// <summary>What has been drunk from each cell so far.</summary>
    public readonly List<float> Taken = new();
    public float Total, Radius, Ink;
}
