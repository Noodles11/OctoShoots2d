using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using OctoShoots.Core.Run;
using OctoShoots.Core.Creatures;
using static OctoShoots.Core.Gen.SdfMath;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>
/// Builds a <see cref="LevelMap"/> through the six stages of DESIGN-TOPDOWN §4.1, each on its own seeded sub-stream:
/// POIs → path network → topography → arches/caves/passes → decoration → spawn table. A level that fails
/// <see cref="LevelValidator"/> is rebuilt from the next attempt's sub-streams (the inherited retry rule).
/// </summary>
public static class TopDownGenerator
{
    public const int MaxAttempts = 100;
    /// <summary>Arches a level tries for (as many as fit), and the most it may have.</summary>
    public const int ArchesMin = 2, ArchesMax = 5, ArchesCap = 6;
    /// <summary>
    /// Passes, the level's shortcuts (tunnels through the rock): PassesMin–PassesMax a level, each PassMin–PassMax metres
    /// long, where the way round by water is at least PassSaves times as long.
    /// </summary>
    public const int PassesMin = 2, PassesMax = 4;
    /// <summary>Passes bored past the level's share, as spares: not every one can be roofed (those are filled back in).</summary>
    const int PassSpares = 3;
    const float PassMin = 10f, PassMax = 36f, PassSaves = 1.6f;
    /// <summary>How far an arch spans from footing to footing (metres): over a tunnel, or across a chamber's edge.</summary>
    public const float ArchSpanMin = 10f, ArchSpanMax = 24f;
    /// <summary>Buried coins and sealed pockets a level holds (the validator checks them).</summary>
    public const int CoinsMin = 4, CoinsMax = 7, PocketsMin = 2, PocketsMax = 4;

    const float Size = LevelMap.Size;
    const float Rim = LevelMap.RimWidth;

    // Stages 1–2: the cave (CaveLayout) and its places.
    /// <summary>The start's clearing, and a blue hole's.</summary>
    const float StartRadius = 8f;
    /// <summary>The boss arena: a ~30 m disc with the Crack across its middle.</summary>
    const float ArenaRadius = 15f;
    /// <summary>A blue hole's shaft, inside its clearing.</summary>
    /// <summary>
    /// The exit's chamber, sunk (SinkExitChamber): its floor sloping down from its walls to the shaft's floor in its
    /// middle, where she can dive (within DiveRadius), its tunnels easing in over ExitMouth metres past its radius
    /// (ExitChamber, if it has none).
    /// </summary>
    const float DiveRadius = 3f, ExitMouth = 4f, ExitChamber = 10.5f;
    /// <summary>
    /// The bowl's floor in its middle: BowlLift over the shaft's floor (the level below's seabed, which would otherwise
    /// show through it in patches before the dive opens it), the last MiddleDip of it a faint dip in the very middle.
    /// </summary>
    const float BowlLift = 0.8f, MiddleDip = 0.6f;
    /// <summary>The exit bowl's slope starts this far inside the rock (metres): its walls drop on into it.</summary>
    const float WallRunsOn = 1.5f;
    const float ClearingRadius = 6f;       // open POIs (item spawn, ninja nests)
    const float PocketRadius = 6f;
    const float ChamberRadius = 6.5f;
    /// <summary>
    /// The curse den's tunnel (DESIGN-TOPDOWN §4.2): open TunnelHalfWidth each side of its line, TunnelMin–TunnelMax m
    /// long (a target picked per level; the longest that fits, never under TunnelMin), in legs of TunnelLegMin–Max m
    /// with a bend between each, ending in a chamber of TunnelEndRadius. Its passes keep TunnelWall of rock apart.
    /// </summary>
    public const float TunnelHalfWidth = 1.4f, TunnelEndRadius = 2.4f;
    const float TunnelMin = 16f, TunnelMax = 32f, TunnelLegMin = 5f, TunnelLegMax = 9f, TunnelStep = 2.5f, TunnelWall = 3.5f;

    // Stage 3
    /// <summary>Open shapes are pinned flat this far past their edge, so bilinear sampling anywhere inside them reads exactly 0.</summary>
    const float FlatMargin = 1.5f;
    /// <summary>The seabed, level −1: the cave's floor lies at least this far under the swim level.</summary>
    const float PathDepth = 1f;
    /// <summary>Wall tops: the prototype's terrain, mapped into this range, so plateaus roll rather than lie flat.</summary>
    const float WallTopMin = 4f, WallTopMax = 14f;
    /// <summary>The rock's foot at the water's edge, and how fast it climbs (metres per metre) before the slopes are eased.</summary>
    const float WallFoot = 0.4f, WallRise = 1.6f;
    // Cave floors are the one flat ground (between the chart's contours, so never on a contour line); passes undulate below theirs.
    const float CaveFloor = 3.5f, PassFloor = 2.5f;
    /// <summary>Rounded rock shoulders raised beside a corridor when no flank stands high enough to carry an arch.</summary>
    const float ShoulderRadius = 6f, ShoulderHeight = 8f;

    /// <summary>The prevailing current; sea fans grow broadside to it (inherited flora rule).</summary>
    static readonly Vector2 Current = Vector2.Normalize(new Vector2(1f, 0.35f));

    /// <param name="cosmeticSalt">Test hook: changes only the cosmetic stream (decoration), to prove it never touches gameplay.</param>
    /// <param name="background">Made while she plays (the level below): on one thread, leaving the rest to the game.</param>
    public static LevelMap Generate(RunStreams streams, LevelId id, Action<string>? log = null, string cosmeticSalt = "", bool background = false)
    {
        var plan = LevelPlan.For(streams.Seed, id);
        var reasons = new List<string>();
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            // Every attempt starts from the bare seabed at level −1.
            var map = new LevelMap
            {
                Seed = streams.Seed.Value, Id = id, Attempt = attempt, HasBoss = plan.HasBoss, Rift = plan.Rift, Menace = plan.Menace,
                EntryStampSeed = plan.EntryStamp, ExitStampSeed = plan.ExitStamp, EntryFromArena = plan.EntryFromArena,
                Template = plan.Template,
            };
            Array.Fill(map.Heights, -PathDepth);
            // The wall tops come from the reef prototype's terrain for this text (the run's first try: the seed itself).
            string text = id == LevelId.First && attempt == 0 ? streams.Seed.ToString() : $"{streams.Seed} {id.Cycle}-{id.Depth}-{id.Level}-{attempt}";
            string? problem = Build(map, streams, plan, attempt, cosmeticSalt, text) ?? LevelValidator.Validate(map);
            log?.Invoke($"Top-down level {id} attempt {attempt + 1}: {problem ?? "ok"}");
            if (problem is null)
            {
                // Water she can never reach becomes mountains (after the checks: it never changes what she can reach).
                int raised = Mountains.Fill(map);
                // No kelp rooted in what is rock now.
                foreach (var bed in map.Kelp)
                    for (int k = bed.Roots.Count - 1; k >= 0; k--)
                        if (map.HeightAt(bed.Roots[k]) > LevelMap.BlockHeight - 0.2f)
                        {
                            bed.Roots.RemoveAt(k);
                            bed.Lengths.RemoveAt(k);
                        }
                map.Kelp.RemoveAll(bed => bed.Roots.Count < KelpLeast);
                log?.Invoke($"Top-down level {id}: {raised} unreachable points raised into mountains");
                return map;
            }
            reasons.Add(problem);
        }
        throw new InvalidOperationException($"No valid top-down level for {id} after {MaxAttempts} attempts: {string.Join("; ", reasons.Distinct())}");
    }

    /// <summary>
    /// Runs the stages — POIs on the bare seabed (the start and the exit where their stamps fit), the route canyons
    /// between them out through the stamps' openings, the labyrinth of side canyons, the walls raised around all of it,
    /// the stamps pinned in, caves, passes and arches, the stamps pinned again, the shaft cut, then decoration, loot and
    /// spawns — and returns why the attempt failed, or null.
    /// </summary>
    static string? Build(LevelMap map, RunStreams streams, LevelPlan plan, int attempt, string cosmeticSalt, string terrainText)
    {
        Rng Stage(string name) => streams.TopDown(plan.Id, attempt, name);
        uint noiseSeed = (uint)(Stage("noise").NextU64() >> 32);

        var spurDirs = new Dictionary<int, Vector2>();
        string? cave = LayCave(map, Stage("cave"), plan, spurDirs, out var open);
        if (cave is not null) return cave;
        var strata = new WallStrata(Stage("strata"));
        RaiseCaveWalls(map, open, terrainText, Stage("topo"), strata);
        string? features = BuildFeatures(map, Stage("features"), spurDirs, strata);
        if (features is not null) return features;
        FlattenSeabed(map);
        SoftenSlopes(map, Stage("slopes"), strata);
        CutShaft(map, Stage("shaft"));
        ClampHeights(map);
        RoofShortcuts(map);
        Decorate(map, Stage("cosmetic" + cosmeticSalt), noiseSeed, strata);
        PlaceKelp(map, Stage("kelp"));
        PlacePickups(map, Stage("pickups"));
        PlaceSpawns(map, Stage("spawns"), plan.Menace);
        return null;
    }

    // ───────────────────────── helpers ─────────────────────────

    static bool Inside(Vector2 p, float inset) => p.X >= inset && p.Y >= inset && p.X <= Size - inset && p.Y <= Size - inset;

    static Vector2 Round(Vector2 p) => new(MathF.Round(p.X), MathF.Round(p.Y));

    static Vector2 RandomIn(Rng rng, float inset) => new(rng.Range(inset, Size - inset), rng.Range(inset, Size - inset));

    static float Smooth(float edge0, float edge1, float x) => MathUtil.Smoothstep(MathUtil.Clamp01((x - edge0) / (edge1 - edge0)));

    // ───────────────────────── stages 1–2: the cave, its places and tunnels ─────────────────────────

    /// <summary>A special room's cave, or an open place's clearing.</summary>
    static float RadiusOf(PoiKind kind) =>
        Poi.IsCaveHosted(kind) ? (kind is PoiKind.TreasureCave or PoiKind.CurseDen ? ChamberRadius : PocketRadius) : ClearingRadius;

    /// <summary>
    /// Lays out the level's cave (<see cref="CaveLayout"/>, docs/CAVEGEN.md) and records it: the start and the way down,
    /// every place of the plan, the tunnels as corridors (a room's own tunnel as its spur) and the chambers as plazas.
    /// </summary>
    static string? LayCave(LevelMap map, Rng rng, LevelPlan plan, Dictionary<int, Vector2> spurDirs, out bool[] open)
    {
        // The plan's places; the early one is the shop when there is one (to teach the economy), else a treasure.
        var kinds = new List<PoiKind>();
        for (int i = 0; i < plan.Shops; i++) kinds.Add(PoiKind.Shop);
        for (int i = 0; i < plan.Treasures; i++) kinds.Add(PoiKind.TreasureCave);
        for (int i = 0; i < plan.Secrets; i++) kinds.Add(PoiKind.Secret);
        for (int i = 0; i < plan.Curses; i++) kinds.Add(PoiKind.CurseDen);
        for (int i = 0; i < plan.Ambushes; i++) kinds.Add(PoiKind.Ambush);
        for (int i = 0; i < plan.Caches; i++) kinds.Add(PoiKind.ShellCache);
        if (kinds.Count > 0)
        {
            PoiKind earlyKind = kinds.Contains(PoiKind.Shop) && (rng.NextFloat() < 0.8f || !kinds.Contains(PoiKind.TreasureCave)) ? PoiKind.Shop
                : kinds.Contains(PoiKind.TreasureCave) ? PoiKind.TreasureCave : kinds[0];
            kinds.Remove(earlyKind);
            kinds.Insert(0, earlyKind);
        }

        var (cave, why) = CaveLayout.Build(rng, plan.HasBoss, plan.EntryFromArena, kinds, RadiusOf, plan.Template?.Layout);
        open = cave?.Open ?? Array.Empty<bool>();
        if (cave is null) return why;
        map.Pois.Add(new Poi { Kind = PoiKind.Start, Position = cave.Start.At, Radius = plan.EntryFromArena ? ArenaRadius : StartRadius });
        map.Pois.Add(new Poi { Kind = PoiKind.Exit, Position = cave.Exit.At, Radius = plan.HasBoss ? ArenaRadius : StartRadius });
        var spurOf = new Dictionary<CaveLayout.Tunnel, int>();
        foreach (var place in cave.Places)
        {
            int pi = map.Pois.Count;
            map.Pois.Add(new Poi { Kind = place.Kind, Position = place.At, Radius = place.Radius, Early = place.Early });
            if (place.Spur is null) continue;
            spurOf[place.Spur] = pi;
            spurDirs[pi] = place.Facing;
        }
        foreach (var t in cave.Tunnels)
            map.Corridors.Add(new Corridor { Kind = t.Kind, Width = t.Width, Points = t.Points.ToList(), Poi = spurOf.TryGetValue(t, out int poi) ? poi : -1 });
        foreach (var c in cave.Chambers)
        {
            if (c.Room) continue;
            map.Plazas.Add(new Plaza(c.At, c.R));
            bool main = c.Role is CaveLayout.Role.Start or CaveLayout.Role.Main or CaveLayout.Role.Exit;
            map.Chambers.Add(new CaveChamber(c.At, c.R, main, c.Role == CaveLayout.Role.Branch && c.Links.Count(l => !l.Room) <= 1));
        }
        AssignNaves(map);
        return null;
    }

    /// <summary>
    /// The naves (DESIGN-TOPDOWN §4.9): every chamber off the main route holds something. A place in it (an open
    /// place's clearing within it) or opening off it (a room whose tunnel leaves from it) is its content, one place to a
    /// nave; every nave left over is a den.
    /// </summary>
    static void AssignNaves(LevelMap map)
    {
        var naves = map.Chambers.Where(c => !c.Main).ToList();
        var poiOf = new int[naves.Count];
        Array.Fill(poiOf, -1);
        for (int pi = 0; pi < map.Pois.Count; pi++)
        {
            var poi = map.Pois[pi];
            if (poi.Kind is PoiKind.Start or PoiKind.Exit) continue;
            // A room's: where its tunnel leaves the cave; an open place's: where it lies.
            var spur = map.Corridors.FirstOrDefault(c => c.Poi == pi);
            var at = spur is { Points.Count: > 0 } ? spur.Points[0] : poi.Position;
            int best = -1;
            float bestD = 3f;
            for (int n = 0; n < naves.Count; n++)
            {
                if (poiOf[n] >= 0) continue;
                float d = Vector2.Distance(at, naves[n].At) - naves[n].Radius;
                if (d < bestD)
                {
                    bestD = d;
                    best = n;
                }
            }
            if (best >= 0) poiOf[best] = pi;
        }
        for (int n = 0; n < naves.Count; n++) map.Naves.Add(new Nave(naves[n].At, naves[n].Radius, poiOf[n]));
    }

    /// <summary>
    /// The rock round the cave: open water lies on the seabed (the boss arena a level floor), and the rock climbs from
    /// the water's edge to the prototype's terrain, mapped between <see cref="WallTopMin"/> and <see cref="WallTopMax"/>.
    /// The terrain is read through the strata's low-frequency warp, so wall outlines wander like real reef edges, and
    /// the rock is terraced into the level's strata (<see cref="WallStrata.Terrace"/>). Only rock is shaped: the water
    /// is exactly the cave's. The impassable rim rises out of it with the same brush.
    /// </summary>
    static void RaiseCaveWalls(LevelMap map, bool[] open, string terrainText, Rng rng, WallStrata strata)
    {
        const int N = LevelMap.Samples;
        var tops = new float[N * N];
        ReefTerrain.Generate(tops, N, terrainText);
        uint dipSeed = (uint)(rng.NextU64() >> 32);
        var toWater = CaveLayout.Chamfer(open, true);
        var rift = map.Exit;
        // The raw profile first: the floor, and the rock rising from the water's edge to its warped top.
        var raw = new float[N * N];
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            int i = y * N + x;
            if (open[i])
            {
                float h = -PathDepth - 0.6f * FloorDip(x, y, dipSeed);
                if (map.HasBoss) h = MathUtil.Lerp(-PathDepth, h, Smooth(rift.Radius, rift.Radius + 5f, Vector2.Distance(new Vector2(x, y), rift.Position)));
                raw[i] = h;
            }
            else
            {
                var warp = strata.Warp(x, y);
                float t = Bilinear(tops, x + warp.X, y + warp.Y);
                float top = WallTopMin + (WallTopMax - WallTopMin) * (t - ReefTerrain.HMin) / (ReefTerrain.HMax - ReefTerrain.HMin);
                raw[i] = MathF.Max(WallFoot, MathF.Min(top, WallFoot + WallRise * (toWater[i] - 1f)));
            }
        }
        float RawAt(float x, float y) => Bilinear(raw, x, y);
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            int i = y * N + x;
            // Terraced: rock only, and never down to the swim level.
            float h = open[i] ? raw[i] : MathF.Max(WallFoot, strata.Terrace(raw[i], x, y, RawAt));
            float e = MathF.Min(MathF.Min(x, y), MathF.Min(Size - x, Size - y));
            float w = ReefTerrain.Falloff(e / (Rim + 1f));
            if (w > 0f) h += w * (ReefTerrain.HMax - h);
            map.Heights[i] = h;
        }
    }

    /// <summary>A grid field's bilinear value at a point (clamped to the square).</summary>
    static float Bilinear(float[] f, float x, float y)
    {
        const int N = LevelMap.Samples;
        x = Math.Clamp(x, 0f, N - 1.001f);
        y = Math.Clamp(y, 0f, N - 1.001f);
        int ix = (int)x, iy = (int)y;
        float tx = x - ix, ty = y - iy;
        int i = iy * N + ix;
        return MathUtil.Lerp(MathUtil.Lerp(f[i], f[i + 1], tx), MathUtil.Lerp(f[i + N], f[i + N + 1], tx), ty);
    }

    /// <summary>The nearest point on a polyline.</summary>
    static Vector2 NearestPoint(List<Vector2> line, Vector2 p) => NearestPoint(line, p, out _);

    /// <summary>The nearest point on a polyline, and the line's direction there.</summary>
    static Vector2 NearestPoint(List<Vector2> line, Vector2 p, out Vector2 direction)
    {
        direction = Vector2.UnitX;
        Vector2 best = line[0];
        float bestD = float.MaxValue;
        for (int i = 1; i < line.Count; i++)
        {
            Vector2 a = line[i - 1], ab = line[i] - a;
            float t = Math.Clamp(Vector2.Dot(p - a, ab) / MathF.Max(ab.LengthSquared(), 1e-9f), 0f, 1f);
            Vector2 q = a + ab * t;
            float d = Vector2.DistanceSquared(q, p);
            if (d < bestD)
            {
                bestD = d;
                best = q;
                direction = Geo.Normalize(ab, direction);
            }
        }
        return best;
    }

    static Vector2 NearestOnCorridors(LevelMap map, Vector2 p, out int corridor)
    {
        corridor = -1;
        Vector2 best = p;
        float bestD = float.MaxValue;
        for (int c = 0; c < map.Corridors.Count; c++)
        {
            if (map.Corridors[c].Kind != CorridorKind.Main) continue;
            Vector2 q = NearestPoint(map.Corridors[c].Points, p);
            float d = Vector2.DistanceSquared(q, p);
            if (d < bestD)
            {
                bestD = d;
                best = q;
                corridor = c;
            }
        }
        return best;
    }

    // ───────────────────────── stage 3: topography ─────────────────────────

    /// <summary>Everything that must stay open at h = 0: corridors, plazas, the start and rift discs, open POI clearings.</summary>
    static float DistanceToOpen(LevelMap map, Vector2 p, int skipSpurOf = -1)
    {
        float d = float.MaxValue;
        foreach (var c in map.Corridors)
        {
            if (skipSpurOf >= 0 && c.Poi == skipSpurOf) continue;
            float half = c.HalfWidth;
            for (int i = 1; i < c.Points.Count; i++)
            {
                // Cheap reject before the exact distance.
                Vector2 a = c.Points[i - 1], b = c.Points[i];
                if (MathF.Min(a.X, b.X) - p.X > d + half || p.X - MathF.Max(a.X, b.X) > d + half) continue;
                if (MathF.Min(a.Y, b.Y) - p.Y > d + half || p.Y - MathF.Max(a.Y, b.Y) > d + half) continue;
                d = MathF.Min(d, Geo.SegmentDistance(p, a, b) - half);
            }
        }
        foreach (var plaza in map.Plazas) d = MathF.Min(d, Vector2.Distance(p, plaza.Center) - plaza.Radius);
        foreach (var poi in map.Pois)
            if (!Poi.IsCaveHosted(poi.Kind)) d = MathF.Min(d, Vector2.Distance(p, poi.Position) - poi.Radius);
        return MathF.Max(d - FlatMargin, 0f);
    }

    /// <summary>The seabed's one level: every floor deeper than this is raised to it (DESIGN-TOPDOWN §4.1).</summary>
    public const float Seabed = -1.2f;

    /// <summary>
    /// A flat seabed (DESIGN-TOPDOWN §4.1): every floor deeper than <see cref="Seabed"/> is raised to it, so nothing on the
    /// floor draws the eye from what matters; the walls' feet keep their slope. Only where all its neighbours lie under
    /// the swim level, so no cell's ground rises into it: the water she can swim is exactly what it was. It runs before
    /// the shaft is cut, so the shaft keeps its depth; the rift's arena keeps its own, and the stamps' floors are lifted
    /// with the rest (alike on both levels that share them).
    /// </summary>
    static void FlattenSeabed(LevelMap map)
    {
        var rift = map.Exit;
        var raise = new List<int>();
        for (int y = 0; y < LevelMap.Samples; y++)
        for (int x = 0; x < LevelMap.Samples; x++)
        {
            int i = y * LevelMap.Samples + x;
            if (map.Heights[i] >= Seabed) continue;
            if ((map.HasBoss || map.Rift) && Vector2.Distance(new Vector2(x, y), rift.Position) < rift.Radius * 0.7f) continue;
            bool under = true;
            for (int dy = -1; dy <= 1 && under; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= LevelMap.Samples || ny >= LevelMap.Samples) continue;
                if (map.Heights[ny * LevelMap.Samples + nx] > LevelMap.BlockHeight) { under = false; break; }
            }
            if (under) raise.Add(i);
        }
        foreach (int i in raise) map.Heights[i] = Seabed;
    }

    /// <summary>
    /// Most walls slope rather than stand sheer (DESIGN-TOPDOWN §4.1): above the swim level the rock is held under the
    /// strata's profile rising from the water's edge (<see cref="WallStrata.SlopeCap"/>): the lowest stratum a cliff of
    /// 50–60°, its steepness varying slowly over the level, each layer above leaning back further, so a face reads as a
    /// reef's and a narrow ridge rounds over lower. A share of the walls, rolled per level, stays sheer. Only rock above
    /// the swim level is lowered, never below it, so the water she swims is the same (the edge moves only by a
    /// fraction of a metre).
    /// </summary>
    static void SoftenSlopes(LevelMap map, Rng rng, WallStrata strata)
    {
        const int N = LevelMap.Samples;
        // Distance (metres) from every grid point to the nearest open water, and to the nearest roof (an arch's deck,
        // a cave's or an overhang's: the rock under and beside them carries them, so it keeps its height).
        var d = Chamfer(i => map.Heights[i] <= LevelMap.BlockHeight);
        // (Cave roofs and the shortcut tunnels' roofs alike: a tunnel keeps the rock it was bored through.)
        var roof = Chamfer(i => !float.IsNaN(map.Lid[i]) || !float.IsNaN(map.Roof[i]));
        uint seed = (uint)(rng.NextU64() >> 32);
        var eased = new bool[N * N];
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            int i = y * N + x;
            float h = map.Heights[i];
            if (h <= LevelMap.BlockHeight || d[i] == float.MaxValue) continue;
            var p = new Vector2(x, y);
            // Where the walls stay cliffs, and how steep the rest lean (two slow noises).
            float cliff = 0.5f + 0.5f * TerrainNoise.Fbm(x / 22f + 31f, y / 22f - 17f, seed, 2);
            float soft = MathUtil.Smoothstep(MathUtil.Clamp01((cliff - strata.CliffShare + 0.06f) / 0.12f));
            float steep = 0.5f + 0.5f * TerrainNoise.Fbm(x / 30f - 7f, y / 30f + 11f, seed + 5u, 2);
            // Not under or beside a roof.
            soft *= MathUtil.Smoothstep(MathUtil.Clamp01((roof[i] - 1f) / 3f));
            if (soft <= 0f) continue;
            // Held under the strata's profile, but never down to the swim level (a wall stays a wall).
            float cap = MathF.Max(strata.SlopeCap(d[i], steep, x, y), 0.4f);
            if (cap >= h) continue;
            map.Heights[i] = MathUtil.Lerp(h, cap, soft);
            eased[i] = true;
        }
        // The distance is measured in eight directions, which leaves faint facets: two light passes round them off
        // (over the eased rock only, never down to the swim level).
        for (int pass = 0; pass < 2; pass++)
        {
            var was = (float[])map.Heights.Clone();
            for (int y = 1; y < N - 1; y++)
            for (int x = 1; x < N - 1; x++)
            {
                int i = y * N + x;
                if (!eased[i]) continue;
                float sum = 0f;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++) sum += MathF.Max(was[i + dy * N + dx], 0.4f);
                map.Heights[i] = MathF.Max(MathF.Min(sum / 9f, was[i] + 0.3f), 0.4f);
            }
        }
    }

    /// <summary>Distance (metres, 8-way chamfer) from every grid point to the nearest point that is <paramref name="source"/>.</summary>
    static float[] Chamfer(Func<int, bool> source)
    {
        const int N = LevelMap.Samples;
        var d = new float[N * N];
        for (int i = 0; i < d.Length; i++) d[i] = source(i) ? 0f : float.MaxValue;
        const float D1 = 1f, D2 = 1.41421356f;
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            int i = y * N + x;
            if (x > 0) d[i] = MathF.Min(d[i], d[i - 1] + D1);
            if (y > 0) d[i] = MathF.Min(d[i], d[i - N] + D1);
            if (x > 0 && y > 0) d[i] = MathF.Min(d[i], d[i - N - 1] + D2);
            if (x < N - 1 && y > 0) d[i] = MathF.Min(d[i], d[i - N + 1] + D2);
        }
        for (int y = N - 1; y >= 0; y--)
        for (int x = N - 1; x >= 0; x--)
        {
            int i = y * N + x;
            if (x < N - 1) d[i] = MathF.Min(d[i], d[i + 1] + D1);
            if (y < N - 1) d[i] = MathF.Min(d[i], d[i + N] + D1);
            if (x < N - 1 && y < N - 1) d[i] = MathF.Min(d[i], d[i + N + 1] + D2);
            if (x > 0 && y < N - 1) d[i] = MathF.Min(d[i], d[i + N - 1] + D2);
        }
        return d;
    }

    /// <summary>
    /// The shortcut tunnels' roofs made whole (<see cref="CleanRoofs"/>: every pass was bored through the rock, which
    /// stays over it), and laid out as canopy pieces along each, which fade as one when she is near.
    /// </summary>
    static void RoofShortcuts(LevelMap map)
    {
        const int N = LevelMap.Samples;
        // Each roof laid over the tunnel's water as the rock now stands (the slopes were softened since it was bored): over
        // each open cell of it, from the wall on one side across to the wall on the other — their heights, a little
        // past their edges, met exactly where the water meets the rock, with a gentle arch between — so it follows the
        // water's own craggy outline and reads as one mass of rock with the walls. Where the water is wider than a roof
        // can span (into a chamber, a lake), the tunnel is open to the sky: its mouths. Rock keeps its own surface.
        Array.Fill(map.Roof, float.NaN);
        foreach (var c in map.Corridors)
        {
            if (!c.Roofed || c.Points.Count < 2) continue;
            var cells = new List<int>();
            var centre = new Dictionary<int, float>();
            float span = Bored(c);
            var pts = c.Points;
            int x0 = Math.Max(0, (int)(pts.Min(p => p.X) - span)), x1 = Math.Min(N - 1, (int)(pts.Max(p => p.X) + span) + 1);
            int y0 = Math.Max(0, (int)(pts.Min(p => p.Y) - span)), y1 = Math.Min(N - 1, (int)(pts.Max(p => p.Y) + span) + 1);
            // How far to the rock from q along d (metres), or −1 past the widest a roof spans.
            float ToRock(Vector2 q, Vector2 d)
            {
                for (float t = 0f; t <= RoofSpan(c) + 1.5f; t += 0.5f)
                    if (map.HeightAt(q + d * t) >= 0.4f) return t;
                return -1f;
            }
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = y * N + x;
                if (!float.IsNaN(map.Lid[i]) || map[x, y] > 0.3f) continue;
                var q = new Vector2(x, y);
                var near = NearestPoint(pts, q, out var dir);
                if (Vector2.Distance(near, q) > span) continue;
                var side = Geo.Perp(dir);
                float la = ToRock(q, side), lb = ToRock(q, -side);
                // The mouth's edge ragged, not cut square: the widest span wavers by a couple of metres along it.
                float widest = RoofSpan(c) - 1f - 1.5f * TerrainNoise.Fbm(x * 0.4f, y * 0.4f, (uint)map.Seed ^ 0x2F00u, 2);
                if (la < 0f || lb < 0f || la + lb > widest) continue;
                float ha = map.HeightAt(q + side * (la + 1.5f)), hb = map.HeightAt(q - side * (lb + 1.5f));
                float t = lb / MathF.Max(la + lb, 0.01f);
                map.Roof[i] = MathF.Max(PassRoofLow, MathUtil.Lerp(hb, ha, t) + 0.5f * MathF.Sin(MathF.PI * t));
                cells.Add(i);
                centre[i] = MathF.Sin(MathF.PI * t);
            }
            var was = cells.ToDictionary(k => k, k => map.Roof[k]);
            foreach (int k in cells)
            {
                float sum = 0f;
                int n = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (was.TryGetValue(k + dy * N + dx, out float v)) { sum += v; n++; }
                map.Roof[k] = MathF.Max(PassRoofLow, sum / n);
            }
            WearMouths(map, cells, centre);
        }
        CleanRoofs(map);
        // Every shortcut is a tunnel: a pass the rock could not roof (it ran out into water too wide) is filled back in,
        // and so are the spares past the most a level has (the first bored, through the highest rock, are kept).
        bool Over(Vector2 p)
        {
            int x = (int)MathF.Round(p.X), y = (int)MathF.Round(p.Y);
            return x >= 0 && y >= 0 && x < N && y < N && !float.IsNaN(map.RoofAt(x, y));
        }
        // Roofed in its middle, and somewhere along it for its canopy pieces (laid at its segments' middles).
        bool Roofed(Corridor c) => Over(c.Points[c.Points.Count / 2])
            && Enumerable.Range(1, c.Points.Count - 1).Any(i => Over((c.Points[i - 1] + c.Points[i]) * 0.5f));
        // (Filling one back can take a neighbour's roof with it: again, until none changes.)
        for (bool again = true; again;)
        {
            again = false;
            int kept = map.Corridors.Count(c => c.Kind == CorridorKind.Pass && Roofed(c));
            for (int ci = map.Corridors.Count - 1; ci >= 0; ci--)
            {
                var c = map.Corridors[ci];
                if (c.Kind != CorridorKind.Pass || c.Bored is null) continue;
                bool roofed = Roofed(c);
                if (!roofed || kept > PassesMax)
                {
                    if (roofed) kept--;
                    foreach (var (i, was) in c.Bored)
                    {
                        map.Heights[i] = MathF.Max(map.Heights[i], Math.Min(was, ReefTerrain.HMax));
                        map.Roof[i] = float.NaN;
                    }
                    map.Corridors.RemoveAt(ci);
                    again = true;
                    foreach (var canopy in map.Canopies)
                        if (canopy.Corridor > ci) canopy.Corridor--;
                }
            }
            if (again) CleanRoofs(map);
        }
        foreach (var c in map.Corridors) c.Bored = null;
        // Each tunnel's roof, as canopy pieces along it (they fade as one when she is near).
        for (int ci = 0; ci < map.Corridors.Count; ci++)
        {
            var c = map.Corridors[ci];
            if (!c.Roofed) continue;
            for (int i = 1; i < c.Points.Count; i++)
            {
                Vector2 a = c.Points[i - 1], b = c.Points[i];
                var mid = (a + b) * 0.5f;
                int x = (int)MathF.Round(mid.X), y = (int)MathF.Round(mid.Y);
                if (x < 0 || y < 0 || x >= N || y >= N || float.IsNaN(map.RoofAt(x, y))) continue;
                map.Canopies.Add(new Canopy
                {
                    Kind = CanopyKind.TunnelRoof,
                    Center = mid,
                    Axis = Geo.Normalize(b - a, Vector2.UnitX),
                    HalfLength = Vector2.Distance(a, b) * 0.5f + 0.5f,
                    HalfWidth = c.HalfWidth + 1.5f,
                    Bottom = 1.5f,
                    Top = map.RoofAt(x, y),
                    Corridor = ci,
                });
            }
        }
    }

    /// <summary>
    /// A tunnel's mouths worn back as water wears a cave's: from the open water it opens into, the roof recedes
    /// MouthWear metres, most in its middle (so the mouth is an arch, not a square cut) and by a wavering amount along its
    /// edge (up to MouthRagged more), its walls' edges untouched.
    /// </summary>
    static void WearMouths(LevelMap map, List<int> cells, Dictionary<int, float> centre)
    {
        const int N = LevelMap.Samples;
        const float MouthWear = 1f, MouthArch = 2.5f, MouthRagged = 3f;
        var roofed = new HashSet<int>(cells.Where(k => !float.IsNaN(map.Roof[k])));
        // How far in from the open water each roof cell lies (8-way steps over the roof).
        var depth = new Dictionary<int, float>();
        var queue = new Queue<int>();
        foreach (int k in roofed)
        {
            int x = k % N, y = k / N;
            for (int dy = -1; dy <= 1 && !depth.ContainsKey(k); dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= N || ny >= N) continue;
                int j = ny * N + nx;
                if (roofed.Contains(j) || map.Heights[j] > 0.3f || !float.IsNaN(map.Lid[j])) continue;
                depth[k] = 1f;
                queue.Enqueue(k);
                break;
            }
        }
        while (queue.Count > 0)
        {
            int k = queue.Dequeue();
            int x = k % N, y = k / N;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int j = (y + dy) * N + x + dx;
                if ((dx == 0 && dy == 0) || !roofed.Contains(j)) continue;
                float d = depth[k] + (dx != 0 && dy != 0 ? 1.41f : 1f);
                if (depth.TryGetValue(j, out float had) && had <= d) continue;
                depth[j] = d;
                queue.Enqueue(j);
            }
        }
        foreach (var (k, d) in depth)
        {
            int x = k % N, y = k / N;
            float wear = MouthWear + MouthArch * centre.GetValueOrDefault(k) + MouthRagged * (0.5f + 0.5f * TerrainNoise.Fbm(x * 0.55f, y * 0.55f, (uint)map.Seed ^ 0x3B10u, 2));
            if (d <= wear) map.Roof[k] = float.NaN;
        }
    }

    /// <summary>A roof's least piece (grid cells): smaller scraps of roof are dropped, never left hanging.</summary>
    const int RoofPieceMin = 12;

    /// <summary>
    /// The roofs made whole: a roof cell with fewer than three roofed neighbours goes (twice over, so ragged edges
    /// round off), then every separate piece under <see cref="RoofPieceMin"/> cells.
    /// </summary>
    static void CleanRoofs(LevelMap map)
    {
        const int N = LevelMap.Samples;
        bool Roofed(int i) => !float.IsNaN(map.Roof[i]);
        for (int pass = 0; pass < 2; pass++)
        {
            var drop = new List<int>();
            for (int y = 1; y < N - 1; y++)
            for (int x = 1; x < N - 1; x++)
            {
                int i = y * N + x;
                if (!Roofed(i)) continue;
                int n = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if ((dx != 0 || dy != 0) && Roofed(i + dy * N + dx)) n++;
                if (n < 3) drop.Add(i);
            }
            foreach (int i in drop) map.Roof[i] = float.NaN;
        }
        var seen = new bool[N * N];
        for (int start = 0; start < N * N; start++)
        {
            if (seen[start] || !Roofed(start)) continue;
            var piece = new List<int>();
            var stack = new Stack<int>();
            stack.Push(start);
            seen[start] = true;
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                piece.Add(i);
                int x = i % N, y = i / N;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= N || ny >= N) continue;
                    int j = ny * N + nx;
                    if (seen[j] || !Roofed(j)) continue;
                    seen[j] = true;
                    stack.Push(j);
                }
            }
            if (piece.Count < RoofPieceMin) foreach (int i in piece) map.Roof[i] = float.NaN;
        }
    }

    /// <summary>Every height back inside the prototype's bounds (the shaft's bottom…+14), roofs included.</summary>
    static void ClampHeights(LevelMap map)
    {
        for (int i = 0; i < map.Heights.Length; i++)
        {
            map.Heights[i] = Math.Clamp(map.Heights[i], LevelMap.ShaftBottom, ReefTerrain.HMax);
            if (!float.IsNaN(map.Lid[i])) map.Lid[i] = Math.Clamp(map.Lid[i], ReefTerrain.HMin, ReefTerrain.HMax);
            if (!float.IsNaN(map.Roof[i])) map.Roof[i] = Math.Clamp(map.Roof[i], ReefTerrain.HMin, ReefTerrain.HMax);
        }
    }


    /// <summary>
    /// The shaft down to the next level (DESIGN-TOPDOWN §4.6): in a blue hole, a disc in the middle of the clearing; on a
    /// boss level, the Crack, a fissure across the middle of the arena. Its floor is the next level's seabed.
    /// </summary>
    static void CutShaft(LevelMap map, Rng rng)
    {
        var exit = map.Exit.Position;
        Shaft shaft;
        if (map.HasBoss)
        {
            float angle = rng.Range(0f, MathF.PI);
            shaft = new Shaft(exit, new Vector2(MathF.Cos(angle), MathF.Sin(angle)), rng.Range(7f, 9f), rng.Range(2.5f, 3.5f));
        }
        else
        {
            SinkExitChamber(map);
            return;
        }
        map.Shaft = shaft;
        // The lip rounds over into a sheer drop: full depth a metre and a half inside the rim, so the rim is smooth
        // rather than stepped cell by cell.
        int r = (int)MathF.Ceiling(shaft.HalfLength) + 2;
        for (int y = (int)exit.Y - r; y <= (int)exit.Y + r; y++)
        for (int x = (int)exit.X - r; x <= (int)exit.X + r; x++)
        {
            Vector2 d = new Vector2(x, y) - shaft.Center;
            float a = Vector2.Dot(d, shaft.Axis) / shaft.HalfLength, b = Vector2.Dot(d, Geo.Perp(shaft.Axis)) / shaft.HalfWidth;
            float beyond = (MathF.Sqrt(a * a + b * b) - 1f) * shaft.HalfWidth;
            float w = 1f - Smooth(-1.5f, 0.3f, beyond);
            if (w > 0f) map[x, y] = MathUtil.Lerp(map[x, y], LevelMap.ShaftBottom, w);
        }
    }

    /// <summary>
    /// The way down on an ordinary level: the exit's whole chamber sinks. Its floor slopes down from every wall round
    /// it — the deeper the further from the rock, so the hollow follows the chamber's own craggy outline, not a circle —
    /// to just over the shaft's floor in its middle, where the chamber is widest. Its tunnels' mouths ease into the slope. The exit
    /// is moved to that middle, and only within DiveRadius of it can she dive.
    /// </summary>
    static void SinkExitChamber(LevelMap map)
    {
        const int N = LevelMap.Samples;
        var exit = map.Exit.Position;
        float room = map.Chambers.Count > 0 ? map.Chambers.OrderBy(c => Vector2.Distance(c.At, exit)).First().Radius : ExitChamber;
        // How far each point of the water is from the rock round it.
        var toRock = Chamfer(i => map.Heights[i] > 0f);
        // The chamber: the water within its radius (and a few metres more, for its craggy edge), its tunnels fading out.
        float reach = room + ExitMouth;
        int x0 = Math.Max(0, (int)(exit.X - reach)), x1 = Math.Min(N - 1, (int)(exit.X + reach) + 1);
        int y0 = Math.Max(0, (int)(exit.Y - reach)), y1 = Math.Min(N - 1, (int)(exit.Y + reach) + 1);
        float widest = 0f;
        Vector2 middle = exit;
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            int i = y * N + x;
            float d = toRock[i];
            // Its middle: as far from the rock as the chamber gets (the nearer the exit, of ties).
            if (map.Heights[i] <= 0f && Vector2.Distance(new Vector2(x, y), exit) <= room
                && (d > widest + 0.01f || (MathF.Abs(d - widest) <= 0.01f && Vector2.Distance(new Vector2(x, y), exit) < Vector2.Distance(middle, exit))))
            {
                widest = d;
                middle = new Vector2(x, y);
            }
        }
        widest = MathF.Max(widest, 2f);
        var sunk = new List<int>();
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            int i = y * N + x;
            if (map.Heights[i] > 0f) continue;
            var q = new Vector2(x, y);
            float inside = 1f - Smooth(room - 1f, room + ExitMouth, Vector2.Distance(q, exit));
            if (inside <= 0f) continue;
            // Down from the walls, levelling off toward the middle: the walls go on down into it (the slope starts
            // WallRunsOn metres inside the rock), so no shelf of shallow sand rings it at their feet.
            float t = MathUtil.Clamp01((toRock[i] + WallRunsOn) / (widest + WallRunsOn));
            float slope = 1f - (1f - t) * (1f - t);
            float floor = MathUtil.Lerp(map.Heights[i], LevelMap.ShaftBottom + BowlLift + MiddleDip, slope * inside);
            map.Heights[i] = MathF.Min(map.Heights[i], floor);
            sunk.Add(i);
        }
        // Smoothed over the water (the distances step in eighths of a turn: no facets), the walls left as they stand.
        for (int pass = 0; pass < 3; pass++)
        {
            var was = sunk.ToDictionary(k => k, k => map.Heights[k]);
            foreach (int k in sunk)
            {
                float sum = 0f;
                int n = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int j = k + dy * N + dx;
                    float h = was.TryGetValue(j, out float v) ? v : map.Heights[j];
                    if (h > 0f) continue;
                    sum += h;
                    n++;
                }
                map.Heights[k] = MathF.Min(0f, sum / n);
            }
        }
        // The very middle the deepest point of the level: a last, faint dip (too shallow to show).
        foreach (int k in sunk)
        {
            float dip = 1f - Smooth(0f, DiveRadius - 0.5f, Vector2.Distance(new Vector2(k % N, k / N), middle));
            if (dip > 0f) map.Heights[k] = MathF.Min(map.Heights[k], LevelMap.ShaftBottom + BowlLift + MiddleDip * (1f - dip));
        }
        map.Shaft = new Shaft(middle, Vector2.UnitX, DiveRadius, DiveRadius);
        map.Exit.Position = middle;
        // The tunnels that ended at the exit end in its middle now (inside the same chamber).
        foreach (var c in map.Corridors)
            for (int k = 0; k < c.Points.Count; k++)
                if (c.Points[k] == exit) c.Points[k] = middle;
    }

    /// <summary>A minimum with a rounded crease (no flat clamp where the two meet).</summary>
    static float SoftMin(float a, float b, float k)
    {
        float t = MathUtil.Clamp01(0.5f + 0.5f * (b - a) / k);
        return MathUtil.Lerp(b, a, t) - k * t * (1f - t);
    }

    /// <summary>Lowers the heightfield to a cave floor under the swim level inside a disc (soft edge).</summary>
    static void CarveDisc(LevelMap map, Vector2 c, float r)
    {
        int x0 = Math.Max(0, (int)(c.X - r - 2)), x1 = Math.Min(LevelMap.Size, (int)(c.X + r + 2));
        int y0 = Math.Max(0, (int)(c.Y - r - 2)), y1 = Math.Min(LevelMap.Size, (int)(c.Y + r + 2));
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), c);
            float k = 1f - Smooth(r - 0.5f, r + 1.5f, d);
            if (k > 0f) map[x, y] = MathF.Min(map[x, y], MathUtil.Lerp(map[x, y], -CaveFloor, k));
        }
    }

    /// <summary>Cuts a channel under the swim level along a line of the given width (soft edge).</summary>
    /// <summary>
    /// Raises a peak over a cave where the rock is lower than it, with the prototype's brush: a cosine dome summiting at
    /// <paramref name="peak"/> (with a little noise) over the chambers, kept off every open place, including the spur
    /// that leads to the cave's mouth.
    /// </summary>
    static void RaiseCaveMountain(LevelMap map, CaveSite cave, float peak)
    {
        float reach = 18f;
        float minX = cave.Chambers.Min(c => c.Center.X) - reach, maxX = cave.Chambers.Max(c => c.Center.X) + reach;
        float minY = cave.Chambers.Min(c => c.Center.Y) - reach, maxY = cave.Chambers.Max(c => c.Center.Y) + reach;
        uint seed = (uint)map.Seed ^ 0x51ED27u;
        for (int y = Math.Max(0, (int)minY); y <= Math.Min(LevelMap.Size, (int)maxY); y++)
        for (int x = Math.Max(0, (int)minX); x <= Math.Min(LevelMap.Size, (int)maxX); x++)
        {
            var p = new Vector2(x, y);
            // The prototype's raising brush: a cosine dome over the chambers, summit at the peak.
            float d = cave.Chambers.Min(c => Vector2.Distance(p, c.Center) - c.Radius * 0.35f);
            float w = ReefTerrain.Falloff(MathF.Max(d, 0f) / reach);
            float fade = Smooth(0f, 3f, DistanceToOpen(map, p));
            float top = peak + 0.8f * TerrainNoise.Perlin(x / 6f, y / 6f, seed);
            // Never into the water (the cave's craggy edge strays past its shapes).
            if (w * fade <= 0f || top <= map[x, y] || map[x, y] <= LevelMap.BlockHeight) continue;
            map[x, y] += w * fade * (top - map[x, y]);
        }
    }

    /// <summary>Keeps the rock surface over a cave's chambers and tunnels (before they are hollowed out) as its roof.</summary>
    static void RecordLid(LevelMap map, CaveSite cave)
    {
        float margin = 2f;
        float minX = cave.Chambers.Min(c => c.Center.X - c.Radius) - margin, maxX = cave.Chambers.Max(c => c.Center.X + c.Radius) + margin;
        float minY = cave.Chambers.Min(c => c.Center.Y - c.Radius) - margin, maxY = cave.Chambers.Max(c => c.Center.Y + c.Radius) + margin;
        for (int y = Math.Max(0, (int)minY); y <= Math.Min(LevelMap.Size, (int)maxY + 1); y++)
        for (int x = Math.Max(0, (int)minX); x <= Math.Min(LevelMap.Size, (int)maxX + 1); x++)
        {
            var p = new Vector2(x, y);
            bool over = cave.Chambers.Any(c => Vector2.Distance(p, c.Center) < c.Radius + margin);
            for (int i = 1; i < cave.Chambers.Count && !over; i++)
                over = Geo.SegmentDistance(p, cave.Chambers[i - 1].Center, cave.Chambers[i].Center) < 2f + FlatMargin + 3f;
            if (over && float.IsNaN(map.LidAt(x, y))) map.Lid[y * LevelMap.Samples + x] = map[x, y];
        }
    }

    /// <summary>A gentle undulation (0–1) for carved floors, so no path, pass or trench bottom is ever flat.</summary>
    static float FloorDip(float x, float y, uint seed) => 0.5f + 0.5f * TerrainNoise.Fbm(x / 16f, y / 16f, seed, 2);

    /// <summary>A pass's line: a gentle curve from a to b (its middle pushed up to a fifth of its length aside), a point a metre.</summary>
    /// <summary>Whether a pass's middle stretch (its middle half) runs through rock: under it, and either side past what is bored.</summary>
    static bool InRock(LevelMap map, Corridor pass)
    {
        var pts = pass.Points;
        float side = Bored(pass) + 0.5f;
        for (int i = pts.Count / 4; i <= pts.Count * 3 / 4; i++)
        {
            var n = Geo.Perp(Geo.Normalize(pts[Math.Min(i + 1, pts.Count - 1)] - pts[Math.Max(i - 1, 0)], Vector2.UnitX));
            if (map.HeightAt(pts[i]) <= LevelMap.BlockHeight + 0.5f
                || map.HeightAt(pts[i] + n * side) <= LevelMap.BlockHeight + 0.5f
                || map.HeightAt(pts[i] - n * side) <= LevelMap.BlockHeight + 0.5f) return false;
        }
        return true;
    }

    static List<Vector2> Winding(Rng rng, Vector2 a, Vector2 b)
    {
        Vector2 d = b - a;
        float l = d.Length();
        Vector2 c = (a + b) * 0.5f + Geo.Perp(d / l) * rng.Range(-0.2f, 0.2f) * l;
        int n = Math.Max(4, (int)MathF.Ceiling(l));
        var points = new List<Vector2>(n + 1);
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n, u = 1f - t;
            points.Add(u * u * a + 2f * u * t * c + t * t * b);
        }
        return points;
    }

    /// <summary>
    /// Pass tunnels' roofs: only where the ridge stood at least PassRoofMin over the swim level (so its mouths, where it
    /// meets the open water, stay open), and never lower than PassRoofLow over it.
    /// </summary>
    const float PassRoofMin = 1.5f, PassRoofLow = 3f;
    /// <summary>
    /// The widest water a tunnel's roof spans, wall to wall (metres, give or take 1.5 along it): the tunnel as bored, its
    /// widest swell and its sloping walls, and a metre over. Wider water (a chamber, a lake) is open to the sky.
    /// </summary>
    static float RoofSpan(Corridor c) => 2f * Bored(c) + 1f;

    /// <summary>How far out from its line a pass is bored, at its widest (metres): past it, the rock stands whole.</summary>
    static float Bored(Corridor c) => c.HalfWidth * 1.25f + PassMargin + PassEdge;
    /// <summary>A pass is bored narrow: its floor this far past its half width, its walls rising over PassEdge metres.</summary>
    const float PassMargin = 0.5f, PassEdge = 2f;

    /// <summary>
    /// A shortcut tunnel (a pass) bored through the rock as water would wear it: along its
    /// curve, its width breathing (±25%) and its walls rough (a metre of noise), its floor down to the pass floor. Where
    /// the rock stood over it, the rock as it stood stays over it as its roof (<see cref="LevelMap.Roof"/>); where it
    /// breaks into open water, its mouths stay open.
    /// </summary>
    static void CarvePass(LevelMap map, Corridor pass, Rng rng)
    {
        float hw = pass.HalfWidth, phase = rng.Range(0f, 10f);
        uint seed = (uint)(rng.NextU64() >> 32);
        var pts = pass.Points;
        float reach = hw * 1.25f + PassMargin + PassEdge + 1f;
        int x0 = Math.Max(0, (int)(pts.Min(p => p.X) - reach)), x1 = Math.Min(LevelMap.Size, (int)(pts.Max(p => p.X) + reach) + 1);
        int y0 = Math.Max(0, (int)(pts.Min(p => p.Y) - reach)), y1 = Math.Min(LevelMap.Size, (int)(pts.Max(p => p.Y) + reach) + 1);
        var before = new Dictionary<int, float>();
        pass.Bored = new Dictionary<int, float>();
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            var q = new Vector2(x, y);
            float d = float.MaxValue;
            int at = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                float di = Geo.SegmentDistance(q, pts[i - 1], pts[i]);
                if (di < d) { d = di; at = i; }
            }
            float half = hw * (1f + 0.25f * MathF.Sin(phase + at * 0.45f)) + TerrainNoise.Fbm(x * 0.35f, y * 0.35f, seed, 2) * 0.5f;
            float k = 1f - Smooth(half + PassMargin, half + PassMargin + PassEdge, d);
            if (k <= 0f) continue;
            float floor = -PassFloor - 1.2f * FloorDip(x, y, (uint)map.Seed ^ 0x7A55u);
            int i0 = y * LevelMap.Samples + x;
            float was = map.Heights[i0];
            map.Heights[i0] = MathF.Min(was, MathUtil.Lerp(was, floor, k));
            if (map.Heights[i0] < was) pass.Bored.TryAdd(i0, was);
            if (d < half + 1f && was >= PassRoofMin && float.IsNaN(map.Lid[i0])) before[i0] = was;
        }
        foreach (var (i, was) in before) map.Roof[i] = MathF.Max(was, PassRoofLow);
    }

    static void CarveLine(LevelMap map, Vector2 a, Vector2 b, float width)
    {
        float hw = width * 0.5f;
        // Bounds reach past the soft edge (hw + margin + 3), so the cut never ends in a square step.
        float reach = hw + FlatMargin + 4f;
        int x0 = Math.Max(0, (int)(MathF.Min(a.X, b.X) - reach)), x1 = Math.Min(LevelMap.Size, (int)(MathF.Max(a.X, b.X) + reach) + 1);
        int y0 = Math.Max(0, (int)(MathF.Min(a.Y, b.Y) - reach)), y1 = Math.Min(LevelMap.Size, (int)(MathF.Max(a.Y, b.Y) + reach) + 1);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            float d = Geo.SegmentDistance(new Vector2(x, y), a, b);
            float k = 1f - Smooth(hw + FlatMargin, hw + FlatMargin + 3f, d);
            float floor = -PassFloor - 1.2f * FloorDip(x, y, (uint)map.Seed ^ 0x7A55u);
            if (k > 0f) map[x, y] = MathF.Min(map[x, y], MathUtil.Lerp(map[x, y], floor, k));
        }
    }

    // ───────────────────────── stage 4: arches, caves, passes ─────────────────────────

    static string? BuildFeatures(LevelMap map, Rng rng, Dictionary<int, Vector2> spurDirs, WallStrata strata)
    {

        // Caves: every special room gets one, carved into the ridge behind its spur.
        for (int pi = 0; pi < map.Pois.Count; pi++)
        {
            var poi = map.Pois[pi];
            if (!Poi.IsCaveHosted(poi.Kind)) continue;
            Vector2 facing = spurDirs[pi];
            if (poi.Kind == PoiKind.CurseDen)
            {
                if (BuildTunnel(map, rng, poi, pi, facing) is { } why) return why;
                continue;
            }
            var cave = new CaveSite
            {
                Kind = poi.Kind is PoiKind.TreasureCave or PoiKind.CurseDen ? CaveKind.MultiChamber : CaveKind.Pocket,
                Poi = pi,
                Facing = facing,
                Mouth = poi.Position + facing * poi.Radius,
            };
            cave.Chambers.Add((poi.Position, poi.Radius));
            if (cave.Kind == CaveKind.MultiChamber)
            {
                // Deeper chambers lead back into the ridge, away from the mouth.
                Vector2 side = Geo.Perp(facing) * (rng.NextFloat() < 0.5f ? -1f : 1f);
                float r2 = rng.Range(5f, 6f), r3 = rng.Range(4f, 5f);
                bool third = rng.NextFloat() < 0.5f;
                // Try a few directions back into the ridge until the chambers clear every corridor.
                foreach (float turn in new[] { 0f, 0.5f, -0.5f, 1f, -1f, 1.5f, -1.5f, 2f, -2f })
                {
                    Vector2 back = Rotate(-facing, turn);
                    Vector2 second = poi.Position + back * (poi.Radius + 4.5f);
                    var extra = new List<(Vector2, float)> { (second, r2) };
                    if (third) extra.Add((second + Rotate(side, turn) * 9f, r3));
                    if (extra.All(ch => ChamberClear(map, ch.Item1, ch.Item2, pi)))
                    {
                        cave.Chambers.AddRange(extra);
                        break;
                    }
                }
                // Where the ridge behind is too thin for more chambers, the room is a single pocket.
                if (cave.Chambers.Count == 1) cave.Kind = CaveKind.Pocket;
            }
            foreach (var (c, r) in cave.Chambers)
            {
                if (!Inside(c, Rim + r + 2f)) return $"the {poi.Kind} cave runs into the rim";
                foreach (var corridor in map.Corridors)
                    if (corridor.Poi != pi && corridor.DistanceToCentre(c) < r + corridor.HalfWidth + 1.5f)
                        return $"the {poi.Kind} cave breaks into a corridor";
            }
            // A mountain stands over every cave: where the rock is too low, a peak rises with the terrain's own slopes.
            RaiseCaveMountain(map, cave, rng.Range(8f, 11f));
            // Its surface is kept as the cave's roof, then the chambers are hollowed out underneath.
            RecordLid(map, cave);
            foreach (var (c, r) in cave.Chambers)
            {
                CarveDisc(map, c, r);
                map.Canopies.Add(new Canopy
                {
                    Kind = CanopyKind.CaveRoof,
                    Center = c,
                    Radius = r + 1f,
                    Bottom = rng.Range(4.5f, 5.5f),
                    Top = rng.Range(9f, 11f),
                });
            }
            for (int i = 1; i < cave.Chambers.Count; i++)
                CarveLine(map, cave.Chambers[i - 1].Center, cave.Chambers[i].Center, 4f);
            poi.Cave = map.Caves.Count;
            map.Caves.Add(cave);
            if (poi.Kind == PoiKind.Secret) map.WeakRocks.Add(new WeakRock(cave.Mouth + facing * 1.5f, 2.5f, poi.Cave));
        }

        // Passes, the level's shortcuts: tunnels worn through the rock between two parts of the cave that are close as the
        // fish swims but far apart by water — from tunnels and from chambers' rims.
        var samples = new List<Vector2>();
        foreach (var corridor in map.Corridors)
        {
            if (corridor.Kind is not (CorridorKind.Main or CorridorKind.Side) || corridor.Roofed) continue;
            for (int i = 0; i < corridor.Points.Count; i += 2)
            {
                Vector2 p = corridor.Points[i];
                if (NearEnds(map, p, 0f) || map.Plazas.Any(z => Vector2.Distance(z.Center, p) < z.Radius + 1f)) continue;
                samples.Add(p);
            }
        }
        foreach (var chamber in map.Chambers)
            for (int k = 0; k < 8; k++)
            {
                float a = k * MathF.Tau / 8f;
                var p = chamber.At + new Vector2(MathF.Cos(a), MathF.Sin(a)) * MathF.Max(chamber.Radius - 1.5f, 1f);
                if (map.HeightAt(p) <= LevelMap.BlockHeight && !NearEnds(map, p, 0f)) samples.Add(p);
            }
        var passCandidates = new List<(Vector2 A, Vector2 B, float Saddle, float D)>();
        for (int i = 0; i < samples.Count; i++)
        for (int j = i + 1; j < samples.Count; j++)
        {
            Vector2 a = samples[i], b = samples[j];
            float d = Vector2.Distance(a, b);
            if (d < PassMin || d > PassMax) continue;
            // Through the rock: its middle stretch all rock, clear of the caves and the open places.
            float saddle = 0f;
            bool ok = true;
            for (float s = 0f; s <= d && ok; s += 1f)
            {
                Vector2 q = Vector2.Lerp(a, b, s / d);
                float h = map.HeightAt(q);
                saddle = MathF.Max(saddle, h);
                // (High enough there to roof it: its middle is a tunnel.)
                if (s > d * 0.25f && s < d * 0.75f && h < PassRoofMin + 0.5f) ok = false;
                foreach (var cave in map.Caves)
                    foreach (var (cc, cr) in cave.Chambers)
                        if (Vector2.Distance(q, cc) < cr + 4f) ok = false;
                foreach (var poi in map.Pois)
                    if (!Poi.IsCaveHosted(poi.Kind) && Vector2.Distance(q, poi.Position) < poi.Radius + 3f) ok = false;
            }
            if (ok && saddle >= 2.5f) passCandidates.Add((a, b, saddle, d));
        }
        // A real shortcut: the way round by water at least PassSaves times as long (measured from each end, as needed).
        var fromSample = new Dictionary<Vector2, float[]>();
        float Water(Vector2 a, Vector2 b)
        {
            if (!fromSample.TryGetValue(a, out var dist)) fromSample[a] = dist = LevelValidator.Distances(map, a);
            return LevelValidator.DistanceAt(dist, b);
        }
        int passes = PassesMin + rng.Int(PassesMax - PassesMin + 1);
        foreach (var cand in passCandidates.OrderByDescending(c => c.Saddle).ThenBy(c => c.A.X).ThenBy(c => c.A.Y))
        {
            if (map.Corridors.Count(c => c.Kind == CorridorKind.Pass) >= passes + PassSpares) break;
            Vector2 mid = (cand.A + cand.B) * 0.5f;
            if (map.Corridors.Where(c => c.Kind == CorridorKind.Pass).Any(c => Vector2.Distance((c.Points[0] + c.Points[^1]) * 0.5f, mid) < 15f)) continue;
            if (Water(cand.A, cand.B) < cand.D * PassSaves) continue;
            // A tunnel worn through the rock: winding a little, its roof the rock as it stood — rock all along its middle
            // stretch, under it and either side past what is bored (never a channel along the open water).
            var pass = new Corridor { Kind = CorridorKind.Pass, Width = rng.Range(3f, 5f), Points = Winding(rng, cand.A, cand.B), Roofed = true };
            for (int bend = 0; bend < 3 && !InRock(map, pass); bend++) pass.Points = Winding(rng, cand.A, cand.B);
            if (!InRock(map, pass)) continue;
            CarvePass(map, pass, rng);
            map.Corridors.Add(pass);
            // The water round has changed: what was measured from before no longer holds.
            fromSample.Clear();
        }

        // Arches: peak pairs flanking a corridor, bridged by a 10–20 m span across it.
        List<(Vector2 A, Vector2 B, int Corridor)> ArchCandidates(float threshold)
        {
            var found = new List<(Vector2 A, Vector2 B, int Corridor)>();
            for (int c = 0; c < map.Corridors.Count; c++)
            {
                var corridor = map.Corridors[c];
                // Over the route's tunnels and the side tunnels (not a room's spur, nor a shortcut: that is roofed).
                if (corridor.Kind is not (CorridorKind.Main or CorridorKind.Side) || corridor.Roofed) continue;
                for (int i = 2; i + 2 < corridor.Points.Count; i += 2)
                {
                    Vector2 p = corridor.Points[i];
                    // Over a tunnel or across a chamber's edge (its footings stand on the walls either side).
                    if (NearEnds(map, p, 4f)) continue;
                    Vector2 tangent = Geo.Normalize(corridor.Points[i + 2] - corridor.Points[i - 2], Vector2.UnitX);
                    Vector2 nrm = Geo.Perp(tangent);
                    float sa = Footing(map, p, nrm, corridor.HalfWidth, threshold), sb = Footing(map, p, -nrm, corridor.HalfWidth, threshold);
                    if (sa < 0f || sb < 0f) continue;
                    float span = sa + sb;
                    if (span < ArchSpanMin || span > ArchSpanMax) continue;
                    found.Add((p + nrm * sa, p - nrm * sb, c));
                }
            }
            return found;
        }
        // Footings stand where the flank is well above the swim level: on the highest rock first, then lower.
        var archCandidates = new List<(Vector2 A, Vector2 B, int Corridor)>();
        // (The walls rise in strata, their first ledge 2.6–4.2 m up: an arch may spring from a ledge.)
        foreach (float threshold in new[] { 5f, 4f, 3f, 2.5f })
            foreach (var cand in ArchCandidates(threshold))
                if (!archCandidates.Any(x => Vector2.Distance((x.A + x.B) * 0.5f, (cand.A + cand.B) * 0.5f) < 4f)) archCandidates.Add(cand);
        // Where too few flanks stand high enough, a pair of rounded rock shoulders rises either side of a corridor to carry one.
        if (archCandidates.Count < ArchesMin && RaiseShoulders(map, rng)) archCandidates.AddRange(ArchCandidates(4f));
        int arches = ArchesMin + rng.Int(ArchesMax - ArchesMin + 1);
        var pool = archCandidates.ToList();
        while (map.Canopies.Count(c => c.Kind == CanopyKind.Arch) < arches && pool.Count > 0)
        {
            var (a, b, c) = pool[rng.Int(pool.Count)];
            pool.RemoveAll(x => Vector2.Distance((x.A + x.B) * 0.5f, (a + b) * 0.5f) < 15f);
            float span = Vector2.Distance(a, b);
            float bottom = rng.Range(5f, 7f);
            map.Canopies.Add(new Canopy
            {
                Kind = CanopyKind.Arch,
                Center = (a + b) * 0.5f,
                Axis = Vector2.Normalize(b - a),
                HalfLength = span * 0.5f,
                HalfWidth = rng.Range(1.8f, 3f),
                Bottom = bottom,
                Top = bottom + rng.Range(2f, 3.5f),
                Corridor = c,
            });
        }

        // Ledge overhangs leaning out over a corridor edge (above the swim band; they never narrow it).
        int overhangs = rng.Int(5);
        for (int tries = 0; tries < 60 && map.Canopies.Count(c => c.Kind == CanopyKind.Overhang) < overhangs; tries++)
        {
            var corridor = map.Corridors[rng.Int(map.Corridors.Count)];
            if (corridor.Kind != CorridorKind.Main || corridor.Points.Count < 6) continue;
            int i = 2 + rng.Int(corridor.Points.Count - 4);
            Vector2 p = corridor.Points[i];
            if (NearEnds(map, p, 6f)) continue;
            Vector2 tangent = Geo.Normalize(corridor.Points[i + 1] - corridor.Points[i - 1], Vector2.UnitX);
            Vector2 nrm = Geo.Perp(tangent) * (rng.NextFloat() < 0.5f ? -1f : 1f);
            if (map.HeightAt(p + nrm * (corridor.HalfWidth + 4f)) < 8f) continue;
            Vector2 at = p + nrm * (corridor.HalfWidth + 0.5f);
            if (map.Canopies.Any(c => Vector2.Distance(c.Center, at) < 15f)) continue;
            // It juts from a ledge where the wall has one at about its height (a stratum's edge), else at 5.5 m.
            float ledge = strata.NearestLedge(5.5f, at.X, at.Y, 2f);
            float bottom = ledge >= 4.5f ? ledge : 5.5f;
            map.Canopies.Add(new Canopy
            {
                Kind = CanopyKind.Overhang,
                Center = at,
                Axis = tangent,
                HalfLength = rng.Range(3f, 5f),
                HalfWidth = rng.Range(1.5f, 2.5f),
                Bottom = bottom,
                Top = bottom + 2f,
                Corridor = map.Corridors.IndexOf(corridor),
            });
        }
        return null;
    }

    /// <summary>Round the start and the way down (and its arena) features keep out.</summary>
    const float EndsKeep = 12f;

    /// <summary>Within reach of the start or the way down (plus a margin): features keep out.</summary>
    static bool NearEnds(LevelMap map, Vector2 p, float margin) =>
        Vector2.Distance(p, map.Start.Position) < MathF.Max(EndsKeep, map.Start.Radius) + margin
        || Vector2.Distance(p, map.Exit.Position) < MathF.Max(EndsKeep, map.Exit.Radius + 9f) + margin;

    /// <summary>The curse den: a winding dead-end tunnel bored into the ridge behind its spur, roofed like any cave.</summary>
    static string? BuildTunnel(LevelMap map, Rng rng, Poi poi, int pi, Vector2 facing)
    {
        var mouth = poi.Position + facing * poi.Radius;
        var (path, length) = TunnelPath(map, rng, mouth, facing, pi);
        if (length < TunnelMin) return $"no room for the curse den's tunnel ({length:0} m)";
        var cave = new CaveSite { Kind = CaveKind.Tunnel, Poi = pi, Facing = facing, Mouth = mouth };
        for (int i = 0; i < path.Count; i++) cave.Chambers.Add((path[i], i == path.Count - 1 ? TunnelEndRadius : TunnelHalfWidth));
        RaiseCaveMountain(map, cave, rng.Range(8f, 11f));
        RecordLid(map, cave);
        for (int i = 1; i < path.Count; i++) CarveTunnel(map, path[i - 1], path[i], TunnelHalfWidth);
        CarveDisc(map, path[^1], TunnelEndRadius - 0.4f);
        for (int i = 0; i < path.Count; i++)
            map.Canopies.Add(new Canopy
            {
                Kind = CanopyKind.CaveRoof,
                Center = path[i],
                Radius = (i == path.Count - 1 ? TunnelEndRadius : TunnelHalfWidth) + 1.6f,
                Bottom = rng.Range(4.5f, 5.5f),
                Top = rng.Range(9f, 11f),
            });
        poi.Cave = map.Caves.Count;
        map.Caves.Add(cave);
        return null;
    }

    /// <summary>
    /// The tunnel's line, from the mouth: a first straight leg back into the ridge, then bends (mostly alternating,
    /// so it snakes) until it is as long as this level's target. Every point keeps clear of the open water (a wall of
    /// rock at least TunnelWall thick), of other caves and of its own earlier passes. Walks that run into something
    /// try other bends; the longest walk wins, if it reaches TunnelMin.
    /// </summary>
    static (List<Vector2> Path, float Length) TunnelPath(LevelMap map, Rng rng, Vector2 mouth, Vector2 facing, int pi)
    {
        float target = rng.Range(TunnelMin + 8f, TunnelMax);
        var best = new List<Vector2> { mouth };
        float bestLength = 0f;
        bool Clear(Vector2 q, List<Vector2> pts)
        {
            if (!Inside(q, Rim + TunnelHalfWidth + 4f)) return false;
            if (DistanceToOpen(map, q, pi) < TunnelHalfWidth + TunnelWall) return false;
            // The cave's craggy water strays past its shapes: rock all round (but at the mouth, where its spur ends).
            if (Vector2.Distance(q, mouth) > TunnelWall + 2f && !RockAround(map, q, TunnelHalfWidth + TunnelWall - 1f)) return false;
            // Its own spur leads in to the mouth: the tunnel may lie straight on behind it, never alongside it.
            foreach (var spur in map.Corridors)
            {
                if (spur.Poi != pi) continue;
                float d = float.MaxValue;
                for (int i = 1; i < spur.Points.Count; i++) d = MathF.Min(d, Geo.SegmentDistance(q, spur.Points[i - 1], spur.Points[i]));
                if (Vector2.Distance(q, mouth) > d + 1.5f && d - spur.HalfWidth < TunnelHalfWidth + TunnelWall) return false;
            }
            foreach (var cave in map.Caves)
                foreach (var (c, r) in cave.Chambers)
                    if (Vector2.Distance(q, c) < r + TunnelHalfWidth + TunnelWall + 2f) return false;
            for (int i = 0; i < map.Pois.Count; i++)
                if (i != pi && Poi.IsCaveHosted(map.Pois[i].Kind) && Vector2.Distance(q, map.Pois[i].Position) < map.Pois[i].Radius + TunnelHalfWidth + TunnelWall + 4f) return false;
            // Its own earlier passes (all but the last few points, which it is leaving).
            for (int i = 0; i < pts.Count - 3; i++)
                if (Vector2.Distance(q, pts[i]) < TunnelHalfWidth * 2f + TunnelWall) return false;
            return true;
        }
        for (int walk = 0; walk < 60 && bestLength < target; walk++)
        {
            var pts = new List<Vector2> { mouth };
            Vector2 dir = -facing;
            float length = 0f, sign = rng.NextFloat() < 0.5f ? 1f : -1f;
            float leg = rng.Range(8f, 10f);
            bool firstLeg = true;
            while (length < target)
            {
                // The bend into this leg: try the one picked, then the others, until the leg's start is clear.
                var turns = new List<float>();
                if (firstLeg) turns.Add(0f);
                else
                {
                    float a = rng.Range(0.55f, 1.25f);
                    sign = rng.NextFloat() < 0.7f ? -sign : sign;
                    turns.AddRange(new[] { a * sign, -a * sign, 0.5f * a * sign, -0.5f * a * sign });
                }
                Vector2? way = null;
                foreach (float t in turns)
                {
                    var d = Rotate(dir, t);
                    if (Clear(pts[^1] + d * TunnelStep, pts) && Clear(pts[^1] + d * TunnelStep * 2f, pts))
                    {
                        way = d;
                        break;
                    }
                }
                if (way is not { } next) break;
                dir = next;
                firstLeg = false;
                int before = pts.Count;
                for (float s = TunnelStep; s <= leg + 1e-3f && length < target; s += TunnelStep)
                {
                    var q = pts[^1] + dir * TunnelStep;
                    if (!Clear(q, pts)) break;
                    pts.Add(q);
                    length += TunnelStep;
                }
                if (pts.Count == before) break;
                leg = rng.Range(TunnelLegMin, TunnelLegMax);
            }
            if (length > bestLength)
            {
                bestLength = length;
                best = pts;
            }
        }
        return (best, bestLength);
    }

    /// <summary>Bores a narrow passage under the swim level along a line: open halfWidth each side, rock just past it.</summary>
    static void CarveTunnel(LevelMap map, Vector2 a, Vector2 b, float halfWidth)
    {
        float reach = halfWidth + 3f;
        int x0 = Math.Max(0, (int)(MathF.Min(a.X, b.X) - reach)), x1 = Math.Min(LevelMap.Size, (int)(MathF.Max(a.X, b.X) + reach) + 1);
        int y0 = Math.Max(0, (int)(MathF.Min(a.Y, b.Y) - reach)), y1 = Math.Min(LevelMap.Size, (int)(MathF.Max(a.Y, b.Y) + reach) + 1);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            float d = Geo.SegmentDistance(new Vector2(x, y), a, b);
            float k = 1f - Smooth(halfWidth, halfWidth + 1.4f, d);
            float floor = -PassFloor - 0.8f * FloorDip(x, y, (uint)map.Seed ^ 0xC0125Eu);
            if (k > 0f) map[x, y] = MathF.Min(map[x, y], MathUtil.Lerp(map[x, y], floor, k));
        }
    }

    /// <summary>Rock (above the swim level) at the centre and on rings out to <paramref name="r"/>.</summary>
    static bool RockAround(LevelMap map, Vector2 c, float r)
    {
        if (map.HeightAt(c) <= LevelMap.BlockHeight) return false;
        foreach (float f in new[] { 0.5f, 1f })
            for (int i = 0; i < 12; i++)
            {
                float a = i * MathF.Tau / 12f;
                if (map.HeightAt(c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r * f) <= LevelMap.BlockHeight) return false;
            }
        return true;
    }

    static Vector2 Rotate(Vector2 v, float a) => new(v.X * MathF.Cos(a) - v.Y * MathF.Sin(a), v.X * MathF.Sin(a) + v.Y * MathF.Cos(a));

    static bool ChamberClear(LevelMap map, Vector2 c, float r, int poi)
    {
        if (!Inside(c, Rim + r + 2f) || !RockAround(map, c, r + 2.5f)) return false;
        foreach (var corridor in map.Corridors)
            if (corridor.Poi != poi && corridor.DistanceToCentre(c) < r + corridor.HalfWidth + 1.5f) return false;
        foreach (var other in map.Pois)
            if (map.Pois.IndexOf(other) != poi && Vector2.Distance(other.Position, c) < other.Radius + r + 3f) return false;
        return true;
    }

    /// <summary>How far from the corridor centre, along dir, the ground first reaches peak height (−1 if not within 12 m of the edge).</summary>
    /// <summary>Raises two smooth rock shoulders facing each other across a corridor, clear of every other open place.</summary>
    static bool RaiseShoulders(LevelMap map, Rng rng)
    {
        var mains = map.Corridors.Where(c => c.Kind == CorridorKind.Main).ToList();
        for (int tries = 0; tries < 200; tries++)
        {
            var c = mains[rng.Int(mains.Count)];
            if (c.Points.Count < 10) continue;
            int i = 4 + rng.Int(c.Points.Count - 8);
            Vector2 p = c.Points[i];
            if (NearEnds(map, p, 8f)) continue;
            if (map.Plazas.Any(z => Vector2.Distance(z.Center, p) < 16f)) continue;
            Vector2 nrm = Geo.Perp(Geo.Normalize(c.Points[i + 2] - c.Points[i - 2], Vector2.UnitX));
            var shoulders = new[] { p + nrm * (c.HalfWidth + ShoulderRadius), p - nrm * (c.HalfWidth + ShoulderRadius) };
            bool clear = shoulders.All(sh =>
                Inside(sh, Rim + ShoulderRadius)
                && map.Corridors.Where(o => o != c).All(o => o.DistanceToCentre(sh) > o.HalfWidth + ShoulderRadius + 1f)
                && map.Pois.All(poi => Vector2.Distance(poi.Position, sh) > poi.Radius + ShoulderRadius + 2f)
                && map.Plazas.All(z => Vector2.Distance(z.Center, sh) > z.Radius + ShoulderRadius + 1f));
            if (!clear) continue;
            // Each shoulder is a short ridge lying along the corridor (an ellipse twice as long as it is wide), not a cone.
            Vector2 along = Geo.Perp(nrm);
            const float reach = ShoulderRadius * 2f;
            foreach (var sh in shoulders)
            {
                int x0 = Math.Max(0, (int)(sh.X - reach - 1)), x1 = Math.Min(LevelMap.Size, (int)(sh.X + reach + 1) + 1);
                int y0 = Math.Max(0, (int)(sh.Y - reach - 1)), y1 = Math.Min(LevelMap.Size, (int)(sh.Y + reach + 1) + 1);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 rel = new Vector2(x, y) - sh;
                    float a = Vector2.Dot(rel, along) / reach, b = Vector2.Dot(rel, nrm) / ShoulderRadius;
                    // Never into open ground: the ridge fades out before any corridor or clearing.
                    float k = ReefTerrain.Falloff(MathF.Sqrt(a * a + b * b)) * Smooth(0f, 2f, DistanceToOpen(map, new Vector2(x, y)));
                    if (k <= 0f || map[x, y] >= ShoulderHeight || map[x, y] <= LevelMap.BlockHeight) continue;
                    map[x, y] += k * (ShoulderHeight - map[x, y]);
                }
            }
            return true;
        }
        return false;
    }

    static float Footing(LevelMap map, Vector2 p, Vector2 dir, float half, float threshold)
    {
        for (float s = half; s <= half + 12f; s += 0.5f)
            if (map.HeightAt(p + dir * s) >= threshold) return s;
        return -1f;
    }

    // ───────────────────────── stage 5: reef & decoration ─────────────────────────

    /// <summary>
    /// Kelp beds: KelpBedsMin–KelpBedsMax a level (KelpDepthMin–KelpDepthMax in the Kelp depth, Depth 2), each a blob
    /// KelpRadiusMin–KelpRadiusMax metres across with a wavering edge, its strands rooted about KelpSpacing apart on the
    /// open floor (thinning toward the edge), each KelpLengthMin–KelpLengthMax long — standing, they reach up past her
    /// plane, so she swims among them and some pass over her. On open water (at least KelpClear metres to the rock at
    /// the middle), kept off the start, the way down and the places, and KelpApart metres apart.
    /// </summary>
    static void PlaceKelp(LevelMap map, Rng rng)
    {
        const int N = LevelMap.Samples;
        int beds = map.Id.Depth == 2 ? KelpDepthMin + rng.Int(KelpDepthMax - KelpDepthMin + 1) : KelpBedsMin + rng.Int(KelpBedsMax - KelpBedsMin + 1);
        var toRock = Chamfer(i => map.Heights[i] > LevelMap.BlockHeight - 0.2f);
        uint seed = (uint)(rng.NextU64() >> 32);
        for (int tries = 0; tries < 600 && map.Kelp.Count < beds; tries++)
        {
            var c = new Vector2(rng.Range(LevelMap.RimWidth + 4f, LevelMap.Size - LevelMap.RimWidth - 4f), rng.Range(LevelMap.RimWidth + 4f, LevelMap.Size - LevelMap.RimWidth - 4f));
            int ci = (int)MathF.Round(c.Y) * N + (int)MathF.Round(c.X);
            if (toRock[ci] < KelpClear || map.HeightAt(c) > LevelMap.BlockHeight - 0.5f) continue;
            float r = rng.Range(KelpRadiusMin, KelpRadiusMax);
            if (Vector2.Distance(c, map.Start.Position) < map.Start.Radius + r + 3f) continue;
            if (Vector2.Distance(c, map.Exit.Position) < map.Exit.Radius + r + 5f || map.Shaft.Contains(c, r + 4f)) continue;
            if (map.Pois.Any(p => p.Kind is not (PoiKind.Start or PoiKind.Exit) && Vector2.Distance(c, p.Position) < p.Radius + r + 2f)) continue;
            if (map.Kelp.Any(k => Vector2.Distance(k.Center, c) < k.Radius + r + KelpApart)) continue;
            var bed = new KelpBed { Center = c, Radius = r };
            for (float y = -r; y <= r; y += KelpSpacing)
            for (float x = -r; x <= r; x += KelpSpacing)
            {
                var p = c + new Vector2(x + rng.Range(-0.45f, 0.45f) * KelpSpacing, y + rng.Range(-0.45f, 0.45f) * KelpSpacing);
                // A wavering outline, thinning toward it.
                float edge = r * (0.8f + 0.3f * TerrainNoise.Fbm(p.X * 0.22f, p.Y * 0.22f, seed, 2));
                float d = Vector2.Distance(p, c) / MathF.Max(edge, 0.5f);
                if (d > 1f || rng.NextFloat() > 1f - 0.65f * d * d) continue;
                // Rooted on the floor, never in the rock.
                if (map.HeightAt(p) > LevelMap.BlockHeight - 0.3f || map.Shaft.Contains(p, 2f)) continue;
                bed.Roots.Add(p);
                bed.Lengths.Add(rng.Range(KelpLengthMin, KelpLengthMax));
            }
            if (bed.Roots.Count >= KelpLeast) map.Kelp.Add(bed);
        }
    }

    public const int KelpBedsMin = 2, KelpBedsMax = 4, KelpDepthMin = 5, KelpDepthMax = 7, KelpLeast = 12;
    public const float KelpRadiusMin = 3f, KelpRadiusMax = 5.5f, KelpSpacing = 0.55f, KelpLengthMin = 3.2f, KelpLengthMax = 4.6f, KelpClear = 2.5f, KelpApart = 10f;

    static void Decorate(LevelMap map, Rng rng, uint seed, WallStrata strata)
    {
        float fanYaw = MathF.Atan2(Current.Y, Current.X);
        for (float y = 1f; y < Size - 1f; y += 2f)
        for (float x = 1f; x < Size - 1f; x += 2f)
        {
            var p = new Vector2(x + rng.Range(0f, 2f), y + rng.Range(0f, 2f));
            float h = map.HeightAt(p);
            float roll = rng.NextFloat();
            float scale = rng.Range(0.7f, 1.4f), yaw = rng.Range(0f, MathF.Tau);
            float zone = 0.5f + 0.5f * Noise(new Vector3(p.X * 0.08f, 3f, p.Y * 0.08f), seed + 31u);
            // The shaft is open water all the way down.
            if (map.Shaft.Contains(p, 1.5f)) continue;
            if (h < -0.5f)
            {
                // The seabed under open water: meadows and sand in the shallows, reef heads and boulders, flora on its slopes.
                float meadow = Noise(new Vector3(p.X * 0.05f, 7f, p.Y * 0.05f), seed + 37u);
                bool slope = map.Gradient(p).Length() > 0.45f;
                if (slope && roll < 0.18f)
                {
                    DecorKind kind = zone > 0.62f ? DecorKind.SeaFan : zone > 0.38f ? DecorKind.SeaRod : DecorKind.TubeSponge;
                    map.Decor.Add(new DecorSpot(kind, p, h, scale, kind == DecorKind.SeaFan ? fanYaw : yaw));
                }
                else if (h < -4f && roll < 0.025f) map.Decor.Add(new DecorSpot(roll < 0.01f ? DecorKind.Bommie : DecorKind.Boulder, p, h, scale, yaw));
                // Meadows and sand lie flat, so only on gentle floor (on a slope they would stack into terraces).
                else if (map.Gradient(p).Length() > 0.25f) { }
                else if (h > -6f && meadow > 0.2f && roll < 0.35f) map.Decor.Add(new DecorSpot(DecorKind.GrassMeadow, p, h, scale, yaw));
                else if (roll < 0.04f) map.Decor.Add(new DecorSpot(DecorKind.SandChannel, p, h, scale, yaw));
            }
            else if (Ledge(map, p, h, strata))
            {
                // A ledge's lip (a stratum's edge, the drop just beyond): fans and sea rods low down, cliff coral higher.
                if (roll < 0.3f)
                {
                    DecorKind kind = h < 6f ? (zone > 0.45f ? DecorKind.SeaFan : DecorKind.SeaRod) : roll < 0.2f ? DecorKind.CliffCoral : DecorKind.EncrustingCoral;
                    map.Decor.Add(new DecorSpot(kind, p, h, scale, kind == DecorKind.SeaFan ? fanYaw : yaw));
                }
            }
            else if (h < 6f)
            {
                // The reef wall where it breaks the swim level.
                if (roll < 0.2f)
                {
                    DecorKind kind = zone > 0.62f ? DecorKind.SeaFan : zone > 0.38f ? DecorKind.SeaRod : DecorKind.TubeSponge;
                    map.Decor.Add(new DecorSpot(kind, p, h, scale, kind == DecorKind.SeaFan ? fanYaw : yaw));
                }
            }
            else
            {
                bool peak = h >= 10f && IsLocalPeak(map, p, h);
                if (peak) map.Decor.Add(new DecorSpot(DecorKind.CrownGarden, p, h, scale, yaw));
                else if (roll < 0.06f) map.Decor.Add(new DecorSpot(DecorKind.CliffCoral, p, h, scale, yaw));
                else if (roll < 0.12f) map.Decor.Add(new DecorSpot(DecorKind.EncrustingCoral, p, h, scale, yaw));
            }
        }
    }

    /// <summary>A ledge's lip: rock at about a stratum's height, nearly level, with a drop of a metre or more just beyond.</summary>
    static bool Ledge(LevelMap map, Vector2 p, float h, WallStrata strata)
    {
        if (h <= LevelMap.BlockHeight || strata.NearestLedge(h, p.X, p.Y, 0.6f) < 0f || map.Gradient(p).Length() > 0.5f) return false;
        for (int a = 0; a < 8; a++)
        {
            var dir = new Vector2(MathF.Cos(a * MathF.PI / 4f), MathF.Sin(a * MathF.PI / 4f));
            if (map.HeightAt(p + dir * 1.5f) < h - 1f) return true;
        }
        return false;
    }

    static bool IsLocalPeak(LevelMap map, Vector2 p, float h)
    {
        for (int a = 0; a < 8; a++)
        {
            float ang = a * MathF.Tau / 8f;
            if (map.HeightAt(p + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * 3f) > h) return false;
        }
        return true;
    }

    /// <summary>Buried coins (8–12, scratched X marks) and sealed pockets (4–6) in the seabed under the paths.</summary>
    static void PlacePickups(LevelMap map, Rng rng)
    {
        var mains = map.Corridors.Where(c => c.Kind == CorridorKind.Main).ToList();
        Vector2 OnFlat(float offset)
        {
            var c = mains[rng.Int(mains.Count)];
            int i = 1 + rng.Int(c.Points.Count - 2);
            Vector2 tangent = Geo.Normalize(c.Points[i + 1] - c.Points[i - 1], Vector2.UnitX);
            return c.Points[i] + Geo.Perp(tangent) * (rng.Range(-1f, 1f) * offset * c.HalfWidth);
        }
        int coins = CoinsMin + rng.Int(CoinsMax - CoinsMin + 1);
        for (int tries = 0; tries < 400 && map.Coins.Count < coins; tries++)
        {
            Vector2 p = OnFlat(0.8f);
            if (map.HeightAt(p) > -PathDepth || map.Coins.Any(c => Vector2.Distance(c.Position, p) < 8f)) continue;
            if (Vector2.Distance(p, map.Start.Position) < 12f || Vector2.Distance(p, map.Exit.Position) < map.Exit.Radius + 2f) continue;
            map.Coins.Add(new BuriedCoins(p, rng.NextFloat() < 0.1f ? 5 : 2 + rng.Int(2)));
        }
        int pockets = PocketsMin + rng.Int(PocketsMax - PocketsMin + 1);
        for (int tries = 0; tries < 400 && map.Pockets.Count < pockets; tries++)
        {
            Vector2 p = OnFlat(0.9f);
            if (map.HeightAt(p) > -PathDepth || map.Pockets.Any(c => Vector2.Distance(c.Position, p) < 12f)) continue;
            if (map.Coins.Any(c => Vector2.Distance(c.Position, p) < 4f)) continue;
            if (Vector2.Distance(p, map.Start.Position) < 15f || Vector2.Distance(p, map.Exit.Position) < map.Exit.Radius + 4f) continue;
            map.Pockets.Add(new SealedPocket(p, 1.6f));
        }
    }

    // ───────────────────────── stage 6: mob spawn rules ─────────────────────────

    static readonly EnemyKind[] Swimmers = { EnemyKind.SpanishDancer, EnemyKind.Pufferling, EnemyKind.Barracuda, EnemyKind.MoonJelly };

    static MoveClass ClassOf(EnemyKind kind) => kind switch
    {
        EnemyKind.Crabby => MoveClass.Walker,
        EnemyKind.SeaUrchin => MoveClass.Clinger,
        EnemyKind.Moray => MoveClass.Burrower,
        _ => MoveClass.Swimmer,
    };

    static void PlaceSpawns(LevelMap map, Rng rng, float menace)
    {
        // Menace (DESIGN-TOPDOWN §6.2): the spawn budget grows ×1.0 → ×1.8 over the depths, and on with every loop.
        float budget = 1f + 0.8f * MathF.Min(menace, 3f);
        int Scaled(int n) => Math.Max(1, (int)MathF.Round(n * budget));

        // Landmark lights are breathing room: ambient spawns keep out of their radius (curse dens excepted).
        var beacons = map.Pois.Where(p => p.Kind is PoiKind.Start or PoiKind.Shop or PoiKind.TreasureCave or PoiKind.Exit).ToList();
        bool NearBeacon(Vector2 p) => beacons.Any(b => Vector2.Distance(b.Position, p) < b.Radius + 12f);

        void Add(SpawnRole role, EnemyKind kind, int count, Vector2 at, int poi = -1, bool trap = false, List<Vector2>? path = null) =>
            map.Spawns.Add(new SpawnEntry { Role = role, Kind = kind, Class = ClassOf(kind), Count = count, Position = at, Poi = poi, Trap = trap, Path = path });

        EnemyKind DenKind() => rng.NextFloat() < 0.75f ? Swimmers[rng.Int(Swimmers.Length)] : EnemyKind.Crabby;

        // Dens in caves (the shop is safe water; the curse den's dark is danger enough) and in pockets along the ridges.
        for (int pi = 0; pi < map.Pois.Count; pi++)
        {
            var poi = map.Pois[pi];
            if (poi.Kind is PoiKind.TreasureCave && poi.Cave >= 0 && map.Caves[poi.Cave].Chambers.Count > 1)
                Add(SpawnRole.Den, DenKind(), Scaled(2 + rng.Int(2)), map.Caves[poi.Cave].Chambers[^1].Center, pi);
        }
        int ridgeDens = 2 + rng.Int(3);
        var mains = map.Corridors.Where(c => c.Kind == CorridorKind.Main).ToList();
        for (int tries = 0; tries < 300 && map.Spawns.Count(s => s.Role == SpawnRole.Den && s.Poi < 0) < ridgeDens; tries++)
        {
            var c = mains[rng.Int(mains.Count)];
            int i = 1 + rng.Int(c.Points.Count - 2);
            Vector2 nrm = Geo.Perp(Geo.Normalize(c.Points[i + 1] - c.Points[i - 1], Vector2.UnitX)) * (rng.NextFloat() < 0.5f ? -1f : 1f);
            Vector2 p = c.Points[i] + nrm * (c.HalfWidth + rng.Range(0.5f, 1.5f));
            if (!map.IsOpen(p) || NearBeacon(p) || map.Spawns.Any(s => Vector2.Distance(s.Position, p) < 20f)) continue;
            if (map.Plazas.Any(z => Vector2.Distance(z.Center, p) < z.Radius + 4f)) continue;
            Add(SpawnRole.Den, DenKind(), Scaled(1 + rng.Int(3)), p);
        }

        // Ambushers in the walls 8–12 m off corridor edges, biased to plazas and arch landings.
        var anchors = map.Plazas.Select(z => z.Center).ToList();
        foreach (var arch in map.Canopies.Where(c => c.Kind == CanopyKind.Arch))
        {
            Vector2 tangent = Geo.Perp(arch.Axis);
            anchors.Add(arch.Center + tangent * (arch.HalfWidth + 3f));
            anchors.Add(arch.Center - tangent * (arch.HalfWidth + 3f));
        }
        int ambushers = 3 + rng.Int(4);
        for (int tries = 0; tries < 300 && map.Spawns.Count(s => s.Role == SpawnRole.Ambush) < ambushers && anchors.Count > 0; tries++)
        {
            Vector2 anchor = anchors[rng.Int(anchors.Count)];
            Vector2 near = NearestOnCorridors(map, anchor, out int ci);
            float half = ci >= 0 ? map.Corridors[ci].HalfWidth : 4f;
            float a = rng.Range(0f, MathF.Tau);
            Vector2 dir = new(MathF.Cos(a), MathF.Sin(a));
            Vector2 p = near + dir * (half + rng.Range(8f, 12f));
            if (!Inside(p, Rim + 1f) || NearBeacon(p) || map.Spawns.Any(s => s.Role == SpawnRole.Ambush && Vector2.Distance(s.Position, p) < 8f)) continue;
            if (map.InsideAnyCorridor(p)) continue;
            Add(SpawnRole.Ambush, rng.NextFloat() < 0.5f ? EnemyKind.SeaUrchin : EnemyKind.Moray, 1, p);
        }

        // Patrols: swimmer packs looping along a corridor stretch between two crossings.
        int patrols = 1 + rng.Int(2);
        for (int tries = 0; tries < 40 && map.Spawns.Count(s => s.Role == SpawnRole.Patrol) < patrols; tries++)
        {
            var c = mains[rng.Int(mains.Count)];
            var stops = new List<int>();
            for (int i = 0; i < c.Points.Count; i++)
                if (map.Plazas.Any(z => Vector2.Distance(z.Center, c.Points[i]) < z.Radius)) stops.Add(i);
            int from, to;
            if (stops.Count >= 2)
            {
                from = stops[0];
                to = stops.First(s => s > from + 4 || s == stops[^1]);
            }
            else
            {
                from = c.Points.Count / 4;
                to = c.Points.Count / 2;
            }
            if (to - from < 4) continue;
            var path = c.Points.GetRange(from, to - from + 1);
            Vector2 mid = path[path.Count / 2];
            if (map.Spawns.Any(s => s.Role == SpawnRole.Patrol && Vector2.Distance(s.Position, mid) < 25f)) continue;
            Add(SpawnRole.Patrol, Swimmers[rng.Int(Swimmers.Length)], Scaled(2 + rng.Int(2)), mid, path: path);
        }

        // Guardians scale with the reward; nests hold their ninja.
        for (int pi = 0; pi < map.Pois.Count; pi++)
        {
            var poi = map.Pois[pi];
            Vector2 mouth = poi.Cave >= 0 ? map.Caves[poi.Cave].Mouth + map.Caves[poi.Cave].Facing * 4f : poi.Position;
            switch (poi.Kind)
            {
                case PoiKind.Shop:
                    Add(SpawnRole.Guardian, Swimmers[rng.Int(Swimmers.Length)], 1, mouth, pi);
                    break;
                case PoiKind.TreasureCave:
                    Add(SpawnRole.Guardian, Swimmers[rng.Int(Swimmers.Length)], Scaled(2 + rng.Int(2)), poi.Position, pi);
                    break;
                case PoiKind.ShellCache:
                    Add(SpawnRole.Guardian, Swimmers[rng.Int(Swimmers.Length)], Scaled(3 + rng.Int(2)), poi.Position, pi);
                    Add(SpawnRole.Guardian, rng.NextFloat() < 0.5f ? EnemyKind.Crabby : EnemyKind.SeaUrchin, Scaled(1 + rng.Int(2)), poi.Position, pi);
                    break;
                case PoiKind.Secret:
                    Add(SpawnRole.Guardian, EnemyKind.SeaUrchin, Scaled(1 + rng.Int(2)), poi.Position, pi, trap: true);
                    Add(SpawnRole.Guardian, EnemyKind.Moray, 1, poi.Position, pi, trap: true);
                    break;
            }
        }
    }
}
