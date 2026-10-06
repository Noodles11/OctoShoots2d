using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using OctoShoots.Core.Run;
using OctoShoots.Core.Sim;
using static OctoShoots.Core.Terrain.SdfMath;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>
/// Builds a <see cref="LevelMap"/> through the six stages of DESIGN-TOPDOWN §4.1, each on its own seeded sub-stream:
/// POIs → path network → topography → arches/caves/passes → decoration → spawn table. A level that fails
/// <see cref="LevelValidator"/> is rebuilt from the next attempt's sub-streams (the inherited retry rule).
/// </summary>
public static class TopDownGenerator
{
    public const int MaxAttempts = 100;
    /// <summary>Retries fit the level into the same ocean sector this many times before a new sector is drawn.</summary>

    const float Size = LevelMap.Size;
    const float Rim = LevelMap.RimWidth;

    // Stage 1
    const float StartRadius = 8f;
    const float RiftRadius = 15f;          // the ~30 m arena disc
    const float RiftExclusion = 40f;       // only the rift inside its ~40 m zone
    const float RiftMinStraight = 122f;    // rejection sampling; the ≥130 m geodesic rule is checked after the paths exist
    const float PoiSpacing = 25f;
    const float CaveSpacing = 30f;
    const float EarlyMin = 20f, EarlyMax = 35f;
    const float ClearingRadius = 6f;       // open POIs (item spawn, ninja nests)
    const float PocketRadius = 6f;
    const float ChamberRadius = 6.5f;

    // Stage 2
    const float CorridorMinWidth = 6f, CorridorMaxWidth = 10f;
    const float SpurWidth = 6f;
    const float PlazaRadius = 7f;          // ~14 m plazas
    const float PlazaMerge = 16f;
    /// <summary>Centre lines of two routes keep at least this far apart away from where they meet, so a ridge stands between them.</summary>
    const float RouteSpacing = 26f;

    // Stage 3
    /// <summary>Open shapes are pinned flat this far past their edge, so bilinear sampling anywhere inside them reads exactly 0.</summary>
    const float FlatMargin = 1.5f;
    /// <summary>The seabed, level −1: canyon floors lie at least this far under the swim level.</summary>
    const float PathDepth = 1f;
    /// <summary>Canyon walls climb from the floor to their top over this many metres (the prototype's cosine brush).</summary>
    const float WallSlope = 3.5f;
    /// <summary>Wall tops: the prototype's terrain, mapped into this range, so plateaus roll rather than lie flat.</summary>
    const float WallTopMin = 4f, WallTopMax = 14f;
    /// <summary>Side canyons: narrower than the routes, branching off them into dead ends and the odd loop.</summary>
    const float SideMinWidth = 4f, SideMaxWidth = 6.5f;
    /// <summary>Rock left standing between two canyons (edge to edge, past their flat margins).</summary>
    const float WallMin = 3.5f;
    /// <summary>Side canyons grow until about this share of the interior is open water (the rest is wall, at most 40%).</summary>
    const float OpenTarget = 0.63f;
    /// <summary>The rift at the heart of the boss arena: the deepest point of the level.</summary>
    const float CrackDepth = 7f;
    /// <summary>Trench floors stay above the rift, the level's deepest point (with their undulation).</summary>
    const float TrenchMin = 3.5f, TrenchMax = 5.5f;
    // Cave floors are the one flat ground (between the chart's contours, so never on a contour line); passes undulate below theirs.
    const float CaveFloor = 3.5f, PassFloor = 2.5f;
    /// <summary>Rounded rock shoulders raised beside a corridor when no flank stands high enough to carry an arch.</summary>
    const float ShoulderRadius = 6f, ShoulderHeight = 8f;

    /// <summary>The prevailing current; sea fans grow broadside to it (inherited flora rule).</summary>
    static readonly Vector2 Current = Vector2.Normalize(new Vector2(1f, 0.35f));

    /// <param name="cosmeticSalt">Test hook: changes only the cosmetic stream (decoration), to prove it never touches gameplay.</param>
    public static LevelMap Generate(RunStreams streams, int depth, int reef, Action<string>? log = null, string cosmeticSalt = "")
    {
        var reasons = new List<string>();
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            // Every attempt starts from the bare seabed at level −1.
            var map = new LevelMap { Seed = streams.Seed.Value, Depth = depth, Reef = reef, Attempt = attempt };
            Array.Fill(map.Heights, -PathDepth);
            // The wall tops come from the reef prototype's terrain for this text (room 1's first try: the seed itself).
            string text = depth == 1 && reef == 1 && attempt == 0 ? streams.Seed.ToString() : $"{streams.Seed} {depth}-{reef}-{attempt}";
            string? problem = Build(map, streams, depth, reef, attempt, cosmeticSalt, text) ?? LevelValidator.Validate(map);
            log?.Invoke($"Top-down level {depth}/{reef} attempt {attempt + 1}: {problem ?? "ok"}");
            if (problem is null) return map;
            reasons.Add(problem);
        }
        throw new InvalidOperationException($"No valid top-down level for depth {depth} reef {reef} after {MaxAttempts} attempts: {string.Join("; ", reasons.Distinct())}");
    }

    /// <summary>
    /// Runs the stages — POIs on the bare seabed, the route canyons between them, the labyrinth of side canyons, the
    /// walls raised around all of it, the boss arena's rift, trenches, then caves, passes, arches, decoration, loot and
    /// spawns — and returns why the attempt failed, or null.
    /// </summary>
    static string? Build(LevelMap map, RunStreams streams, int depth, int reef, int attempt, string cosmeticSalt, string terrainText)
    {
        Rng Stage(string name) => streams.TopDown(depth, reef, attempt, name);
        uint noiseSeed = (uint)(Stage("noise").NextU64() >> 32);

        string? pois = PlacePois(map, Stage("poi"));
        if (pois is not null) return pois;
        var spurDirs = new Dictionary<int, Vector2>();
        string? paths = BuildPaths(map, Stage("paths"), spurDirs);
        if (paths is not null) return paths;
        GrowCanyons(map, Stage("canyons"));
        RaiseWalls(map, terrainText, Stage("topo"));
        string? features = BuildFeatures(map, Stage("features"), spurDirs);
        if (features is not null) return features;
        ClampHeights(map);
        Decorate(map, Stage("cosmetic" + cosmeticSalt), noiseSeed);
        PlacePickups(map, Stage("pickups"));
        PlaceSpawns(map, Stage("spawns"), depth);
        return null;
    }

    // ───────────────────────── helpers ─────────────────────────

    static bool Inside(Vector2 p, float inset) => p.X >= inset && p.Y >= inset && p.X <= Size - inset && p.Y <= Size - inset;

    static Vector2 RandomIn(Rng rng, float inset) => new(rng.Range(inset, Size - inset), rng.Range(inset, Size - inset));

    static float Smooth(float edge0, float edge1, float x) => MathUtil.Smoothstep(MathUtil.Clamp01((x - edge0) / (edge1 - edge0)));

    // ───────────────────────── stage 1: POIs ─────────────────────────

    /// <summary>How well a spot suits an open place: low ground over the whole clearing (lower is better).</summary>
    static float LowScore(LevelMap map, Vector2 p, float radius)
    {
        float sum = map.HeightAt(p);
        for (int i = 0; i < 8; i++)
        {
            float a = i * MathF.Tau / 8f;
            sum += map.HeightAt(p + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius);
        }
        return sum / 9f;
    }

    /// <summary>How well a spot suits a cave: rock to dig into, with open water a short swim from the chamber (lower is better).</summary>
    static float FlankScore(LevelMap map, Vector2 p, float radius)
    {
        float h = map.HeightAt(p);
        float water = float.MaxValue;
        for (int i = 0; i < 16; i++)
        {
            float a = i * MathF.Tau / 16f;
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            for (float d = radius; d <= radius + 16f; d += 1f)
                if (map.HeightAt(p + dir * d) < -PathDepth)
                {
                    water = MathF.Min(water, d - radius);
                    break;
                }
        }
        float score = h < 3f ? (3f - h) * 4f : 0f;
        return score + (water == float.MaxValue ? 40f : MathF.Abs(water - 6f));
    }

    /// <summary>Places every POI; returns which one found no room, or null.</summary>
    static string? PlacePois(LevelMap map, Rng rng)
    {
        // Each place is the best-scoring of a batch of candidates that keep the scatter rules: open places on low ground,
        // caves in rock flanks beside water. Where nothing low enough fits, the carving stage hollows the spot out.
        const int Batch = 24;
        // The jitter keeps retries on the same terrain from always picking the same spot.
        Vector2? Best(Func<Vector2?> sample, Func<Vector2, bool> fits, Func<Vector2, float> score, int tries = 600, float jitter = 2f)
        {
            Vector2? best = null;
            float bestScore = float.MaxValue;
            int found = 0;
            for (int i = 0; i < tries && found < Batch; i++)
            {
                if (sample() is not { } p || !fits(p)) continue;
                found++;
                float sc = score(p) + rng.Range(0f, jitter);
                if (sc < bestScore)
                {
                    bestScore = sc;
                    best = p;
                }
            }
            return best;
        }

        // Only starts with room for the rift far enough away somewhere in the square.
        const float riftInset = Rim + RiftRadius + 3f;
        bool CanReachRift(Vector2 q) => new[] { new Vector2(riftInset), new Vector2(Size - riftInset, riftInset), new Vector2(riftInset, Size - riftInset), new Vector2(Size - riftInset) }
            .Any(c => Vector2.Distance(c, q) >= RiftMinStraight + 4f);

        // Start: on the sunlit border zone just inside the rim, in the lowest water along the chosen side.
        int side = rng.Int(4);
        const float edge = Rim + 6f;
        Vector2? startAt = Best(() =>
        {
            float along = rng.Range(30f, Size - 30f);
            return side switch
            {
                0 => new Vector2(along, edge),
                1 => new Vector2(Size - edge, along),
                2 => new Vector2(along, Size - edge),
                _ => new Vector2(edge, along),
            };
        }, CanReachRift, q => LowScore(map, q, StartRadius), 60, jitter: 8f);
        if (startAt is not { } start) return "no room for the start";
        map.Pois.Add(new Poi { Kind = PoiKind.Start, Position = start, Radius = StartRadius });

        // Rift: far from the start, the arena disc clear of the rim, in the deepest water that fits.
        Vector2? riftFound = Best(() => RandomIn(rng, Rim + RiftRadius + 3f), q => Vector2.Distance(q, start) >= RiftMinStraight, q => LowScore(map, q, RiftRadius), jitter: 6f);
        if (riftFound is not { } riftAt) return "no room for the rift far enough from the start";
        map.Pois.Add(new Poi { Kind = PoiKind.Rift, Position = riftAt, Radius = RiftRadius });

        Vector2 axis = riftAt - start;
        Vector2 u = Vector2.Normalize(axis);
        Vector2 n = Geo.Perp(u);
        var placed = new List<Poi>();

        bool Fits(Vector2 p, PoiKind kind, float radius, bool early)
        {
            float inset = Rim + radius + (Poi.IsCaveHosted(kind) ? 10f : 6f);
            if (!Inside(p, inset)) return false;
            float fromStart = Vector2.Distance(p, start);
            if (early ? fromStart < EarlyMin || fromStart > EarlyMax : fromStart <= EarlyMax + 1f) return false;
            if (Vector2.Distance(p, riftAt) < RiftExclusion) return false;
            foreach (var o in placed)
            {
                float d = Vector2.Distance(p, o.Position);
                if (d < PoiSpacing) return false;
                if (Poi.IsCaveHosted(kind) && Poi.IsCaveHosted(o.Kind) && d < CaveSpacing) return false;
            }
            return true;
        }

        float RadiusOf(PoiKind kind) => Poi.IsCaveHosted(kind) ? (kind is PoiKind.TreasureCave or PoiKind.CurseDen ? ChamberRadius : PocketRadius) : ClearingRadius;
        float Score(PoiKind kind, Vector2 p) => Poi.IsCaveHosted(kind) ? FlankScore(map, p, RadiusOf(kind)) : LowScore(map, p, RadiusOf(kind));

        // The guaranteed item cache: 40–60% along the start→rift axis, off to one side of it.
        Vector2? item = Best(() => start + axis * rng.Range(0.4f, 0.6f) + n * (rng.Range(18f, 30f) * (rng.NextFloat() < 0.5f ? -1f : 1f)),
            q => Fits(q, PoiKind.ItemSpawn, ClearingRadius, false), q => Score(PoiKind.ItemSpawn, q));
        if (item is not { } itemAt) return "no room for the item cache";
        placed.Add(new Poi { Kind = PoiKind.ItemSpawn, Position = itemAt, Radius = ClearingRadius });

        // Optional places: shop always, 1–2 secrets, 0–1 curse den, a treasure cave, 2–4 ninja nests.
        var kinds = new List<PoiKind> { PoiKind.Shop, PoiKind.TreasureCave };
        int secrets = 1 + rng.Int(2), curses = rng.Int(2), nests = 2 + rng.Int(3);
        for (int i = 0; i < secrets; i++) kinds.Add(PoiKind.Secret);
        for (int i = 0; i < curses; i++) kinds.Add(PoiKind.CurseDen);
        for (int i = 0; i < nests; i++) kinds.Add(PoiKind.Ambush);

        // Exactly one sits early, 20–35 m from the start: usually the shop, to teach the economy.
        PoiKind earlyKind = rng.NextFloat() < 0.8f ? PoiKind.Shop : PoiKind.TreasureCave;
        kinds.Remove(earlyKind);
        kinds.Insert(0, earlyKind);

        Vector2 inward = Vector2.Normalize(new Vector2(Size / 2f, Size / 2f) - start);
        for (int k = 0; k < kinds.Count; k++)
        {
            PoiKind kind = kinds[k];
            bool early = k == 0;
            Vector2? at = Best(() =>
            {
                if (!early) return RandomIn(rng, Rim + 14f);
                // Toward the inside of the square from the start.
                float a = MathF.Atan2(inward.Y, inward.X) + rng.Range(-1.1f, 1.1f);
                return start + new Vector2(MathF.Cos(a), MathF.Sin(a)) * rng.Range(EarlyMin + 1f, EarlyMax - 1f);
            }, q => Fits(q, kind, RadiusOf(kind), early), q => Score(kind, q));
            if (at is not { } p) return $"no room for the {(early ? "early " : "")}{kind}";
            placed.Add(new Poi { Kind = kind, Position = p, Early = early, Radius = RadiusOf(kind) });
        }
        map.Pois.AddRange(placed);
        return null;
    }

    // ───────────────────────── stage 2: path network ─────────────────────────

    enum Route { Direct, ArcLeft, ArcRight, SCurve, SCurveMirror }

    sealed class Plan
    {
        public Route Route;
        public float Width;
        /// <summary>Waypoints as (t along the axis, lateral offset), plus fixed points inserted later.</summary>
        public List<(float T, Vector2 At)> Waypoints = new();
        /// <summary>The off-path places this route serves through spurs.</summary>
        public List<int> Hooked = new();
    }

    static string? BuildPaths(LevelMap map, Rng rng, Dictionary<int, Vector2> spurDirs)
    {
        Vector2 start = map.Start.Position, rift = map.Rift.Position;
        Vector2 axis = rift - start;
        float length = axis.Length();
        Vector2 u = axis / length, n = Geo.Perp(u);
        Vector2 At(float t, float lateral) => start + u * (t * length) + n * lateral;
        float T(Vector2 p) => Vector2.Dot(p - start, u) / length;

        var ground = new Ground(map);
        int count = 3 + rng.Int(3);
        var routes = new List<Route> { Route.Direct, Route.ArcLeft, Route.ArcRight, Route.SCurve, Route.SCurveMirror };
        // The trench route of deeper depths (DESIGN-TOPDOWN §4.1) is not built yet: Depth 1 has none.
        var plans = new List<Plan>();
        for (int i = 0; i < count; i++)
        {
            var plan = new Plan { Route = routes[i], Width = rng.Range(CorridorMinWidth, CorridorMaxWidth) };
            float a1 = rng.Range(32f, 42f), a2 = rng.Range(32f, 42f), s1 = rng.Range(18f, 26f), s2 = rng.Range(18f, 26f);
            var controls = plan.Route switch
            {
                Route.Direct => new[] { (0.5f, rng.Range(-6f, 6f)) },
                Route.ArcLeft => new[] { (0.3f, a1), (0.7f, a2) },
                Route.ArcRight => new[] { (0.3f, -a1), (0.7f, -a2) },
                Route.SCurve => new[] { (0.33f, s1), (0.67f, -s2) },
                _ => new[] { (0.33f, -s1), (0.67f, s2) },
            };
            foreach (var (t, lat) in controls) plan.Waypoints.Add((t, At(t, lat)));
            plans.Add(plan);
        }

        // Crossing nodes: 2–4 points each shared by two routes, so the routes are guaranteed to meet there.
        int nodes = 2 + rng.Int(3);
        var nodePoints = new List<Vector2>();
        for (int k = 0; k < nodes; k++)
        {
            float t = 0.3f + (k + 0.5f) / nodes * 0.45f + rng.Range(-0.03f, 0.03f);
            int a = rng.Int(plans.Count), b = (a + 1 + rng.Int(plans.Count - 1)) % plans.Count;
            Vector2 pa = PlanPointAt(plans[a], t, start, rift), pb = PlanPointAt(plans[b], t, start, rift);
            Vector2 node = (pa + pb) * 0.5f + n * rng.Range(-3f, 3f);
            // A crossing never sits on a place (routes meet in the open and reach places by spurs).
            var blocking = map.Pois.FirstOrDefault(poi => poi.Kind is not (PoiKind.Start or PoiKind.Rift)
                && Vector2.Distance(poi.Position, node) < KeepOut(poi, CorridorMaxWidth * 0.5f) + 1f);
            if (blocking is not null)
            {
                Vector2 away = Geo.Normalize(node - blocking.Position, n);
                node = blocking.Position + away * (KeepOut(blocking, CorridorMaxWidth * 0.5f) + 1f);
            }
            // Routes meet in the lowest water nearby (still clear of every place).
            Vector2 low = node;
            float lowH = ground.Height(node);
            for (float dy = -10f; dy <= 10f; dy += 2f)
            for (float dx = -10f; dx <= 10f; dx += 2f)
            {
                var q = node + new Vector2(dx, dy);
                if (dx * dx + dy * dy > 100f || !Inside(q, Rim + 12f)) continue;
                if (map.Pois.Any(poi => poi.Kind is not (PoiKind.Start or PoiKind.Rift) && Vector2.Distance(poi.Position, q) < KeepOut(poi, CorridorMaxWidth * 0.5f) + 1f)) continue;
                float h = ground.Height(q);
                if (h < lowH - 0.5f)
                {
                    lowH = h;
                    low = q;
                }
            }
            node = low;
            nodePoints.Add(node);
            foreach (var plan in new[] { plans[a], plans[b] })
            {
                plan.Waypoints.RemoveAll(w => MathF.Abs(w.T - t) < 0.12f);
                plan.Waypoints.Add((t, node));
            }
        }

        // Off-path POIs hook onto the nearest route through a spur of at most ~15–20 m.
        var offPath = Enumerable.Range(0, map.Pois.Count).Where(i => map.Pois[i].Kind is not (PoiKind.Start or PoiKind.Rift)).ToList();
        var hooks = new Dictionary<int, Vector2>();
        foreach (int pi in offPath)
        {
            var poi = map.Pois[pi];
            Plan? best = null;
            Vector2 nearest = default;
            float bestDist = float.MaxValue;
            foreach (var plan in plans)
            {
                var line = Polyline(plan, start, rift);
                Vector2 q = NearestPoint(line, poi.Position);
                float d = Vector2.Distance(q, poi.Position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = plan;
                    nearest = q;
                }
            }
            Vector2 dir = Geo.Normalize(nearest - poi.Position, -u);
            float spur = rng.Range(9f, 12f);
            Vector2 hook = poi.Position + dir * (poi.Radius + spur + best!.Width * 0.5f);
            best.Waypoints.RemoveAll(w => MathF.Abs(w.T - T(hook)) < 0.05f && !hooks.ContainsValue(w.At));
            best.Waypoints.Add((T(hook), hook));
            hooks[pi] = hook;
            best.Hooked.Add(pi);
        }

        // Turn the plans into corridors, relaxed together: they bend around the places they pass (those are reached by
        // spurs, not crossed) and, away from where they meet, keep a ridge's width apart instead of running side by side.
        var lines = plans.Select(plan => Polyline(plan, start, rift)).ToList();
        // Crossing nodes stay pinned so the routes really meet there.
        var pinned = lines.Select(line => line.Select(q => nodePoints.Any(nd => Vector2.DistanceSquared(nd, q) < 0.01f)).ToArray()).ToList();
        float Apart(Vector2 q)
        {
            float w = Smooth(StartRadius + 2f, StartRadius + 12f, Vector2.Distance(q, start)) * Smooth(RiftRadius + 1f, RiftRadius + 8f, Vector2.Distance(q, rift));
            foreach (var nd in nodePoints) w *= Smooth(4f, 12f, Vector2.Distance(q, nd));
            return w;
        }
        for (int pass = 0; pass < 40; pass++)
        {
            for (int li = 0; li < lines.Count; li++)
            {
                var plan = plans[li];
                var line = lines[li];
                float lo = Rim + 2f + plan.Width * 0.5f, hi = Size - Rim - 2f - plan.Width * 0.5f;
                // Relax the kinks the pushes leave (endpoints and nodes stay put), then push apart and out of the keep-outs.
                for (int sweep = 0; pass > 0 && sweep < 2; sweep++)
                {
                    var smoothed = new List<Vector2>(line);
                    for (int i = 1; i + 1 < line.Count; i++)
                        if (!pinned[li][i]) smoothed[i] = line[i] * 0.5f + (line[i - 1] + line[i + 1]) * 0.25f;
                    line = lines[li] = smoothed;
                }
                // The sideways push away from routes running alongside, blurred along the line so it bends the route
                // gently rather than point by point. Only sideways: where two routes cross at an angle it does nothing.
                var push = new Vector2[line.Count];
                var others = lines.Select((l, j) => j == li ? default : new Chunks(l)).ToArray();
                for (int i = 1; i + 1 < line.Count; i++)
                {
                    float apart = pinned[li][i] ? 0f : Apart(line[i]);
                    // A route stays close by the places it serves, so their spurs stay short.
                    foreach (int pi in plan.Hooked)
                    {
                        float keep = KeepOut(map.Pois[pi], plan.Width * 0.5f);
                        apart *= Smooth(keep + 3f, keep + 14f, Vector2.Distance(line[i], map.Pois[pi].Position));
                    }
                    if (apart <= 0f) continue;
                    Vector2 along = Geo.Normalize(line[i + 1] - line[i - 1], u), side = Geo.Perp(along);
                    for (int lj = 0; lj < lines.Count; lj++)
                    {
                        if (lj == li) continue;
                        if (!others[lj].NearestWithin(line[i], RouteSpacing, out Vector2 c, out Vector2 otherAlong)) continue;
                        float d = Vector2.Distance(line[i], c);
                        float parallel = MathF.Pow(MathF.Abs(Vector2.Dot(along, otherAlong)), 4f);
                        // Away from the other route, judged from its direction (stable along a stretch).
                        Vector2 off = Geo.Perp(otherAlong);
                        float away = MathF.Sign(Vector2.Dot(side, off)) * (Vector2.Dot(line[i] - c, off) < 0f ? -1f : 1f);
                        push[i] += side * (away * (RouteSpacing - d) * 0.25f * apart * parallel);
                    }
                }
                // Downhill: where the route runs over ground it would have to carve, it slides sideways toward lower ground,
                // so routes follow the valleys and channels of the terrain.
                for (int i = 1; i + 1 < line.Count; i++)
                {
                    if (pinned[li][i]) continue;
                    float h = ground.Height(line[i]);
                    float need = Smooth(-PathDepth - 3f, 2f, h);
                    // Places sit in the low ground too: near one the route keeps its line instead of sliding into it.
                    foreach (int pi in offPath)
                    {
                        float keep = KeepOut(map.Pois[pi], plan.Width * 0.5f);
                        need *= Smooth(keep, keep + 12f, Vector2.Distance(line[i], map.Pois[pi].Position));
                    }
                    if (need <= 0f) continue;
                    Vector2 side = Geo.Perp(Geo.Normalize(line[i + 1] - line[i - 1], u));
                    float slide = -Vector2.Dot(ground.Gradient(line[i]), side);
                    push[i] += side * (Math.Clamp(slide, -1f, 1f) * 0.8f * need);
                }
                for (int blur = 0; blur < 4; blur++)
                {
                    var next = new Vector2[push.Length];
                    for (int i = 1; i + 1 < push.Length; i++)
                        next[i] = pinned[li][i] ? Vector2.Zero : push[i] * 0.5f + (push[i - 1] + push[i + 1]) * 0.25f;
                    push = next;
                }
                for (int i = 1; i + 1 < line.Count; i++)
                {
                    Vector2 q = line[i];
                    Vector2 step = push[i].Length() > 1f ? Vector2.Normalize(push[i]) : push[i];
                    Vector2 pushed = q + step;
                    // Never shove a route into a place it bends around (it could pop out on the far side).
                    if (step != Vector2.Zero && !offPath.Any(pi => Vector2.Distance(pushed, map.Pois[pi].Position) < KeepOut(map.Pois[pi], plan.Width * 0.5f) + 1f)) q = pushed;
                    foreach (int pi in offPath)
                    {
                        var poi = map.Pois[pi];
                        float keep = KeepOut(poi, plan.Width * 0.5f) + 0.5f;
                        Vector2 away = q - poi.Position;
                        float d = away.Length();
                        if (d < keep) q = poi.Position + Geo.Normalize(away, n) * keep;
                    }
                    line[i] = Vector2.Clamp(q, new Vector2(lo), new Vector2(hi));
                }
            }
        }
        // A last relax smooths what the final pushes left, then the keep-outs and bounds are enforced once more.
        for (int li = 0; li < lines.Count; li++)
        {
            var line = lines[li];
            float lo = Rim + 2f + plans[li].Width * 0.5f, hi = Size - Rim - 2f - plans[li].Width * 0.5f;
            for (int sweep = 0; sweep < 3; sweep++)
            {
                var smoothed = new List<Vector2>(line);
                for (int i = 1; i + 1 < line.Count; i++)
                    if (!pinned[li][i]) smoothed[i] = line[i] * 0.5f + (line[i - 1] + line[i + 1]) * 0.25f;
                line = smoothed;
            }
            for (int i = 1; i + 1 < line.Count; i++)
            {
                Vector2 q = line[i];
                foreach (int pi in offPath)
                {
                    var poi = map.Pois[pi];
                    float keep = KeepOut(poi, plans[li].Width * 0.5f) + 0.5f;
                    Vector2 away = q - poi.Position;
                    if (away.Length() < keep) q = poi.Position + Geo.Normalize(away, n) * keep;
                }
                line[i] = Vector2.Clamp(q, new Vector2(lo), new Vector2(hi));
            }
            lines[li] = line;
        }
        for (int li = 0; li < plans.Count; li++)
            map.Corridors.Add(new Corridor { Kind = CorridorKind.Main, Points = lines[li], Width = plans[li].Width });

        // Spurs from the network to each place.
        foreach (int pi in offPath)
        {
            var poi = map.Pois[pi];
            Vector2 q = NearestOnCorridors(map, poi.Position, out int ci);
            Vector2 dir = Geo.Normalize(q - poi.Position, -u);
            // The spur begins where the corridor's floor ends.
            q -= dir * (map.Corridors[ci].HalfWidth - 0.5f);
            Vector2 end = Poi.IsCaveHosted(poi.Kind) ? poi.Position + dir * poi.Radius : poi.Position;
            spurDirs[pi] = dir;
            map.Corridors.Add(new Corridor { Kind = CorridorKind.Spur, Points = new List<Vector2> { q, end }, Width = SpurWidth, Poi = pi });
        }

        // Main corridors must keep clear of every off-path place (they are reached by spurs, not crossed).
        foreach (var c in map.Corridors.Where(c => c.Kind == CorridorKind.Main))
            foreach (int pi in offPath)
            {
                var poi = map.Pois[pi];
                if (c.DistanceToCentre(poi.Position) < KeepOut(poi, c.HalfWidth) - 0.5f) return $"a corridor runs through the {poi.Kind}";
            }

        // Plazas wherever main corridors cross, away from the shared start and rift.
        var crossings = new List<Vector2>();
        var mains = map.Corridors.Where(c => c.Kind == CorridorKind.Main).ToList();
        for (int a = 0; a < mains.Count; a++)
        for (int b = a + 1; b < mains.Count; b++)
            for (int i = 1; i < mains[a].Points.Count; i++)
            for (int j = 1; j < mains[b].Points.Count; j++)
                if (Meet(mains[a].Points[i - 1], mains[a].Points[i], mains[b].Points[j - 1], mains[b].Points[j], out var x)
                    && Vector2.Distance(x, start) > StartRadius + 16f && Vector2.Distance(x, rift) > RiftRadius + 7f)
                    crossings.Add(x);
        var clusters = new List<List<Vector2>>();
        foreach (var x in crossings)
        {
            var home = clusters.FirstOrDefault(c => c.Any(p => Vector2.Distance(p, x) < PlazaMerge));
            if (home is null) clusters.Add(new List<Vector2> { x });
            else home.Add(x);
        }
        foreach (var c in clusters)
            map.Plazas.Add(new Plaza(c.Aggregate(Vector2.Zero, (s, p) => s + p) / c.Count, PlazaRadius));
        if (map.Plazas.Count is < 2 or > 4) return $"{map.Plazas.Count} corridor crossings (want 2–4)";
        // Routes that cannot be spread (a cramped corner) would read as one wide lane: try the next layout.
        if (LevelValidator.LongestParallelRun(map) > LevelValidator.MaxSideBySide) return "two routes run side by side";
        return null;
    }

    /// <summary>Two corridor centre-line segments meet when they cross or pass within 2 m (they share floor there).</summary>
    static bool Meet(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 at)
    {
        if (Geo.SegmentIntersection(a, b, c, d, out at)) return true;
        float best = float.MaxValue;
        foreach (var (p, q0, q1) in new[] { (a, c, d), (b, c, d), (c, a, b), (d, a, b) })
        {
            float dist = Geo.SegmentDistance(p, q0, q1);
            if (dist < best)
            {
                best = dist;
                at = p;
            }
        }
        return best < 2f;
    }

    /// <summary>How close a corridor's centre line may come to an off-path place.</summary>
    static float KeepOut(Poi poi, float halfWidth) => poi.Radius + halfWidth + (Poi.IsCaveHosted(poi.Kind) ? 4f : 2f);

    static Vector2 PlanPointAt(Plan plan, float t, Vector2 start, Vector2 rift)
    {
        var pts = new List<(float T, Vector2 At)> { (0f, start) };
        pts.AddRange(plan.Waypoints.OrderBy(w => w.T));
        pts.Add((1f, rift));
        for (int i = 1; i < pts.Count; i++)
            if (t <= pts[i].T)
            {
                float k = (t - pts[i - 1].T) / MathF.Max(1e-4f, pts[i].T - pts[i - 1].T);
                return Vector2.Lerp(pts[i - 1].At, pts[i].At, Math.Clamp(k, 0f, 1f));
            }
        return rift;
    }

    /// <summary>A smooth Catmull-Rom line through the plan's waypoints, about one point every 2 m.</summary>
    static List<Vector2> Polyline(Plan plan, Vector2 start, Vector2 rift)
    {
        var pts = new List<Vector2> { start };
        pts.AddRange(plan.Waypoints.OrderBy(w => w.T).Select(w => w.At));
        pts.Add(rift);
        var line = new List<Vector2>();
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            Vector2 p0 = pts[Math.Max(i - 1, 0)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Math.Min(i + 2, pts.Count - 1)];
            int steps = Math.Max(2, (int)MathF.Ceiling(Vector2.Distance(p1, p2) / 2f));
            for (int s = 0; s < steps; s++) line.Add(CatmullRom(p0, p1, p2, p3, s / (float)steps));
        }
        line.Add(rift);
        return line;
    }

    static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
    }

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

    /// <summary>Bounding boxes over runs of a polyline's segments, so distant runs are skipped wholesale.</summary>
    readonly struct Chunks
    {
        const int Run = 8;
        public readonly List<Vector2> Line;
        readonly Vector4[] _boxes;

        public Chunks(List<Vector2> line)
        {
            Line = line;
            int count = (line.Count - 2) / Run + 1;
            _boxes = new Vector4[count];
            for (int c = 0; c < count; c++)
            {
                var box = new Vector4(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
                for (int i = c * Run; i <= Math.Min(line.Count - 1, (c + 1) * Run); i++)
                    box = new Vector4(MathF.Min(box.X, line[i].X), MathF.Min(box.Y, line[i].Y), MathF.Max(box.Z, line[i].X), MathF.Max(box.W, line[i].Y));
                _boxes[c] = box;
            }
        }

        /// <summary>The nearest point closer than <paramref name="within"/>, and the line's direction there; false when none is.</summary>
        public bool NearestWithin(Vector2 p, float within, out Vector2 nearest, out Vector2 direction)
        {
            nearest = p;
            direction = Vector2.UnitX;
            float bestD = within * within;
            bool found = false;
            for (int c = 0; c < _boxes.Length; c++)
            {
                var box = _boxes[c];
                float bx = MathF.Max(MathF.Max(box.X - p.X, p.X - box.Z), 0f), by = MathF.Max(MathF.Max(box.Y - p.Y, p.Y - box.W), 0f);
                if (bx * bx + by * by >= bestD) continue;
                for (int i = Math.Max(1, c * Run + 1); i <= Math.Min(Line.Count - 1, (c + 1) * Run); i++)
                {
                    Vector2 a = Line[i - 1], ab = Line[i] - a;
                    float t = Math.Clamp(Vector2.Dot(p - a, ab) / MathF.Max(ab.LengthSquared(), 1e-9f), 0f, 1f);
                    Vector2 q = a + ab * t;
                    float d = Vector2.DistanceSquared(q, p);
                    if (d < bestD)
                    {
                        bestD = d;
                        nearest = q;
                        direction = Geo.Normalize(ab, direction);
                        found = true;
                    }
                }
            }
            return found;
        }
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
    static float DistanceToOpen(LevelMap map, Vector2 p)
    {
        float d = float.MaxValue;
        foreach (var c in map.Corridors)
        {
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

    /// <summary>Every height back inside the prototype's bounds (−7…+14), roofs included.</summary>
    static void ClampHeights(LevelMap map)
    {
        for (int i = 0; i < map.Heights.Length; i++)
        {
            map.Heights[i] = Math.Clamp(map.Heights[i], ReefTerrain.HMin, ReefTerrain.HMax);
            if (!float.IsNaN(map.Lid[i])) map.Lid[i] = Math.Clamp(map.Lid[i], ReefTerrain.HMin, ReefTerrain.HMax);
        }
    }

    /// <summary>
    /// The labyrinth: side canyons branch off the route canyons (and off each other) into dead ends, turning sharply
    /// every so often, and now and then running into another canyon to close a loop. Each keeps a wall of rock between
    /// itself and every other canyon, and stays clear of the places (caves keep their rock). They grow until about
    /// <see cref="OpenTarget"/> of the interior is open water.
    /// </summary>
    static void GrowCanyons(LevelMap map, Rng rng)
    {
        const int N = LevelMap.Samples;
        int lo = (int)Rim, hi = LevelMap.Size - lo;
        int interior = (hi - lo + 1) * (hi - lo + 1), openCells = 0;
        var open = new bool[N * N];
        void Mark(int x, int y)
        {
            if (x < lo || y < lo || x > hi || y > hi || open[y * N + x]) return;
            open[y * N + x] = true;
            openCells++;
        }
        // Cells whose floor stays under the swim level: the open shape, its flat margin, and the wall's first half metre.
        void StampSegment(Vector2 a, Vector2 b, float half)
        {
            float r = half + FlatMargin + 0.6f;
            for (int y = (int)(MathF.Min(a.Y, b.Y) - r); y <= (int)(MathF.Max(a.Y, b.Y) + r) + 1; y++)
            for (int x = (int)(MathF.Min(a.X, b.X) - r); x <= (int)(MathF.Max(a.X, b.X) + r) + 1; x++)
                if (Geo.SegmentDistance(new Vector2(x, y), a, b) <= r) Mark(x, y);
        }
        void StampCorridor(Corridor c)
        {
            for (int i = 1; i < c.Points.Count; i++) StampSegment(c.Points[i - 1], c.Points[i], c.HalfWidth);
        }
        foreach (var c in map.Corridors) StampCorridor(c);
        foreach (var z in map.Plazas) StampSegment(z.Center, z.Center, z.Radius);
        foreach (var poi in map.Pois)
            if (!Poi.IsCaveHosted(poi.Kind)) StampSegment(poi.Position, poi.Position, poi.Radius);

        const float step = 3f;
        for (int tries = 0; tries < 3000 && openCells < OpenTarget * interior; tries++)
        {
            var parent = map.Corridors[rng.Int(map.Corridors.Count)];
            if (parent.Kind is not (CorridorKind.Main or CorridorKind.Side) || parent.Points.Count < 3) continue;
            int at = 1 + rng.Int(parent.Points.Count - 2);
            Vector2 root = parent.Points[at];
            Vector2 tangent = Geo.Normalize(parent.Points[at + 1] - parent.Points[at - 1], Vector2.UnitX);
            Vector2 dir = Rotate(Geo.Perp(tangent) * (rng.NextFloat() < 0.5f ? -1f : 1f), rng.Range(-0.35f, 0.35f));
            float half = rng.Range(SideMinWidth, SideMaxWidth) * 0.5f;
            float target = rng.Range(18f, 55f), walked = 0f, untilTurn = rng.Range(8f, 16f);
            var points = new List<Vector2> { root };
            Vector2 pos = root;
            bool joined = false;
            while (walked < target)
            {
                Vector2 next = pos + dir * step;
                if (!Inside(next, Rim + half + 3f)) break;
                // What it would run into: another canyon (too close for a wall between), a place, a plaza, or itself.
                Corridor? blocker = null;
                bool blocked = false;
                foreach (var c in map.Corridors)
                {
                    float need = half + c.HalfWidth + WallMin + 2f * FlatMargin;
                    // Leaving its parent, it may start right at the parent's edge.
                    if (c == parent && walked < parent.HalfWidth + half + WallMin + 2f * FlatMargin) continue;
                    if (c.DistanceToCentre(next) < need)
                    {
                        blocked = true;
                        if (c != parent && c.Kind is CorridorKind.Main or CorridorKind.Side) blocker = c;
                        break;
                    }
                }
                foreach (var poi in map.Pois)
                {
                    float keep = poi.Kind == PoiKind.Rift ? poi.Radius + 6f : Poi.IsCaveHosted(poi.Kind) ? poi.Radius + 20f : poi.Radius + 4f;
                    if (Vector2.Distance(next, poi.Position) < keep + half) { blocked = true; blocker = null; }
                }
                foreach (var z in map.Plazas)
                    if (Vector2.Distance(next, z.Center) < z.Radius + half + WallMin) { blocked = true; blocker = null; }
                // Itself: points far enough back along the canyon that a wall must stand between (not its last few steps).
                float selfGap = 2f * half + WallMin + 2f * FlatMargin;
                int recent = (int)MathF.Ceiling(1.6f * selfGap / step);
                for (int i = 0; i + recent < points.Count; i++)
                    if (Vector2.Distance(next, points[i]) < selfGap) { blocked = true; blocker = null; }
                if (blocked)
                {
                    // Now and then the canyon breaks through into the one it met: a loop in the labyrinth.
                    if (blocker is not null && walked >= 9f && rng.NextFloat() < 0.5f && Vector2.Distance(next, map.Rift.Position) > map.Rift.Radius + 25f)
                    {
                        points.Add(NearestPoint(blocker.Points, next));
                        joined = true;
                    }
                    break;
                }
                pos = next;
                points.Add(pos);
                walked += step;
                untilTurn -= step;
                if (untilTurn <= 0f)
                {
                    // The labyrinth turns: a sharp bend left or right.
                    dir = Rotate(dir, (rng.NextFloat() < 0.5f ? -1f : 1f) * rng.Range(0.9f, 1.6f));
                    untilTurn = rng.Range(8f, 16f);
                }
                else dir = Rotate(dir, rng.Range(-0.12f, 0.12f));
            }
            if (walked < 9f && !joined) continue;
            var canyon = new Corridor { Kind = CorridorKind.Side, Points = points, Width = half * 2f };
            map.Corridors.Add(canyon);
            StampCorridor(canyon);
        }
    }

    /// <summary>
    /// Raises the reef out of the seabed: everywhere but the open ground (canyons, plazas, clearings, the boss arena),
    /// walls climb from the floor over <see cref="WallSlope"/> metres with the prototype's cosine brush to tops taken
    /// from the prototype's terrain (<see cref="ReefTerrain"/>, mapped to 4–14 m). Canyon floors undulate just under
    /// level −1; the boss arena is level, with its rift the deepest point of the map; then the trenches and the rim.
    /// </summary>
    static void RaiseWalls(LevelMap map, string terrainText, Rng rng)
    {
        const int N = LevelMap.Samples;
        var tops = new float[N * N];
        ReefTerrain.Generate(tops, N, terrainText);
        uint dipSeed = (uint)(rng.NextU64() >> 32);
        var rift = map.Rift;
        var toOpen = new float[N * N];
        Parallel.For(0, N, y =>
        {
            for (int x = 0; x < N; x++)
            {
                var p = new Vector2(x, y);
                float d = toOpen[y * N + x] = DistanceToOpen(map, p);
                float floor = -PathDepth - 0.6f * FloorDip(x, y, dipSeed);
                // The boss arena is a level floor.
                floor = MathUtil.Lerp(-PathDepth, floor, Smooth(rift.Radius, rift.Radius + 5f, Vector2.Distance(p, rift.Position)));
                float top = WallTopMin + (WallTopMax - WallTopMin) * (tops[y * N + x] - ReefTerrain.HMin) / (ReefTerrain.HMax - ReefTerrain.HMin);
                float rise = 1f - ReefTerrain.Falloff(d / WallSlope);
                float h = floor + rise * (top - floor);
                // The impassable rim rises out of it with the same brush.
                float e = MathF.Min(MathF.Min(x, y), MathF.Min(Size - x, Size - y));
                float w = ReefTerrain.Falloff(e / (Rim + 1f));
                if (w > 0f) h += w * (ReefTerrain.HMax - h);
                map[x, y] = h;
            }
        });

        // At most 40% may stand above the swim level: where the prototype's terrain is lowest, walls sink into shallow
        // reef plateaus under the swim level (open chambers in the labyrinth), just enough to keep under the cap.
        SinkShoals(map, tops, toOpen, dipSeed);

        // The rift: a crack across the middle of the arena, its floor the lowest point of the level.
        float angle = rng.Range(0f, MathF.PI);
        Vector2 axis = new(MathF.Cos(angle), MathF.Sin(angle)), across = Geo.Perp(axis);
        float halfLength = rng.Range(7f, 9f), halfWidth = rng.Range(2.5f, 3.5f);
        for (int y = (int)(rift.Position.Y - halfLength - 1); y <= (int)(rift.Position.Y + halfLength + 1); y++)
        for (int x = (int)(rift.Position.X - halfLength - 1); x <= (int)(rift.Position.X + halfLength + 1); x++)
        {
            Vector2 rel = new Vector2(x, y) - rift.Position;
            float a = Vector2.Dot(rel, axis) / halfLength, b = Vector2.Dot(rel, across) / halfWidth;
            float k = ReefTerrain.Falloff(MathF.Sqrt(a * a + b * b));
            if (k > 0f) map[x, y] = MathUtil.Lerp(map[x, y], -CrackDepth, k);
        }

        CutTrenches(map, rng, dipSeed);
    }

    /// <summary>Share of the interior the walls may cover after sinking (a little under the 40% cap, for later carving).</summary>
    const float WallShare = 0.33f;

    /// <summary>
    /// Sinks the cores of the biggest wall masses into shoals until at most <see cref="WallShare"/> of the interior stands
    /// above the swim level: rock far from every canyon (and on the prototype's lower ground) goes first, so the canyon
    /// walls themselves keep standing.
    /// </summary>
    static void SinkShoals(LevelMap map, float[] tops, float[] toOpen, uint dipSeed)
    {
        const int N = LevelMap.Samples;
        int lo = (int)Rim, hi = LevelMap.Size - lo;
        var walls = new List<float>();
        int interior = 0;
        for (int y = lo; y <= hi; y++)
        for (int x = lo; x <= hi; x++)
        {
            interior++;
            if (map[x, y] > 0f) walls.Add(SinkScore(tops[y * N + x], toOpen[y * N + x]));
        }
        int excess = walls.Count - (int)(WallShare * interior);
        if (excess <= 0) return;
        walls.Sort();
        // Walls on the lowest prototype ground sink first; a soft band around the threshold keeps the slopes smooth.
        float threshold = walls[Math.Min(excess, walls.Count - 1)];
        const float band = 0.06f;
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float e = MathF.Min(MathF.Min(x, y), MathF.Min(Size - x, Size - y));
            if (e < Rim + 1f) continue;
            float h = map[x, y];
            float floor = -PathDepth - 0.6f * FloorDip(x, y, dipSeed);
            if (h <= floor + 0.01f) continue;
            float keep = Smooth(threshold - band, threshold + band, SinkScore(tops[y * N + x], toOpen[y * N + x]));
            if (keep >= 1f) continue;
            // A shoal plateau rolling a little under the swim level.
            float shoal = -0.35f - 0.5f * FloorDip(x + 37, y + 11, dipSeed);
            float sunk = floor + (h - floor) / MathF.Max(1f, h - floor) * MathF.Max(shoal - floor, 0f);
            map[x, y] = MathUtil.Lerp(MathF.Min(h, sunk), h, keep);
        }
    }

    /// <summary>Lower sinks first: far from the canyons, then low on the prototype's terrain (0…1 each).</summary>
    static float SinkScore(float top, float toOpen) =>
        0.35f * (top - ReefTerrain.HMin) / (ReefTerrain.HMax - ReefTerrain.HMin) + (1f - Smooth(WallSlope + 2f, WallSlope + 12f, toOpen));

    /// <summary>The deeps: 1–3 stretches of route canyon (30–60 m, away from the start and the arena) cut deeper.</summary>
    static void CutTrenches(LevelMap map, Rng rng, uint dipSeed)
    {
        var mains = map.Corridors.Where(c => c.Kind == CorridorKind.Main).ToList();
        int want = 1 + rng.Int(3);
        for (int tries = 0; tries < 60 && map.Trenches.Count < want; tries++)
        {
            var c = mains[rng.Int(mains.Count)];
            if (c.Points.Count < 12) continue;
            int i0 = rng.Int(c.Points.Count - 10);
            float length = rng.Range(30f, 60f), walked = 0f;
            var points = new List<Vector2> { c.Points[i0] };
            for (int i = i0 + 1; i < c.Points.Count && walked < length; i++)
            {
                walked += Vector2.Distance(c.Points[i - 1], c.Points[i]);
                points.Add(c.Points[i]);
            }
            if (walked < 30f) continue;
            if (points.Any(q => Vector2.Distance(q, map.Start.Position) < map.Start.Radius + 14f || Vector2.Distance(q, map.Rift.Position) < map.Rift.Radius + 8f)) continue;
            if (map.Trenches.Any(t => points.Any(q => t.DistanceToCentre(q) < 20f))) continue;
            map.Trenches.Add(new Trench { Points = points, Width = Math.Clamp(c.Width + rng.Range(0f, 2f), 8f, 14f), Depth = rng.Range(TrenchMin, TrenchMax) });
        }
        Parallel.For(0, LevelMap.Samples, y =>
        {
            for (int x = 0; x < LevelMap.Samples; x++)
            {
                var p = new Vector2(x, y);
                float h = map[x, y];
                foreach (var t in map.Trenches)
                {
                    float dc = t.DistanceToCentre(p), hw = t.Width * 0.5f;
                    if (dc >= hw) continue;
                    float profile = 1f - Smooth(hw * 0.4f, hw, dc);
                    float bottom = -t.Depth - 0.8f * FloorDip(x, y, dipSeed + 91u);
                    h = MathF.Min(h, MathUtil.Lerp(h, bottom, profile));
                }
                map[x, y] = h;
            }
        });
    }

    /// <summary>The terrain smoothed over a few metres, for routing: its slopes show valleys rather than every bump.</summary>
    sealed class Ground
    {
        const int N = LevelMap.Samples;
        readonly float[] _h = new float[N * N];

        public Ground(LevelMap map)
        {
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
                _h[y * N + x] = map[x, y];
            var tmp = new float[N * N];
            for (int pass = 0; pass < 2; pass++)
            {
                for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float sum = 0f;
                    int c = 0;
                    for (int k = -3; k <= 3; k++)
                    {
                        int xx = x + k;
                        if (xx < 0 || xx >= N) continue;
                        sum += _h[y * N + xx];
                        c++;
                    }
                    tmp[y * N + x] = sum / c;
                }
                for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float sum = 0f;
                    int c = 0;
                    for (int k = -3; k <= 3; k++)
                    {
                        int yy = y + k;
                        if (yy < 0 || yy >= N) continue;
                        sum += tmp[yy * N + x];
                        c++;
                    }
                    _h[y * N + x] = sum / c;
                }
            }
        }

        public float Height(Vector2 p)
        {
            float fx = Math.Clamp(p.X, 0f, N - 1.001f), fy = Math.Clamp(p.Y, 0f, N - 1.001f);
            int x = (int)fx, y = (int)fy;
            float tx = fx - x, ty = fy - y;
            float a = MathUtil.Lerp(_h[y * N + x], _h[y * N + x + 1], tx), b = MathUtil.Lerp(_h[(y + 1) * N + x], _h[(y + 1) * N + x + 1], tx);
            return MathUtil.Lerp(a, b, ty);
        }

        public Vector2 Gradient(Vector2 p) => new Vector2(
            Height(p + new Vector2(1f, 0f)) - Height(p - new Vector2(1f, 0f)),
            Height(p + new Vector2(0f, 1f)) - Height(p - new Vector2(0f, 1f))) * 0.5f;
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
            if (w * fade <= 0f || top <= map[x, y]) continue;
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

    static string? BuildFeatures(LevelMap map, Rng rng, Dictionary<int, Vector2> spurDirs)
    {
        // Caves: every special room gets one, carved into the ridge behind its spur.
        for (int pi = 0; pi < map.Pois.Count; pi++)
        {
            var poi = map.Pois[pi];
            if (!Poi.IsCaveHosted(poi.Kind)) continue;
            Vector2 facing = spurDirs[pi];
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
                // The curse den may settle for a pocket when the ridge behind it is too thin; the treasure cave may not.
                if (cave.Chambers.Count == 1 && poi.Kind == PoiKind.CurseDen) cave.Kind = CaveKind.Pocket;
                else if (cave.Chambers.Count == 1) return $"no room behind the {poi.Kind} for its chambers";
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

        // Passes: narrow cuts through ridge saddles between two corridors that are close as the fish swims but far apart on the network.
        var samples = new List<(Vector2 P, int Corridor, float Along)>();
        for (int c = 0; c < map.Corridors.Count; c++)
        {
            var corridor = map.Corridors[c];
            if (corridor.Kind != CorridorKind.Main) continue;
            float along = 0f;
            for (int i = 0; i < corridor.Points.Count; i += 2)
            {
                if (i > 0) along += Vector2.Distance(corridor.Points[i - 2 >= 0 ? i - 2 : 0], corridor.Points[i]);
                Vector2 p = corridor.Points[i];
                if (Vector2.Distance(p, map.Start.Position) < 22f || Vector2.Distance(p, map.Rift.Position) < 24f) continue;
                if (map.Plazas.Any(z => Vector2.Distance(z.Center, p) < 14f)) continue;
                samples.Add((p, c, along));
            }
        }
        var passCandidates = new List<(Vector2 A, Vector2 B, float Saddle)>();
        for (int i = 0; i < samples.Count; i++)
        for (int j = i + 1; j < samples.Count; j++)
        {
            var (a, ca, la) = samples[i];
            var (b, cb, lb) = samples[j];
            float d = Vector2.Distance(a, b);
            if (d is < 14f or > 34f) continue;
            if (ca == cb && MathF.Abs(la - lb) < d * 2.5f) continue;
            float saddle = 0f;
            bool ok = true;
            for (float s = 0f; s <= d; s += 1f)
            {
                Vector2 q = Vector2.Lerp(a, b, s / d);
                saddle = MathF.Max(saddle, map.HeightAt(q));
                foreach (var cave in map.Caves)
                    foreach (var (cc, cr) in cave.Chambers)
                        if (Vector2.Distance(q, cc) < cr + 4f) ok = false;
                foreach (var poi in map.Pois)
                    if (!Poi.IsCaveHosted(poi.Kind) && Vector2.Distance(q, poi.Position) < poi.Radius + 3f) ok = false;
                if (!ok) break;
            }
            if (ok && saddle >= 3f) passCandidates.Add((a, b, saddle));
        }
        int passes = 1 + rng.Int(3);
        foreach (var cand in passCandidates.OrderBy(c => c.Saddle).ThenBy(c => c.A.X).ThenBy(c => c.A.Y))
        {
            if (map.Corridors.Count(c => c.Kind == CorridorKind.Pass) >= passes) break;
            Vector2 mid = (cand.A + cand.B) * 0.5f;
            if (map.Corridors.Where(c => c.Kind == CorridorKind.Pass).Any(c => Vector2.Distance((c.Points[0] + c.Points[1]) * 0.5f, mid) < 25f)) continue;
            var pass = new Corridor { Kind = CorridorKind.Pass, Width = rng.Range(3f, 5f), Points = new List<Vector2> { cand.A, cand.B } };
            CarveLine(map, cand.A, cand.B, pass.Width);
            map.Corridors.Add(pass);
        }

        // Arches: peak pairs flanking a corridor, bridged by a 10–20 m span across it.
        List<(Vector2 A, Vector2 B, int Corridor)> ArchCandidates(float threshold)
        {
            var found = new List<(Vector2 A, Vector2 B, int Corridor)>();
            for (int c = 0; c < map.Corridors.Count; c++)
            {
                var corridor = map.Corridors[c];
                if (corridor.Kind != CorridorKind.Main) continue;
                for (int i = 2; i + 2 < corridor.Points.Count; i += 2)
                {
                    Vector2 p = corridor.Points[i];
                    if (Vector2.Distance(p, map.Start.Position) < 20f || Vector2.Distance(p, map.Rift.Position) < 24f) continue;
                    if (map.Plazas.Any(z => Vector2.Distance(z.Center, p) < 12f)) continue;
                    Vector2 tangent = Geo.Normalize(corridor.Points[i + 2] - corridor.Points[i - 2], Vector2.UnitX);
                    Vector2 nrm = Geo.Perp(tangent);
                    float sa = Footing(map, p, nrm, corridor.HalfWidth, threshold), sb = Footing(map, p, -nrm, corridor.HalfWidth, threshold);
                    if (sa < 0f || sb < 0f) continue;
                    float span = sa + sb;
                    if (span is < 10f or > 20f) continue;
                    found.Add((p + nrm * sa, p - nrm * sb, c));
                }
            }
            return found;
        }
        var archCandidates = new List<(Vector2 A, Vector2 B, int Corridor)>();
        // Footings stand where the flank is well above the swim level.
        foreach (float threshold in new[] { 6f, 5f, 4f })
        {
            archCandidates = ArchCandidates(threshold);
            if (archCandidates.Count > 0) break;
        }
        // Where no flank stands high enough, a pair of rounded rock shoulders rises either side of a corridor to carry one.
        if (archCandidates.Count == 0 && RaiseShoulders(map, rng)) archCandidates = ArchCandidates(4f);
        if (archCandidates.Count == 0) return "no peaks flank a corridor for an arch";
        int arches = 1 + rng.Int(5);
        var pool = archCandidates.ToList();
        while (map.Canopies.Count(c => c.Kind == CanopyKind.Arch) < arches && pool.Count > 0)
        {
            var (a, b, c) = pool[rng.Int(pool.Count)];
            pool.RemoveAll(x => Vector2.Distance((x.A + x.B) * 0.5f, (a + b) * 0.5f) < 20f);
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
            Vector2 tangent = Geo.Normalize(corridor.Points[i + 1] - corridor.Points[i - 1], Vector2.UnitX);
            Vector2 nrm = Geo.Perp(tangent) * (rng.NextFloat() < 0.5f ? -1f : 1f);
            if (map.HeightAt(p + nrm * (corridor.HalfWidth + 4f)) < 8f) continue;
            Vector2 at = p + nrm * (corridor.HalfWidth + 0.5f);
            if (map.Canopies.Any(c => Vector2.Distance(c.Center, at) < 15f)) continue;
            map.Canopies.Add(new Canopy
            {
                Kind = CanopyKind.Overhang,
                Center = at,
                Axis = tangent,
                HalfLength = rng.Range(3f, 5f),
                HalfWidth = rng.Range(1.5f, 2.5f),
                Bottom = 5.5f,
                Top = 7.5f,
                Corridor = map.Corridors.IndexOf(corridor),
            });
        }
        return null;
    }

    static Vector2 Rotate(Vector2 v, float a) => new(v.X * MathF.Cos(a) - v.Y * MathF.Sin(a), v.X * MathF.Sin(a) + v.Y * MathF.Cos(a));

    static bool ChamberClear(LevelMap map, Vector2 c, float r, int poi)
    {
        if (!Inside(c, Rim + r + 2f)) return false;
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
            if (Vector2.Distance(p, map.Start.Position) < 24f || Vector2.Distance(p, map.Rift.Position) < 28f) continue;
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
                    if (k <= 0f || map[x, y] >= ShoulderHeight) continue;
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

    static void Decorate(LevelMap map, Rng rng, uint seed)
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
            if (h < -TrenchMin * 0.8f)
            {
                // The deeps: dark and quiet.
                if (roll < 0.12f) map.Decor.Add(new DecorSpot(DecorKind.TrenchSponge, p, h, scale, yaw));
                else if (roll < 0.2f) map.Decor.Add(new DecorSpot(DecorKind.BiolumAccent, p, h, scale, yaw));
            }
            else if (h < -0.5f)
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
        int coins = 8 + rng.Int(5);
        for (int tries = 0; tries < 400 && map.Coins.Count < coins; tries++)
        {
            Vector2 p = OnFlat(0.8f);
            if (map.HeightAt(p) > -PathDepth || map.Coins.Any(c => Vector2.Distance(c.Position, p) < 10f)) continue;
            if (Vector2.Distance(p, map.Start.Position) < 12f || Vector2.Distance(p, map.Rift.Position) < map.Rift.Radius) continue;
            map.Coins.Add(new BuriedCoins(p, rng.NextFloat() < 0.1f ? 5 : 2 + rng.Int(2)));
        }
        int pockets = 4 + rng.Int(3);
        for (int tries = 0; tries < 400 && map.Pockets.Count < pockets; tries++)
        {
            Vector2 p = OnFlat(0.9f);
            if (map.HeightAt(p) > -PathDepth || map.Pockets.Any(c => Vector2.Distance(c.Position, p) < 15f)) continue;
            if (map.Coins.Any(c => Vector2.Distance(c.Position, p) < 4f)) continue;
            if (Vector2.Distance(p, map.Start.Position) < 15f || Vector2.Distance(p, map.Rift.Position) < map.Rift.Radius + 4f) continue;
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

    static void PlaceSpawns(LevelMap map, Rng rng, int depth)
    {
        float menace = MathUtil.Clamp01((depth - 1) / 5f);
        float budget = 1f + 0.8f * menace;
        int Scaled(int n) => Math.Max(1, (int)MathF.Round(n * budget));

        // Landmark lights are breathing room: ambient spawns keep out of their radius (curse dens excepted).
        var beacons = map.Pois.Where(p => p.Kind is PoiKind.Start or PoiKind.Shop or PoiKind.TreasureCave or PoiKind.Rift).ToList();
        bool NearBeacon(Vector2 p) => beacons.Any(b => Vector2.Distance(b.Position, p) < b.Radius + 12f);

        void Add(SpawnRole role, EnemyKind kind, int count, Vector2 at, int poi = -1, bool trap = false, List<Vector2>? path = null) =>
            map.Spawns.Add(new SpawnEntry { Role = role, Kind = kind, Class = ClassOf(kind), Count = count, Position = at, Poi = poi, Trap = trap, Path = path });

        EnemyKind DenKind() => rng.NextFloat() < 0.75f ? Swimmers[rng.Int(Swimmers.Length)] : EnemyKind.Crabby;

        // Dens in caves (the shop is safe water) and in pockets along the ridges.
        for (int pi = 0; pi < map.Pois.Count; pi++)
        {
            var poi = map.Pois[pi];
            if (poi.Kind is PoiKind.CurseDen) Add(SpawnRole.Den, DenKind(), Scaled(3 + rng.Int(2)), poi.Position, pi);
            if (poi.Kind is PoiKind.TreasureCave && poi.Cave >= 0 && map.Caves[poi.Cave].Chambers.Count > 1)
                Add(SpawnRole.Den, DenKind(), Scaled(2 + rng.Int(2)), map.Caves[poi.Cave].Chambers[^1].Center, pi);
        }
        int ridgeDens = 3 + rng.Int(3);
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
        int ambushers = 4 + rng.Int(5);
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
                case PoiKind.ItemSpawn:
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
