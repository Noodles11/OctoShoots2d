using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>
/// The rules a generated level must keep (DESIGN-TOPDOWN §4.1, §4.5), plus the swim-band flood fill they are
/// checked with. A level that breaks any of them is regenerated from the next attempt's sub-streams.
/// </summary>
public static class LevelValidator
{
    public const float MinExitGeodesic = 90f;
    /// <summary>The longest spur from the network to a place.</summary>
    public const float MaxSpur = 24f;

    /// <summary>Null when the level keeps every rule; otherwise the first one it breaks.</summary>
    public static string? Validate(LevelMap map)
    {
        int Count(PoiKind k) => map.Pois.Count(p => p.Kind == k);
        if (Count(PoiKind.Start) != 1 || Count(PoiKind.Exit) != 1) return "missing start or exit";
        if (Count(PoiKind.TreasureCave) > 2 || Count(PoiKind.Shop) > 1 || Count(PoiKind.Secret) > 1 || Count(PoiKind.CurseDen) > 1
            || Count(PoiKind.ShellCache) > 1 || Count(PoiKind.Ambush) > LevelStocking.MaxAmbushes) return "optional POI counts out of range";

        // Scatter rules.
        var start = map.Start;
        var exit = map.Exit;
        var others = map.Pois.Where(p => p.Kind is not (PoiKind.Start or PoiKind.Exit)).ToList();
        if (others.Count + (map.HasBoss ? 1 : 0) > LevelStocking.MaxPlaces) return $"{others.Count} places (want at most {LevelStocking.MaxPlaces})";
        for (int i = 0; i < others.Count; i++)
        {
            if (Vector2.Distance(others[i].Position, exit.Position) < 40f) return $"{others[i].Kind} inside the exit's exclusion zone";
            if (Vector2.Distance(others[i].Position, start.Position) < 39f) return $"{others[i].Kind} too close to the start";
            for (int j = i + 1; j < others.Count; j++)
                if (Vector2.Distance(others[i].Position, others[j].Position) < 21f) return $"{others[i].Kind} and {others[j].Kind} closer than 21 m";
        }
        // One place sits early, unless the shell cache (which keeps to the middle of the way) is the level's only place.
        var early = others.Where(p => p.Early).ToList();
        if (others.Any(p => p.Kind != PoiKind.ShellCache) && (early.Count != 1 || Vector2.Distance(early[0].Position, start.Position) > 47f)) return $"{early.Count} early POIs (want exactly one, 39–47 m from the start)";

        // Path network.
        var mains = map.Corridors.Where(c => c.Kind == CorridorKind.Main).ToList();
        if (mains.Count is < 3 or > 5) return $"{mains.Count} corridors (want 3–5)";
        if (mains.Any(c => c.Width is < 6f or > 10f)) return "a corridor width outside 6–10 m";
        if (map.Plazas.Count is < 1 or > 4) return $"{map.Plazas.Count} plazas (want 1–4)";
        float sideBySide = LongestParallelRun(map);
        if (sideBySide > MaxSideBySide) return $"two routes run side by side for {sideBySide:0} m (want ≤ {MaxSideBySide:0} m)";
        foreach (var spur in map.Corridors.Where(c => c.Kind == CorridorKind.Spur))
            if (spur.Length > MaxSpur) return $"a spur of {spur.Length:0.0} m (want ≤ {MaxSpur:0} m)";
        if (map.Corridors.Any(c => c.Kind == CorridorKind.Pass && c.Width is < 3f or > 5f)) return "a pass width outside 3–5 m";

        // Nothing above the swim level on a path: water at least a metre deep everywhere on it.
        string? flat = CheckCorridorFloors(map);
        if (flat is not null) return flat;

        // Topography.
        float raised = AboveFraction(map);
        if (raised > MaxAbove) return $"{raised:P0} of the interior above the swim level (want ≤ {MaxAbove:P0})";
        // The labyrinth: side canyons branch off the routes.
        int sides = map.Corridors.Count(c => c.Kind == CorridorKind.Side);
        if (sides < MinSideCanyons) return $"{sides} side canyons (want ≥ {MinSideCanyons})";
        // The shaft is the only deep water: the deepest point of the level lies in it.
        if (!map.Shaft.Contains(DeepestPoint(map), 1f)) return "the shaft is not the deepest point";
        float peak = InteriorPeak(map);
        if (peak is < 3f or > 14f) return $"highest peak {peak:0.0} m (want 3–14 m)";

        // Arches across corridors.
        var arches = map.Canopies.Where(c => c.Kind == CanopyKind.Arch).ToList();
        if (arches.Count > 5) return $"{arches.Count} arches (want at most 5)";
        foreach (var arch in arches)
        {
            float span = arch.HalfLength * 2f;
            if (span is < 10f or > 20f) return $"an arch spanning {span:0.0} m (want 10–20 m)";
            if (!ArchCrossesCorridor(map, arch)) return "an arch does not span its corridor";
        }

        // Caves: apart, and opening onto a corridor or open water.
        var caves = map.Caves;
        for (int i = 0; i < caves.Count; i++)
        {
            if (!CaveMouthOpens(map, caves[i])) return $"the {map.Pois[caves[i].Poi].Kind} cave's mouth faces rock";
            for (int j = i + 1; j < caves.Count; j++)
                if (Vector2.Distance(map.Pois[caves[i].Poi].Position, map.Pois[caves[j].Poi].Position) < 25f) return "two caves closer than 25 m";
        }

        // Loot under the floor.
        if (map.Pockets.Count is < 3 or > 5) return $"{map.Pockets.Count} sealed pockets (want 3–5)";
        if (map.Coins.Count is < 6 or > 10) return $"{map.Coins.Count} buried coins (want 6–10)";

        // The stamps: the rock around the start and the exit is exactly the rock the neighbouring levels share.
        string? stamps = CheckStamps(map);
        if (stamps is not null) return stamps;

        // Reachability and the exit's distance.
        var dist = Distances(map, start.Position);
        foreach (var poi in map.Pois)
            if (float.IsPositiveInfinity(DistanceAt(dist, poi.Position))) return $"the {poi.Kind} cannot be reached from the start";
        float swim = OpenFraction(map);
        if (swim < MinOpen) return $"only {swim:P0} of the level is free-swimming water (want ≥ {MinOpen:P0})";
        float geodesic = DistanceAt(dist, exit.Position);
        if (geodesic < MinExitGeodesic) return $"the exit is only {geodesic:0} m from the start (want ≥ {MinExitGeodesic:0} m)";
        return null;
    }

    /// <summary>How far a pinned height may stray from its stamp.</summary>
    public const float StampTolerance = 0.1f;

    /// <summary>Every grid point within the pinned radius of the start and the exit (outside the shaft) matches its stamp.</summary>
    public static string? CheckStamps(LevelMap map)
    {
        var entry = Stamp.Generate(map.EntryStampSeed, map.EntryFromArena);
        var exit = Stamp.Generate(map.ExitStampSeed, map.HasBoss);
        foreach (var (stamp, at, name) in new[] { (entry, map.Start.Position, "start"), (exit, map.Exit.Position, "exit") })
        {
            float r = Stamp.Pin - 0.5f;
            for (int y = (int)(at.Y - r); y <= (int)(at.Y + r) + 1; y++)
            for (int x = (int)(at.X - r); x <= (int)(at.X + r) + 1; x++)
            {
                var p = new Vector2(x, y);
                if (Vector2.Distance(p, at) > r || map.Shaft.Contains(p, 0.5f)) continue;
                float want = stamp.HeightAt(p - at);
                if (MathF.Abs(map[x, y] - want) > StampTolerance) return $"the {name}'s stamp is off by {map[x, y] - want:0.00} m at ({x}, {y})";
            }
        }
        return null;
    }

    /// <summary>Water under a path is at least this deep.</summary>
    public const float PathClearance = 1f;

    /// <summary>Samples across every corridor: the seabed lies at least <see cref="PathClearance"/> below the swim level.</summary>
    public static string? CheckCorridorFloors(LevelMap map)
    {
        foreach (var c in map.Corridors)
        {
            for (int i = 0; i < c.Points.Count; i++)
            {
                Vector2 tangent = Geo.Normalize(c.Points[Math.Min(i + 1, c.Points.Count - 1)] - c.Points[Math.Max(i - 1, 0)], Vector2.UnitX);
                Vector2 nrm = Geo.Perp(tangent);
                for (float k = -0.95f; k <= 0.951f; k += 0.19f)
                {
                    Vector2 p = c.Points[i] + nrm * (k * c.HalfWidth);
                    if (!InSquare(p)) continue;
                    float h = map.HeightAt(p);
                    if (h > -PathClearance) return $"a {c.Kind} corridor rises to {h:0.00} m at ({p.X:0.0}, {p.Y:0.0})";
                }
            }
        }
        return null;
    }

    /// <summary>Share of the square inside the rim that is free-swimming water reachable from the start.</summary>
    public static float OpenFraction(LevelMap map)
    {
        var dist = Distances(map, map.Start.Position);
        int margin = (int)MathF.Ceiling(LevelMap.RimWidth);
        int open = 0, total = 0;
        for (int y = margin; y < N - 1 - margin; y++)
        for (int x = margin; x < N - 1 - margin; x++)
        {
            total++;
            if (float.IsFinite(dist[y * N + x])) open++;
        }
        return open / (float)total;
    }

    /// <summary>Two routes closer than this (centre lines) leave no ridge between them: they read as one wide lane.</summary>
    public const float SideBySideGap = 16f;
    /// <summary>How long two routes may run that close away from where they meet.</summary>
    public const float MaxSideBySide = 30f;

    /// <summary>
    /// The longest stretch (metres) of any main corridor that runs alongside another, its centre line within
    /// <paramref name="near"/> of the other's, away from where they meet (start, rift, plazas).
    /// </summary>
    public static float LongestParallelRun(LevelMap map, float near = SideBySideGap)
    {
        var mains = map.Corridors.Where(c => c.Kind == CorridorKind.Main).ToList();
        float worst = 0f;
        foreach (var c in mains)
        {
            float run = 0f;
            for (int i = 1; i < c.Points.Count; i++)
            {
                var p = c.Points[i];
                // Routes share the stamps' canyons out of the start and into the exit.
                bool meeting = Vector2.Distance(p, map.Start.Position) < Stamp.Blend + 14f || Vector2.Distance(p, map.Exit.Position) < Stamp.Blend + 14f
                    || map.Plazas.Any(z => Vector2.Distance(z.Center, p) < 18f);
                bool beside = !meeting && mains.Any(o => o != c && o.DistanceToCentre(p) < near);
                run = beside ? run + Vector2.Distance(c.Points[i - 1], p) : 0f;
                worst = MathF.Max(worst, run);
            }
        }
        return worst;
    }

    static bool InSquare(Vector2 p) => p.X >= 0f && p.Y >= 0f && p.X <= LevelMap.Size && p.Y <= LevelMap.Size;

    /// <summary>The highest ground inside the rim.</summary>
    public const int MinSideCanyons = 2;
    /// <summary>At least this share of the interior is water reachable from the start.</summary>
    public const float MinOpen = 0.45f;

    /// <summary>At most this share of the interior (inside the rim) stands above the swim level.</summary>
    public const float MaxAbove = 0.4f;

    /// <summary>Share of the interior (inside the rim) whose terrain stands above the swim level.</summary>
    public static float AboveFraction(LevelMap map)
    {
        int lo = (int)LevelMap.RimWidth, hi = LevelMap.Size - lo, above = 0, total = 0;
        for (int y = lo; y <= hi; y++)
        for (int x = lo; x <= hi; x++)
        {
            total++;
            if (map[x, y] > LevelMap.BlockHeight) above++;
        }
        return above / (float)total;
    }

    /// <summary>The lowest grid point inside the rim.</summary>
    public static Vector2 DeepestPoint(LevelMap map)
    {
        float best = float.MaxValue;
        Vector2 at = default;
        int margin = (int)MathF.Ceiling(LevelMap.RimWidth) + 1;
        for (int y = margin; y <= LevelMap.Size - margin; y++)
        for (int x = margin; x <= LevelMap.Size - margin; x++)
            if (map[x, y] < best)
            {
                best = map[x, y];
                at = new Vector2(x, y);
            }
        return at;
    }

    public static float InteriorPeak(LevelMap map)
    {
        float best = float.MinValue;
        int margin = (int)MathF.Ceiling(LevelMap.RimWidth) + 1;
        for (int y = margin; y <= LevelMap.Size - margin; y++)
        for (int x = margin; x <= LevelMap.Size - margin; x++)
            best = MathF.Max(best, map[x, y]);
        return best;
    }

    /// <summary>The span between the arch's footings crosses the centre line of its corridor.</summary>
    public static bool ArchCrossesCorridor(LevelMap map, Canopy arch)
    {
        if (arch.Corridor < 0 || arch.Corridor >= map.Corridors.Count) return false;
        var line = map.Corridors[arch.Corridor].Points;
        for (int i = 1; i < line.Count; i++)
            if (Geo.SegmentIntersection(arch.EndA, arch.EndB, line[i - 1], line[i], out _)) return true;
        return false;
    }

    /// <summary>Stepping out of the mouth, Clementine reaches a corridor or open water within a few metres.</summary>
    public static bool CaveMouthOpens(LevelMap map, CaveSite cave)
    {
        for (float s = 0.5f; s <= 6f; s += 0.5f)
        {
            Vector2 p = cave.Mouth + cave.Facing * s;
            if (!map.IsOpen(p)) return false;
            bool inCave = cave.Chambers.Any(c => Vector2.Distance(c.Center, p) < c.Radius);
            if (!inCave && (map.InsideAnyCorridor(p) || s >= 3f)) return true;
        }
        return false;
    }

    // ───────────────────────── flood fill ─────────────────────────

    const int N = LevelMap.Size;

    /// <summary>The cell a position falls in.</summary>
    public static (int X, int Y) Cell(Vector2 p) => (Math.Clamp((int)p.X, 0, N - 1), Math.Clamp((int)p.Y, 0, N - 1));

    /// <summary>Half the bell's width (~0.8 m across): the clearance a swimmable cell must have all round.</summary>
    public const float BodyRadius = 0.4f;

    /// <summary>A 1 m cell is swimmable when the bell fits at its centre: the ground under it and all round it lies below the swim band.</summary>
    public static bool CellOpen(LevelMap map, int x, int y, float clearance = BodyRadius)
    {
        var c = new Vector2(x + 0.5f, y + 0.5f);
        if (!map.IsOpen(c)) return false;
        for (int i = 0; i < 8; i++)
        {
            float a = i * MathF.Tau / 8f;
            if (!map.IsOpen(c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * clearance)) return false;
        }
        return true;
    }

    /// <summary>Swimming distance from a point to every 1 m cell (∞ where unreachable). Weak rock is not counted as a wall.</summary>
    public static float[] Distances(LevelMap map, Vector2 from, float clearance = BodyRadius)
    {
        var open = new bool[N * N];
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
            open[y * N + x] = CellOpen(map, x, y, clearance);
        var dist = new float[N * N];
        Array.Fill(dist, float.PositiveInfinity);
        var (sx, sy) = Cell(from);
        var queue = new PriorityQueue<int, float>();
        dist[sy * N + sx] = 0f;
        queue.Enqueue(sy * N + sx, 0f);
        while (queue.TryDequeue(out int i, out float d))
        {
            if (d > dist[i]) continue;
            int x = i % N, y = i / N;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= N || ny >= N || !open[ny * N + nx]) continue;
                // No squeezing diagonally between two walls.
                if (dx != 0 && dy != 0 && (!open[y * N + nx] || !open[ny * N + x])) continue;
                float nd = d + (dx != 0 && dy != 0 ? 1.41421356f : 1f);
                int j = ny * N + nx;
                if (nd < dist[j])
                {
                    dist[j] = nd;
                    queue.Enqueue(j, nd);
                }
            }
        }
        return dist;
    }

    public static float DistanceAt(float[] dist, Vector2 p)
    {
        var (x, y) = Cell(p);
        return dist[y * N + x];
    }

    /// <summary>A shortest swimming route as cell centres (empty if unreachable).</summary>
    public static List<Vector2> ShortestPath(LevelMap map, Vector2 from, Vector2 to, float clearance = BodyRadius)
    {
        var dist = Distances(map, to, clearance);
        var path = new List<Vector2>();
        var (x, y) = Cell(from);
        if (float.IsPositiveInfinity(dist[y * N + x])) return path;
        for (int guard = 0; guard < N * N; guard++)
        {
            path.Add(new Vector2(x + 0.5f, y + 0.5f));
            float here = dist[y * N + x];
            if (here <= 0f) break;
            int bx = x, by = y;
            float best = here;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= N || ny >= N) continue;
                if (dist[ny * N + nx] < best)
                {
                    best = dist[ny * N + nx];
                    bx = nx;
                    by = ny;
                }
            }
            if (bx == x && by == y) break;
            x = bx;
            y = by;
        }
        return path;
    }
}
