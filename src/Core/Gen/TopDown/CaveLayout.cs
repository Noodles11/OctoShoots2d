using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>
/// The level's cave (docs/CAVEGEN.md, ported from tools/cavegen/cavegen.js): a chain of chambers from the start to the
/// way down, side branches and the odd loop, joined by winding tunnels, rasterised as craggy open water on the 1 m grid
/// and grown until the water she can reach covers the area rule. Special rooms take dead-end chambers left as rock:
/// their branch tunnel ends at the room's mouth, and the generator hollows the cave out behind it.
/// </summary>
public sealed class CaveLayout
{
    const int N = LevelMap.Samples;
    const float Size = LevelMap.Size;
    const float Rim = LevelMap.RimWidth;

    /// <summary>Reachable water the level aims for (m²), and how far it may stray (the validator's band).</summary>
    public const float Area = 6000f, AreaSlack = 0.15f;
    public const float AreaMin = Area * (1f - AreaSlack), AreaMax = Area * (1f + AreaSlack);
    const float MinPath = LevelValidator.MinExitGeodesic, MinStraight = 78f;
    static readonly (float Lo, float Hi) StepMain = (16f, 22f), StepBranch = (14f, 21f);
    static readonly (float Lo, float Hi) RadiusMain = (6f, 10f), RadiusBranch = (5f, 9f), RadiusStart = (9f, 11f);
    static readonly (float Lo, float Hi) WidthMain = (6f, 9f), WidthBranch = (5f, 7f), WidthLoop = (4.5f, 6f);
    const float LoopChance = 0.25f, LoopReach = 26f;
    /// <summary>The craggy edge: two octaves of noise on the outline (metres).</summary>
    const float Crag = 1.6f, CragFine = 0.6f;
    /// <summary>
    /// Rock kept round a room's chamber, past its radius: its walls and the crag's reach, more for a treasure cave's
    /// deeper chambers and most for the curse den's winding tunnel.
    /// </summary>
    static float RoomKeep(PoiKind kind) => kind switch { PoiKind.CurseDen => 9f, PoiKind.TreasureCave => 8f, _ => 6f };
    const int RoomTries = 600;
    // The validator's scatter rules.
    const float FromStart = 39f, FromExit = 40f, Spacing = 21f, CaveSpacing = 25f, EarlyMax = 47f;
    const int Misses = 1500;

    public enum Role { Start, Main, Branch, Exit }

    public sealed class Chamber
    {
        public Vector2 At;
        public float R;
        public Role Role;
        public (int K, float A, float P)[] Harm = Array.Empty<(int, float, float)>();
        public readonly List<Chamber> Links = new();
        /// <summary>A special room's chamber: left as rock (the cave is hollowed out later), with this much rock kept round it.</summary>
        public bool Room;
        public float Keep;
    }

    public sealed class Tunnel
    {
        public Chamber A = null!, B = null!;
        public float Width;
        public CorridorKind Kind;
        public List<Vector2> Points = new();
        public float Phase;
    }

    public sealed class Place
    {
        public PoiKind Kind;
        public Vector2 At;
        public float Radius;
        public bool Early;
        /// <summary>For a room: its spur (the branch tunnel, ending at the mouth) and the way out of the mouth.</summary>
        public Tunnel? Spur;
        public Vector2 Facing;
    }

    public readonly List<Chamber> Chambers = new();
    public readonly List<Tunnel> Tunnels = new();
    public readonly List<Place> Places = new();
    public Chamber Start = null!, Exit = null!;
    /// <summary>Open water joined to the start, on the grid points (index y·Samples + x).</summary>
    public bool[] Open = Array.Empty<bool>();
    public int OpenArea;
    public float PathToExit;

    /// <summary>
    /// One cave for the level, or why it failed. <paramref name="kinds"/> are the plan's places in order of preference
    /// for the early one; <paramref name="radius"/> gives each its room or clearing.
    /// </summary>
    public static (CaveLayout? Layout, string? Why) Build(Rng rng, bool boss, bool fromArena, IReadOnlyList<PoiKind> kinds, Func<PoiKind, float> radius)
    {
        var g = new CaveLayout();
        uint seed = (uint)(rng.NextU64() >> 34);
        g.MainChain(rng, boss, fromArena);
        if (Vector2.Distance(g.Exit.At, g.Start.At) < MinStraight) return (null, "the way down is too close to the start");
        // Special rooms first, off the main chain, while the rock round it is still whole.
        if (g.PlaceRooms(rng, kinds, radius) is { } why) return (null, why);

        // Grow side branches until the water covers the area: first by estimate (cheap), then measured.
        g.Rasterise(seed);
        float ratio = g.OpenArea / g.Estimate();
        for (int misses = 0; g.Estimate() * ratio < Area && misses < Misses;)
            if (!g.TryBranch(rng)) misses++;
        g.Rasterise(seed);
        g.GrowMeasured(rng, seed);
        // Where branches no longer fit (rooms crowding the rock), the chambers swell instead.
        g.Swell(seed);
        if (g.OpenArea < AreaMin || g.OpenArea > AreaMax) return (null, $"cave water {g.OpenArea} m² (want {AreaMin:0}–{AreaMax:0})");

        var fromStart = g.Geodesic(g.Start.At);
        g.PathToExit = fromStart[Index(g.Exit.At)];
        if (!float.IsFinite(g.PathToExit) || g.PathToExit < MinPath) return (null, $"the way down is {g.PathToExit:0} m from the start along the water");
        if (g.PlaceOpen(kinds, radius) is { } missing) return (null, missing);
        return (g, null);
    }

    // ───────────────────────── the graph ─────────────────────────

    static float Pick(Rng rng, (float Lo, float Hi) r) => rng.Range(r.Lo, r.Hi);

    static bool InBounds(Vector2 p, float r)
    {
        float m = Rim + r + 4f;
        return p.X >= m && p.Y >= m && p.X <= Size - m && p.Y <= Size - m;
    }

    static Vector2 Clamp(Vector2 p, float r)
    {
        float m = Rim + r + 4f;
        return new Vector2(Math.Clamp(p.X, m, Size - m), Math.Clamp(p.Y, m, Size - m));
    }

    /// <summary>A chamber's organic outline: its radius wavers round it (four low harmonics).</summary>
    static Chamber MakeChamber(Rng rng, Vector2 at, float r, Role role)
    {
        var harm = new (int, float, float)[4];
        for (int k = 2; k <= 5; k++) harm[k - 2] = (k, rng.Range(0.03f, 0.12f) / MathF.Sqrt(k - 1), rng.Range(0f, MathF.Tau));
        return new Chamber { At = at, R = r, Role = role, Harm = harm };
    }

    /// <summary>A tunnel: a gentle curve (a quadratic Bézier) from one chamber to another, sampled every ~2 m.</summary>
    static Tunnel MakeTunnel(Rng rng, Chamber a, Chamber b, float width, CorridorKind kind)
    {
        Vector2 d = b.At - a.At;
        float l = d.Length();
        float bend = rng.Range(-0.3f, 0.3f) * l;
        Vector2 c = (a.At + b.At) * 0.5f + new Vector2(-d.Y, d.X) / l * bend;
        int n = Math.Max(4, (int)MathF.Ceiling(l / 2f));
        var points = new List<Vector2>(n + 1);
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n, u = 1f - t;
            points.Add(u * u * a.At + 2f * u * t * c + t * t * b.At);
        }
        a.Links.Add(b);
        b.Links.Add(a);
        return new Tunnel { A = a, B = b, Width = width, Kind = kind, Points = points, Phase = rng.Range(0f, 10f) };
    }

    static float Keep(Chamber c) => c.Keep;

    /// <summary>Whether a straight run from a to b keeps clear of every other chamber.</summary>
    bool ClearRun(Chamber a, Chamber b, Chamber? skip)
    {
        foreach (var c in Chambers)
        {
            if (c == a || c == b || c == skip) continue;
            if (Geo.SegmentDistance(c.At, a.At, b.At) < c.R + 3f + Keep(c)) return false;
        }
        return true;
    }

    /// <summary>The main chain: from the start on one side to the way down near the far side, wandering.</summary>
    void MainChain(Rng rng, bool boss, bool fromArena)
    {
        int side = rng.Int(4);
        float inset = 26f, along = rng.Range(0.3f, 0.7f) * Size, far = rng.Range(0.2f, 0.8f) * Size;
        static Vector2 P(int s, float t, float d) => s switch
        {
            0 => new Vector2(t, d),
            1 => new Vector2(Size - d, t),
            2 => new Vector2(t, Size - d),
            _ => new Vector2(d, t),
        };
        Vector2 s = P(side, along, inset), goal = P((side + 2) % 4, far, inset);
        // Arriving from a boss arena, she lands in a chamber as wide as the arena above.
        float startR = fromArena ? 16f : Pick(rng, RadiusStart);
        Start = MakeChamber(rng, Clamp(s, startR), startR, Role.Start);
        Chambers.Add(Start);
        var cur = Start;
        Vector2 dir = Vector2.Normalize(goal - s);
        for (int i = 0; i < 9; i++)
        {
            float toGoal = Vector2.Distance(goal, cur.At);
            if (toGoal < 20f) break;
            float wobble = rng.Range(-0.65f, 0.65f);
            Vector2 next = new(dir.X * MathF.Cos(wobble) - dir.Y * MathF.Sin(wobble), dir.X * MathF.Sin(wobble) + dir.Y * MathF.Cos(wobble));
            next = Vector2.Normalize(next * 0.55f + (goal - cur.At) / toGoal * 0.45f);
            float step = MathF.Min(Pick(rng, StepMain), toGoal - 8f);
            float r = Pick(rng, RadiusMain);
            var chamber = MakeChamber(rng, Clamp(cur.At + next * step, r), r, Role.Main);
            Chambers.Add(chamber);
            Tunnels.Add(MakeTunnel(rng, cur, chamber, Pick(rng, WidthMain), CorridorKind.Main));
            cur = chamber;
            dir = next;
        }
        float er = boss ? 17f : 10.5f;
        Exit = MakeChamber(rng, Clamp(goal, er), er, Role.Exit);
        // The arena stays round.
        if (boss) Exit.Harm = Exit.Harm.Select(h => (h.K, h.A * 0.3f, h.P)).ToArray();
        Chambers.Add(Exit);
        Tunnels.Add(MakeTunnel(rng, cur, Exit, Pick(rng, WidthMain), CorridorKind.Main));
    }

    /// <summary>One side branch off a chamber (and now and then a second tunnel making a loop); false when it does not fit.</summary>
    bool TryBranch(Rng rng) => Branch(rng) is { } added && Commit(added);

    (Chamber Chamber, List<Tunnel> Tunnels)? Branch(Rng rng)
    {
        var options = Chambers.Where(c => c.Role != Role.Exit && !c.Room).ToList();
        var parent = options[rng.Int(options.Count)];
        float a = rng.Range(0f, MathF.Tau), step = Pick(rng, StepBranch), r = Pick(rng, RadiusBranch);
        Vector2 at = parent.At + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (parent.R + step);
        if (!InBounds(at, r)) return null;
        foreach (var c in Chambers)
            if (Vector2.Distance(c.At, at) < c.R + r + 3f + Keep(c)) return null;
        var chamber = MakeChamber(rng, at, r, Role.Branch);
        if (!ClearRun(parent, chamber, null)) return null;
        foreach (var t in Tunnels)
        {
            if (t.A == parent || t.B == parent) continue;
            foreach (var p in t.Points)
                if (Vector2.Distance(p, at) < r + t.Width / 2f + 1f) return null;
        }
        var tunnels = new List<Tunnel> { MakeTunnel(rng, parent, chamber, Pick(rng, WidthBranch), CorridorKind.Side) };
        if (rng.NextFloat() < LoopChance)
        {
            Chamber? best = null;
            float bd = LoopReach;
            foreach (var o in Chambers)
            {
                if (o == parent || o.Role == Role.Exit || o.Room) continue;
                float d = Vector2.Distance(o.At, at) - o.R - r;
                if (d < bd && ClearRun(chamber, o, parent))
                {
                    bd = d;
                    best = o;
                }
            }
            if (best is not null) tunnels.Add(MakeTunnel(rng, chamber, best, Pick(rng, WidthLoop), CorridorKind.Side));
        }
        return (chamber, tunnels);
    }

    bool Commit((Chamber Chamber, List<Tunnel> Tunnels) added)
    {
        Chambers.Add(added.Chamber);
        Tunnels.AddRange(added.Tunnels);
        return true;
    }

    void Undo((Chamber Chamber, List<Tunnel> Tunnels) added)
    {
        Chambers.Remove(added.Chamber);
        foreach (var t in added.Tunnels)
        {
            Tunnels.Remove(t);
            t.A.Links.Remove(t.B);
            t.B.Links.Remove(t.A);
        }
    }

    float Estimate() =>
        Chambers.Where(c => !c.Room).Sum(c => MathF.PI * c.R * c.R)
        + Tunnels.Sum(t => MathF.Max(0f, Vector2.Distance(t.A.At, t.B.At) - t.A.R - t.B.R) * t.Width);

    /// <summary>Adds branches one at a time, measuring the water, until it covers the area (never past the band).</summary>
    void GrowMeasured(Rng rng, uint seed)
    {
        for (int misses = 0; OpenArea < Area && misses < Misses;)
        {
            if (Branch(rng) is not { } added)
            {
                misses++;
                continue;
            }
            Commit(added);
            int before = OpenArea;
            var open = Open;
            Rasterise(seed);
            if (OpenArea > AreaMax)
            {
                Undo(added);
                Open = open;
                OpenArea = before;
                misses++;
            }
        }
    }

    /// <summary>Widens the chambers a step at a time, clear of the rooms' rock and the rim, until the water covers the area.</summary>
    void Swell(uint seed)
    {
        for (int pass = 0; pass < 8 && OpenArea < AreaMin + 400f; pass++)
        {
            var was = Chambers.Select(c => c.R).ToArray();
            int before = OpenArea;
            var open = Open;
            foreach (var c in Chambers)
            {
                if (c.Room || c.Role == Role.Exit) continue;
                float r = c.R * 1.08f;
                if (!InBounds(c.At, r)) continue;
                if (Chambers.Any(o => o.Room && Vector2.Distance(o.At, c.At) - o.R - o.Keep < r * 1.2f + 1f)) continue;
                c.R = r;
            }
            Rasterise(seed);
            if (OpenArea > AreaMax)
            {
                for (int i = 0; i < Chambers.Count; i++) Chambers[i].R = was[i];
                Open = open;
                OpenArea = before;
                return;
            }
        }
    }

    // ───────────────────────── rasterising: the water ─────────────────────────

    static int Index(Vector2 p) => (int)MathF.Round(p.Y) * N + (int)MathF.Round(p.X);

    /// <summary>
    /// The signed distance to the cave (negative inside) as wobbly blobs and winding tubes whose width breathes along
    /// them, roughened by noise so the outline crags; then its inside smoothed twice (a majority vote), keeping only
    /// what joins the start.
    /// </summary>
    void Rasterise(uint seed)
    {
        var sdf = new float[N * N];
        Array.Fill(sdf, 1e9f);
        var core = new bool[N * N];
        foreach (var c in Chambers)
        {
            if (c.Room) continue;
            float reach = c.R * 1.6f + 2f;
            for (int y = Math.Max(0, (int)MathF.Floor(c.At.Y - reach)); y <= Math.Min(N - 1, (int)MathF.Ceiling(c.At.Y + reach)); y++)
            for (int x = Math.Max(0, (int)MathF.Floor(c.At.X - reach)); x <= Math.Min(N - 1, (int)MathF.Ceiling(c.At.X + reach)); x++)
            {
                float ang = MathF.Atan2(y - c.At.Y, x - c.At.X), k = 1f;
                foreach (var (hk, ha, hp) in c.Harm) k += ha * MathF.Cos(hk * ang + hp);
                float d = Vector2.Distance(new Vector2(x, y), c.At) - c.R * k;
                int i = y * N + x;
                if (d < sdf[i]) sdf[i] = d;
            }
        }
        foreach (var t in Tunnels)
        {
            var pts = t.Points;
            float reach = t.Width + 3f;
            for (int s = 0; s < pts.Count - 1; s++)
            {
                Vector2 a = pts[s], b = pts[s + 1];
                int x0 = Math.Max(0, (int)MathF.Floor(MathF.Min(a.X, b.X) - reach)), x1 = Math.Min(N - 1, (int)MathF.Ceiling(MathF.Max(a.X, b.X) + reach));
                int y0 = Math.Max(0, (int)MathF.Floor(MathF.Min(a.Y, b.Y) - reach)), y1 = Math.Min(N - 1, (int)MathF.Ceiling(MathF.Max(a.Y, b.Y) + reach));
                float half = t.Width / 2f * (1f + 0.25f * MathF.Sin(t.Phase + s * 0.55f));
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float along = Geo.SegmentDistance(new Vector2(x, y), a, b), d = along - half;
                    int i = y * N + x;
                    if (d < sdf[i]) sdf[i] = d;
                    // The tunnel's middle stays swimmable whatever the noise does to its walls.
                    if (along < 1.6f) core[i] = true;
                }
            }
        }
        var open = new bool[N * N];
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            int i = y * N + x;
            float v = sdf[i] + Fbm(x * 0.12f, y * 0.12f, seed) * Crag + Fbm(x * 0.37f, y * 0.37f, seed + 31) * CragFine;
            if (core[i]) v = MathF.Min(v, -1f);
            // Never into the rim.
            float edge = MathF.Min(MathF.Min(x, y), MathF.Min(Size - x, Size - y));
            if (edge < Rim + 1f) v = MathF.Max(v, Rim + 1f - edge);
            open[i] = v < 0f;
        }
        for (int pass = 0; pass < 2; pass++)
        {
            var next = new bool[N * N];
            for (int y = 1; y < N - 1; y++)
            for (int x = 1; x < N - 1; x++)
            {
                int n = 0;
                for (int j = -1; j <= 1; j++)
                for (int k = -1; k <= 1; k++)
                    if (open[(y + j) * N + x + k]) n++;
                next[y * N + x] = n >= 5;
            }
            open = next;
        }
        // Keep what joins the start.
        var keep = new bool[N * N];
        int area = 0, si = Index(Start.At);
        if (open[si])
        {
            var stack = new Stack<int>();
            stack.Push(si);
            keep[si] = true;
            while (stack.Count > 0)
            {
                int i = stack.Pop(), x = i % N, y = i / N;
                area++;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= N || ny >= N) continue;
                    int j = ny * N + nx;
                    if (open[j] && !keep[j])
                    {
                        keep[j] = true;
                        stack.Push(j);
                    }
                }
            }
        }
        Open = keep;
        OpenArea = area;
    }

    /// <summary>Distances through the water from a point (8-neighbour Dijkstra, no squeezing between rock corners).</summary>
    float[] Geodesic(Vector2 from)
    {
        var dist = new float[N * N];
        Array.Fill(dist, float.PositiveInfinity);
        int s = Index(from);
        if (!Open[s]) return dist;
        dist[s] = 0f;
        var queue = new PriorityQueue<int, float>();
        queue.Enqueue(s, 0f);
        while (queue.TryDequeue(out int i, out float d))
        {
            if (d > dist[i]) continue;
            int x = i % N, y = i / N;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= N || ny >= N) continue;
                int j = ny * N + nx;
                if (!Open[j]) continue;
                if (dx != 0 && dy != 0 && (!Open[y * N + nx] || !Open[ny * N + x])) continue;
                float nd = d + (dx != 0 && dy != 0 ? 1.41421356f : 1f);
                if (nd < dist[j])
                {
                    dist[j] = nd;
                    queue.Enqueue(j, nd);
                }
            }
        }
        return dist;
    }

    /// <summary>Chamfer distance (metres) from every grid point to the nearest point where <paramref name="source"/> holds.</summary>
    public static float[] Chamfer(bool[] cells, bool source)
    {
        var d = new float[N * N];
        for (int i = 0; i < d.Length; i++) d[i] = cells[i] == source ? 0f : 1e9f;
        const float D = 1.41421356f;
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            int i = y * N + x;
            if (x > 0) d[i] = MathF.Min(d[i], d[i - 1] + 1f);
            if (y > 0) d[i] = MathF.Min(d[i], d[i - N] + 1f);
            if (x > 0 && y > 0) d[i] = MathF.Min(d[i], d[i - N - 1] + D);
            if (x < N - 1 && y > 0) d[i] = MathF.Min(d[i], d[i - N + 1] + D);
        }
        for (int y = N - 1; y >= 0; y--)
        for (int x = N - 1; x >= 0; x--)
        {
            int i = y * N + x;
            if (x < N - 1) d[i] = MathF.Min(d[i], d[i + 1] + 1f);
            if (y < N - 1) d[i] = MathF.Min(d[i], d[i + N] + 1f);
            if (x < N - 1 && y < N - 1) d[i] = MathF.Min(d[i], d[i + N + 1] + D);
            if (x > 0 && y < N - 1) d[i] = MathF.Min(d[i], d[i + N - 1] + D);
        }
        return d;
    }

    // ───────────────────────── places ─────────────────────────

    bool Spaced(Vector2 p, PoiKind kind, bool early)
    {
        float fromStart = Vector2.Distance(p, Start.At);
        if (fromStart < FromStart || early && fromStart > EarlyMax) return false;
        if (Vector2.Distance(p, Exit.At) < FromExit) return false;
        foreach (var o in Places)
        {
            float d = Vector2.Distance(o.At, p);
            if (d < Spacing) return false;
            if (Poi.IsCaveHosted(kind) && Poi.IsCaveHosted(o.Kind) && d < CaveSpacing) return false;
        }
        return true;
    }

    /// <summary>
    /// The special rooms: each a dead-end branch off the cave so far, its chamber left as rock with rock kept round it,
    /// its tunnel stopping at the room's mouth (the first in the list early, when it fits there).
    /// </summary>
    string? PlaceRooms(Rng rng, IReadOnlyList<PoiKind> kinds, Func<PoiKind, float> radius)
    {
        for (int k = 0; k < kinds.Count; k++)
        {
            var kind = kinds[k];
            if (!Poi.IsCaveHosted(kind)) continue;
            float r = radius(kind), keep = RoomKeep(kind);
            bool placed = false;
            for (int tries = 0; tries < RoomTries && !placed; tries++)
            {
                // Only the first place in the list may be early (the generator orders them), for most of the tries.
                bool early = k == 0 && tries < RoomTries * 2 / 3;
                var options = Chambers.Where(c => c.Role != Role.Exit && !c.Room).ToList();
                var parent = options[rng.Int(options.Count)];
                float a = rng.Range(0f, MathF.Tau);
                Vector2 at = parent.At + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (parent.R + Pick(rng, StepBranch));
                if (!InBounds(at, r + keep - 4f) || !Spaced(at, kind, early)) continue;
                if (Chambers.Any(c => Vector2.Distance(c.At, at) < c.R * 1.2f + r + keep + c.Keep)) continue;
                if (Tunnels.Any(t => t.Points.Any(p => Vector2.Distance(p, at) < t.Width * 0.65f + r + keep))) continue;
                var room = MakeChamber(rng, at, r, Role.Branch);
                room.Room = true;
                room.Keep = keep;
                if (!ClearRun(parent, room, null)) continue;
                var tunnel = MakeTunnel(rng, parent, room, Pick(rng, WidthBranch), CorridorKind.Spur);
                // The tunnel runs from the cave to the room's mouth, and no further.
                var kept = tunnel.Points.TakeWhile(p => Vector2.Distance(p, at) > r + 1f).ToList();
                if (kept.Count < 2)
                {
                    parent.Links.Remove(room);
                    continue;
                }
                Vector2 facing = Vector2.Normalize(kept[^1] - at);
                kept.Add(at + facing * r);
                tunnel.Points = kept;
                Chambers.Add(room);
                Tunnels.Add(tunnel);
                Places.Add(new Place { Kind = kind, At = at, Radius = r, Early = early, Spur = tunnel, Facing = facing });
                placed = true;
            }
            if (!placed) return $"no room for the {kind}";
        }
        return null;
    }

    /// <summary>The open places (clearings in the water), the early one first when no room took it.</summary>
    string? PlaceOpen(IReadOnlyList<PoiKind> kinds, Func<PoiKind, float> radius)
    {
        var clearance = Chamfer(Open, false);
        var spots = Chambers.Where(c => c.Role is Role.Main or Role.Branch && !c.Room).Select(c => c.At).ToList();
        foreach (var t in Tunnels)
            for (int i = 2; i < t.Points.Count - 2; i += 3) spots.Add(t.Points[i]);
        bool Fits(Vector2 p, float r) => Open[Index(p)] && clearance[Index(p)] >= MathF.Min(r, 6f) * 0.8f;
        // A shell cache keeps to the middle of the way (30–70% along start → way down), off the main tunnels.
        bool Suits(Vector2 p, PoiKind kind)
        {
            if (kind != PoiKind.ShellCache) return true;
            Vector2 axis = Exit.At - Start.At;
            float t = Vector2.Dot(p - Start.At, axis) / axis.LengthSquared();
            return t is >= 0.32f and <= 0.68f
                && !Tunnels.Any(tn => tn.Kind == CorridorKind.Main && tn.Points.Zip(tn.Points.Skip(1)).Any(s => Geo.SegmentDistance(p, s.First, s.Second) <= tn.Width / 2f));
        }
        bool earlyTaken = Places.Any(p => p.Early);
        for (int k = 0; k < kinds.Count; k++)
        {
            var kind = kinds[k];
            if (Poi.IsCaveHosted(kind)) continue;
            float r = radius(kind);
            bool early = k == 0 && !earlyTaken;
            Vector2? at = null;
            foreach (var s in spots)
                if (Fits(s, r) && Suits(s, kind) && Spaced(s, kind, early))
                {
                    at = s;
                    break;
                }
            if (at is null && early)
            {
                early = false;
                foreach (var s in spots)
                    if (Fits(s, r) && Suits(s, kind) && Spaced(s, kind, false))
                    {
                        at = s;
                        break;
                    }
            }
            if (at is not { } p) return $"no room for the {kind}";
            Places.Add(new Place { Kind = kind, At = p, Radius = r, Early = early });
        }
        // Exactly one early place (unless a lone shell cache is all there is).
        if (Places.Count > 0 && !Places.Any(p => p.Early))
        {
            if (Places.All(p => p.Kind == PoiKind.ShellCache)) return null;
            // Swap a clearing into the early band if one can move there.
            for (int i = 0; i < Places.Count; i++)
            {
                var place = Places[i];
                if (place.Spur is not null) continue;
                Places.RemoveAt(i);
                foreach (var s in spots)
                    if (Fits(s, place.Radius) && Suits(s, place.Kind) && Spaced(s, place.Kind, true))
                    {
                        place.At = s;
                        place.Early = true;
                        break;
                    }
                Places.Insert(i, place);
                if (place.Early) return null;
            }
            return "no early place";
        }
        return null;
    }

    // ───────────────────────── portable hash noise ─────────────────────────

    static float Hash2(int x, int y, uint seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + (int)seed * 1442695041;
            h = (h ^ (int)((uint)h >> 13)) * 1274126177;
            h ^= (int)((uint)h >> 16);
            return (uint)h / 4294967296f;
        }
    }

    static float VNoise(float x, float y, uint seed)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Hash2(xi, yi, seed), b = Hash2(xi + 1, yi, seed), c = Hash2(xi, yi + 1, seed), d = Hash2(xi + 1, yi + 1, seed);
        return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
    }

    /// <summary>Two octaves, centred on 0 (about −1…1).</summary>
    public static float Fbm(float x, float y, uint seed) => (VNoise(x, y, seed) * 0.66f + VNoise(x * 2.03f, y * 2.03f, seed + 7) * 0.34f) * 2f - 1f;
}
