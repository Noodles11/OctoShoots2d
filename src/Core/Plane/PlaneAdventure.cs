using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Plane;

/// <summary>The adventure's numbers (docs/ADVENTURE.md): first guesses, tuned in play.</summary>
public static class AdventureTuning
{
    /// <summary>The ink dots lie on an even grid this far apart, each this big; at most MaxDots on a level.</summary>
    public const float Spacing = 0.8f, DotRadius = 0.135f;
    public const int Cells = (int)(LevelMap.Size / Spacing), MaxDots = 9000;
    /// <summary>Every SpreadEvery seconds (÷ 1 + SpreadPerMenace × Menace) each whole dot beside an empty cell divides into it.</summary>
    public const float SpreadEvery = 10f, SpreadPerMenace = 0.3f;
    /// <summary>Only ink joined, dot to dot, to an open vent spreads: to a dot within VentFeed of its mouth.</summary>
    public const float VentFeed = 2f;
    /// <summary>A dot her light has worn down (but not cleared) fills back at this rate (strength a second) once out of it.</summary>
    public const float Regrow = 0.6f;
    /// <summary>Her shots' pops and the Starburst take this many times their ground strength off a dot.</summary>
    public const float PopBite = 1.6f, BurstBite = 2f;
    /// <summary>A corrupted creature leaves a dot every TrailEvery metres it swims (into an empty cell).</summary>
    public const float TrailEvery = 2.5f;
    /// <summary>
    /// A dot cut off from every open vent (its patch cleared through, or its vent sealed; or a trail's, never joined to
    /// one) decays: it pops on its own after DecayMin seconds (+ DecayPerMenace × Menace), and up to DecayJitter more,
    /// its own share of it, so a patch goes out dot by dot rather than all at once. Joined to a vent again, it starts over.
    /// </summary>
    public const float DecayMin = 10f, DecayPerMenace = 20f, DecayJitter = 2f;

    /// <summary>How long a dot cut off from every vent lasts at this Menace (its own jitter not counted).</summary>
    public static float DecayAfter(float menace) => DecayMin + DecayPerMenace * menace;

    /// <summary>
    /// The vents: LevelVents a level on the walls' faces, VentSpacing apart and VentFromStart from her start (closer if the
    /// level has no room). Sealing VentsToClear of them opens the way on. Open, each pours a dot every VentEvery seconds
    /// (÷ 1 + VentPerMenace × Menace) into the nearest empty cell before it.
    /// </summary>
    public const int LevelVents = 5, VentsToClear = 3;
    public const float VentSpacing = 30f, VentFromStart = 24f, VentEvery = 1.6f, VentPerMenace = 0.5f;
    /// <summary>Sealed, a vent sends out a ring of light that clears the ink round it: out to SealWave metres in SealWaveSeconds.</summary>
    public const float SealWave = 6f, SealWaveSeconds = 0.6f;
    /// <summary>
    /// Sealing: VentHits shots (+ VentHitsPerMenace × Menace) striking the wall within VentReach of its mouth, one after
    /// another — VentHold seconds after the last, a hit drains away every VentDrain seconds. Each sealed is worth VentXp,
    /// and throws VentShells shells out of its mouth.
    /// </summary>
    public const int VentHits = 8, VentXp = 25, VentShells = 5;
    public const float VentHitsPerMenace = 2f, VentReach = 1.6f, VentHold = 1.4f, VentDrain = 0.3f;
    /// <summary>
    /// The deeper the level, the longer its vents have been pouring: each starts with a puddle of PuddlePerMenace ×
    /// Menace dots grown out of its mouth (none at Depth 1, 12 at Depth 2, 60 at Depth 6), its edge ragged by PuddleRagged.
    /// </summary>
    public const float PuddlePerMenace = 60f, PuddleRagged = 0.45f;

    /// <summary>
    /// A fish's gauge: it fills FishFill a second with a dot within FishSense, FishEat for each dot it eats, and drains
    /// FishDrain a second over clean water. Full, the fish turns.
    /// </summary>
    public const float FishSense = 1f, FishFill = 0.12f, FishEat = 0.2f, FishDrain = 0.05f;
    /// <summary>A spiked murkling's throw striking a fish fills its gauge (and those in the splash) this much.</summary>
    public const float ThrowFill = 0.6f;
    /// <summary>A pufferfish's gauge: PufferFill a second with a dot within PufferSense (and FishEat a dot it eats); full, it is an Inkwarden. PufferMin–Max a level.</summary>
    public const float PufferSense = 1.4f, PufferFill = 0.06f, PufferDrain = 0.03f;
    public const int PufferMin = 12, PufferMax = 18;
    /// <summary>Fish: this many times the depth's count of anemones, tangs and schools; schools of SchoolMin–SchoolMax.</summary>
    public const float FishMore = 3f;
    public const int SchoolMin = 12, SchoolMax = 24;

    /// <summary>
    /// Each fish kind has its own depth: there it lives in its full numbers, above it not at all, and below it at
    /// EarlierRate of them (the deeper depths keep the earlier fish, fewer). The Shallows' fish (the reef fish and the
    /// pufferfish) count the Kelp Jungle, where runs start, as their own too: BaseHome.
    /// </summary>
    public const float EarlierRate = 0.6f;
    public const int BaseHome = 2;

    /// <summary>How many of a fish whose own depth is <paramref name="home"/> live at <paramref name="depth"/>, as a share of its full numbers.</summary>
    public static float Rate(int home, int depth) => depth < home ? 0f : depth == home ? 1f : EarlierRate;

    /// <summary>Spiked murkling (a blue tang): keeps KeepMin–KeepMax metres from her at SpikedSpeed; a touch hurts and knocks her off at SpikedKnock.</summary>
    public const float KeepMin = 6f, KeepMax = 9f, SpikedSpeed = 3.4f, SpikedKnock = 7f, SpikedRadius = 0.38f, SpikedSense = 16f;
    /// <summary>Tadpole murkling (a clownfish): this small, chasing at TadpoleSpeed times her cruise.</summary>
    public const float TadpoleRadius = 0.22f, TadpoleSpeed = 1.1f, TadpoleSense = 16f;
    /// <summary>Shoal murkling (a chromis): very fast, on a ring ShoalRing (+ a layer) round her, circling her at ShoalOrbit radians a second.</summary>
    public const float ShoalSpeed = 9f, ShoalRing = 1.8f, ShoalLayer = 0.4f, ShoalRadius = 0.2f, ShoalSense = 14f, ShoalOrbit = 1.1f;

    /// <summary>
    /// Her oral arm: ArmBase metres long, ArmPerUpgrade longer for each skill point she has spent (at most ArmMax). It
    /// trails her along the way she swam (her last ArmMemory seconds of it, so it gathers in when she stops). A
    /// corrupted creature it touches (within ArmWidth of it) takes ArmShare of a shot's damage, once every ArmEvery.
    /// </summary>
    public const float ArmBase = 3.2f, ArmPerUpgrade = 0.2f, ArmMax = 6f, ArmMemory = 1.5f, ArmWidth = 0.14f, ArmShare = 0.3f, ArmEvery = 0.4f;

    /// <summary>Her oral arm's length after so many skill points spent.</summary>
    public static float ArmLength(int upgrades) => System.MathF.Min(ArmBase + ArmPerUpgrade * upgrades, ArmMax);
}

/// <summary>
/// The ink in the water (docs/ADVENTURE.md §2): round dots on an even grid at her swim plane, each with its strength
/// (1 whole, worn down by her light; gone at 0). Cells in rock and in safe places never hold one; Links says which of a
/// cell's four neighbours the ink may divide into (no rock between).
/// </summary>
public sealed class InkDots
{
    public const int N = AdventureTuning.Cells;
    const float S = AdventureTuning.Spacing;

    /// <summary>Strength per cell (0: no dot), row-major.</summary>
    public readonly float[] Ink = new float[N * N];
    /// <summary>Where a dot may lie, and which neighbours it reaches (bits: east, south, west, north).</summary>
    public readonly bool[] Open = new bool[N * N];
    public readonly byte[] Links = new byte[N * N];
    /// <summary>The dots, in the order they came (each one's place in it in Slot).</summary>
    public readonly List<int> Live = new();
    readonly int[] _slot = new int[N * N];
    /// <summary>The step each cell was last in her light (it does not regrow then).</summary>
    readonly long[] _lit = new long[N * N];
    /// <summary>Dots joined (through other dots) to an open vent's mouth: only these spread (<see cref="Feed"/>).</summary>
    public readonly bool[] Fed = new bool[N * N];
    /// <summary>A fed dot's steps from the nearest open vent's mouth, dot to dot (the view's brackets hop outward along them).</summary>
    public readonly int[] Hops = new int[N * N];
    /// <summary>Seconds each dot has been cut off from every open vent (0 while fed).</summary>
    public readonly float[] Adrift = new float[N * N];

    /// <summary>A dot's own share of the decay's jitter (0..1), the same for its cell every time.</summary>
    public static float DecayShare(int i)
    {
        uint h = unchecked((uint)i * 2654435761u);
        h ^= h >> 15;
        h = unchecked(h * 2246822519u);
        h ^= h >> 13;
        return (h & 0xFFFFFF) / 16777216f;
    }
    readonly Queue<int> _walk = new();

    /// <summary>
    /// Marks the dots fed by open vents: every dot reached from the dots at their mouths, neighbour to neighbour across
    /// the ink's links. A patch cut off from every working vent (cleared through, or its vent sealed) is not.
    /// </summary>
    public void Feed(IEnumerable<int> mouths)
    {
        foreach (int i in Live) Fed[i] = false;
        _walk.Clear();
        foreach (int m in mouths)
            if (m >= 0 && Ink[m] > 0f && !Fed[m])
            {
                Fed[m] = true;
                Hops[m] = 0;
                _walk.Enqueue(m);
            }
        while (_walk.Count > 0)
        {
            int c = _walk.Dequeue();
            for (int k = 0; k < 4; k++)
            {
                int n = Neighbour(c, k);
                if (n < 0 || Ink[n] <= 0f || Fed[n]) continue;
                Fed[n] = true;
                Hops[n] = Hops[c] + 1;
                _walk.Enqueue(n);
            }
        }
    }

    public static readonly (int Dx, int Dy)[] Steps = { (1, 0), (0, 1), (-1, 0), (0, -1) };

    public int Count => Live.Count;

    public static Vector2 Centre(int i) => new((i % N + 0.5f) * S, (i / N + 0.5f) * S);

    public static int CellOf(Vector2 p)
    {
        int x = (int)MathF.Floor(p.X / S), y = (int)MathF.Floor(p.Y / S);
        return x < 0 || y < 0 || x >= N || y >= N ? -1 : y * N + x;
    }

    public bool Has(int i) => i >= 0 && Ink[i] > 0f;

    public static InkDots ForMap(LevelMap map)
    {
        var dots = new InkDots();
        var safes = CorruptionField.SafeOf(map).ToList();
        for (int i = 0; i < N * N; i++)
        {
            var c = Centre(i);
            if (!map.IsOpen(c)) continue;
            bool clear = true;
            for (int k = 0; k < 4 && clear; k++)
                clear = map.IsOpen(c + new Vector2(Steps[k].Dx, Steps[k].Dy) * 0.3f);
            if (!clear || safes.Any(s => Vector2.Distance(c, s.Centre) < s.Radius + 2f)) continue;
            dots.Open[i] = true;
        }
        for (int i = 0; i < N * N; i++)
        {
            if (!dots.Open[i]) continue;
            int x = i % N, y = i / N;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + Steps[k].Dx, ny = y + Steps[k].Dy;
                if (nx < 0 || ny < 0 || nx >= N || ny >= N || !dots.Open[ny * N + nx]) continue;
                if (map.IsOpen((Centre(i) + Centre(ny * N + nx)) * 0.5f)) dots.Links[i] |= (byte)(1 << k);
            }
        }
        return dots;
    }

    /// <summary>The neighbour of a cell one step along k (−1 when the ink cannot reach it).</summary>
    public int Neighbour(int i, int k) => (Links[i] & (1 << k)) == 0 ? -1 : i + Steps[k].Dx + Steps[k].Dy * N;

    public bool Add(int i)
    {
        if (i < 0 || !Open[i] || Ink[i] > 0f || Live.Count >= AdventureTuning.MaxDots) return false;
        Ink[i] = 1f;
        Adrift[i] = 0f;
        _slot[i] = Live.Count;
        Live.Add(i);
        return true;
    }

    public void Remove(int i)
    {
        if (i < 0 || Ink[i] <= 0f) return;
        Ink[i] = 0f;
        int at = _slot[i], last = Live[^1];
        Live[at] = last;
        _slot[last] = at;
        Live.RemoveAt(Live.Count - 1);
    }

    /// <summary>
    /// Light on the dots round a point: strength off each (full at the centre, a third at the rim). Those it empties are
    /// gone and listed in <paramref name="cleared"/>; the rest do not regrow this step.
    /// </summary>
    public void Light(Vector2 at, float radius, float strength, long tick, List<Vector2> cleared)
    {
        Span(at, radius, out int x0, out int y0, out int x1, out int y1);
        float r2 = radius * radius;
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            int i = y * N + x;
            if (Ink[i] <= 0f) continue;
            float d2 = Vector2.DistanceSquared(Centre(i), at);
            if (d2 > r2) continue;
            float k = 1f - d2 / r2;
            Ink[i] -= strength * (0.35f + 0.65f * k);
            _lit[i] = tick;
            if (Ink[i] > 0f) continue;
            Ink[i] = 1f;
            Remove(i);
            cleared.Add(Centre(i));
        }
    }

    /// <summary>Worn dots out of her light fill back.</summary>
    public void Regrow(long tick, float step)
    {
        foreach (int i in Live)
            if (Ink[i] < 1f && _lit[i] != tick) Ink[i] = MathF.Min(1f, Ink[i] + step);
    }

    /// <summary>The nearest empty cell the ink can reach from a cell, within so many steps (−1: none).</summary>
    public int NearestEmpty(int from, int steps)
    {
        if (from < 0 || !Open[from]) return -1;
        if (Ink[from] <= 0f) return from;
        var seen = new HashSet<int> { from };
        var ring = new List<int> { from };
        for (int s = 0; s < steps && ring.Count > 0; s++)
        {
            var next = new List<int>();
            foreach (int c in ring)
                for (int k = 0; k < 4; k++)
                {
                    int n = Neighbour(c, k);
                    if (n < 0 || !seen.Add(n)) continue;
                    if (Ink[n] <= 0f) return n;
                    next.Add(n);
                }
            ring = next;
        }
        return -1;
    }

    /// <summary>The cells a disc covers (clamped to the grid).</summary>
    public static void Span(Vector2 at, float radius, out int x0, out int y0, out int x1, out int y1)
    {
        x0 = Math.Max(0, (int)((at.X - radius) / S));
        y0 = Math.Max(0, (int)((at.Y - radius) / S));
        x1 = Math.Min(N - 1, (int)((at.X + radius) / S));
        y1 = Math.Min(N - 1, (int)((at.Y + radius) / S));
    }

    /// <summary>The nearest dot within a reach of a point (−1: none).</summary>
    public int Nearest(Vector2 at, float reach)
    {
        Span(at, reach, out int x0, out int y0, out int x1, out int y1);
        int best = -1;
        float bestD = reach * reach;
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            int i = y * N + x;
            if (Ink[i] <= 0f) continue;
            float d2 = Vector2.DistanceSquared(Centre(i), at);
            if (d2 > bestD) continue;
            bestD = d2;
            best = i;
        }
        return best;
    }
}

/// <summary>
/// A corruption vent (docs/ADVENTURE.md §1): a geyser-like opening in a reef wall, its mouth toward open water, pouring
/// ink dots out until she seals it with her shots.
/// </summary>
public sealed class PlaneVent
{
    /// <summary>Its mouth in the water before the wall, the way it faces (out into the water), and the rock it opens in.</summary>
    public Vector2 Position, Facing, Wall;
    public int Hits, Needed;
    /// <summary>Light short of a whole hit (a small shot is only part of one).</summary>
    public float Credit;
    public bool Sealed;
    /// <summary>Since the last hit that counted (its light drains once VentHold has passed), the drain's clock, the stream's clock.</summary>
    public float Since, DrainClock, StreamTimer;
    /// <summary>Its sealing's ring of light running out over the ink (its radius; negative: none).</summary>
    public float Wave = -1f;
    /// <summary>How full of her light it is, 0..1.</summary>
    public float Glow => Needed <= 0 ? 1f : Hits / (float)Needed;
}

public sealed partial class PlaneWorld
{
    /// <summary>The ink dots in the water (the adventure; null otherwise).</summary>
    public InkDots? Dots { get; private set; }
    public List<PlaneVent> Vents { get; } = new();
    public int VentsOpen => Vents.Count(v => !v.Sealed);
    /// <summary>How many more vents she must seal before the way on opens.</summary>
    public int VentsLeft => Math.Max(Math.Min(AdventureTuning.VentsToClear, Vents.Count) - (Vents.Count - VentsOpen), 0);

    Rng _inkRng = null!;
    readonly List<Vector2> _cleared = new();

    /// <summary>The adventure's level: the dots' grid, the vents, the way down shut until they are sealed (and its boss freed).</summary>
    void PlaceAdventure()
    {
        _inkRng = new Rng(Map.Seed ^ 0x1DC0757UL ^ ((ulong)Map.Level << 21) ^ ((ulong)Map.Depth << 29) ^ ((ulong)Map.Attempt << 45));
        Dots = InkDots.ForMap(Map);
        PlaceVents();
        PlacePuddles(Dots);
        ExitOpen = Vents.Count == 0 && Boss is null;
    }

    void PlaceVents()
    {
        // Where she can swim with the boss's walls shut: they wait for every vent, so none may lie behind them.
        var bossWalls = _rocks.Where(r => r.Kind == SealKind.Boss).ToList();
        var reach = LevelValidator.Distances(Map, Map.Start.Position, blocked: q => bossWalls.Exists(r => r.Contains(q)));
        var safes = SafePlaces().ToList();
        var candidates = new List<Vector2>();
        foreach (var c in Map.Corridors.Where(c => c.Kind is CorridorKind.Main or CorridorKind.Side))
            for (int i = 0; i < c.Points.Count; i += 3) candidates.Add(c.Points[i]);
        candidates.AddRange(Map.Plazas.Select(z => z.Center));
        foreach (var ch in Map.Chambers) candidates.Add(ch.At);
        int needed = AdventureTuning.VentHits + (int)MathF.Round(AdventureTuning.VentHitsPerMenace * Menace);
        PlaceVents(candidates, reach, safes, needed);
        if (Vents.Count >= AdventureTuning.LevelVents) return;
        // Wide chambers can hold their walls out of reach of those spots: look from all the water she can swim.
        candidates.Clear();
        for (int y = 2; y < LevelMap.Size; y += 3)
            for (int x = 2; x < LevelMap.Size; x += 3)
                if (!float.IsPositiveInfinity(LevelValidator.DistanceAt(reach, new Vector2(x, y)))) candidates.Add(new Vector2(x, y));
        PlaceVents(candidates, reach, safes, needed);
    }

    /// <summary>Opens vents on the wall faces round these spots, until the level has its number of them.</summary>
    void PlaceVents(List<Vector2> candidates, float[] reach, List<(Vector2 Centre, float Radius)> safes, int needed)
    {
        // Each spot looks round for the nearest wall face in a few directions: a vent may open there.
        var faces = new List<(Vector2 Mouth, Vector2 Facing, Vector2 Wall)>();
        foreach (var from in candidates)
        {
            if (!Map.IsOpen(from)) continue;
            float turn = _inkRng.Range(0f, MathF.Tau);
            for (int k = 0; k < 6; k++)
            {
                float a = turn + k * MathF.Tau / 6f;
                var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
                for (float d = 0.5f; d < 9f; d += 0.25f)
                {
                    var q = from + dir * d;
                    if (Map.IsOpen(q)) continue;
                    // A thick wall (not a sliver), not a sealed wall, its mouth clear in the water before it.
                    if (Map.IsOpen(q + dir * 1.5f) || _rocks.Any(r => r.Contains(q, 1f))) break;
                    var mouth = q - dir * 0.7f;
                    if (Clear(mouth, 0.35f)) faces.Add((mouth, -dir, q));
                    break;
                }
            }
        }
        Shuffle(faces, _inkRng);
        int want = AdventureTuning.LevelVents;
        // Far apart if the level allows it, closer if it must.
        for (float spacing = AdventureTuning.VentSpacing; spacing > 3.5f && Vents.Count < want; spacing *= 0.75f)
            foreach (var (mouth, facing, wall) in faces)
            {
                if (Vents.Count >= want) break;
                if (float.IsPositiveInfinity(LevelValidator.DistanceAt(reach, mouth))) continue;
                if (Vector2.Distance(mouth, Map.Start.Position) < MathF.Min(AdventureTuning.VentFromStart, spacing)) continue;
                if (Vector2.Distance(mouth, Map.Exit.Position) < Map.Exit.Radius + 6f || Map.Shaft.Contains(mouth, 4f)) continue;
                if (safes.Any(s => Vector2.Distance(mouth, s.Centre) < s.Radius + 4f)) continue;
                if (Vents.Any(v => Vector2.Distance(v.Position, mouth) < spacing)) continue;
                Vents.Add(new PlaneVent
                {
                    Position = mouth, Facing = facing, Wall = wall, Needed = needed,
                    StreamTimer = _inkRng.Range(0.2f, AdventureTuning.VentEvery),
                });
            }
    }

    /// <summary>
    /// The ink a deeper level starts with: round each vent a puddle (PuddlePerMenace × Menace dots), grown out of its
    /// mouth as the ink itself spreads — neighbour to neighbour through open water only, the nearest first, its edge
    /// ragged (each cell's distance stretched by up to PuddleRagged at random) — so it lies joined to its vent, fed.
    /// </summary>
    void PlacePuddles(InkDots dots)
    {
        int size = (int)MathF.Round(AdventureTuning.PuddlePerMenace * Menace);
        if (size <= 0) return;
        foreach (var v in Vents)
        {
            int mouth = InkDots.CellOf(v.Position + v.Facing * 0.6f);
            if (mouth < 0 || !dots.Open[mouth]) mouth = InkDots.CellOf(v.Position + v.Facing * 1.4f);
            if (mouth < 0 || !dots.Open[mouth]) continue;
            var reach = new PriorityQueue<int, float>();
            var seen = new HashSet<int> { mouth };
            reach.Enqueue(mouth, 0f);
            int placed = 0;
            while (placed < size && reach.TryDequeue(out int cell, out _))
            {
                if (dots.Add(cell) || dots.Has(cell)) placed++;
                for (int k = 0; k < 4; k++)
                {
                    int next = dots.Neighbour(cell, k);
                    if (next < 0 || !seen.Add(next)) continue;
                    float far = Vector2.Distance(InkDots.Centre(next), v.Position) * (1f + AdventureTuning.PuddleRagged * _inkRng.NextFloat());
                    reach.Enqueue(next, far);
                }
            }
        }
        dots.Feed(VentMouths(dots));
    }

    /// <summary>The adventure's turn, after the war's: vents pour, the ink spreads and trails, fish and anemones fill.</summary>
    void StepAdventure()
    {
        StepArm();
        if (Dots is not { } dots) return;
        StepVents(dots);
        StepSpread(dots);
        StepDecay(dots);
        StepTrails(dots);
        StepGauges(dots);
        StepFlounders(dots);
        dots.Regrow(Tick, AdventureTuning.Regrow * Dt);
    }

    // ───────────────────────── the vents ─────────────────────────

    void StepVents(InkDots dots)
    {
        float every = AdventureTuning.VentEvery / (1f + AdventureTuning.VentPerMenace * Menace);
        foreach (var v in Vents)
        {
            if (v.Wave >= 0f)
            {
                // The sealing's ring of light: everything inside it is cleared as it runs out.
                v.Wave += AdventureTuning.SealWave / AdventureTuning.SealWaveSeconds * Dt;
                LightDots(v.Position, MathF.Min(v.Wave, AdventureTuning.SealWave), 3f);
                if (v.Wave >= AdventureTuning.SealWave) v.Wave = -1f;
            }
            if (v.Sealed) continue;
            // Her hits drain away when they stop coming.
            if (v.Hits > 0)
            {
                v.Since += Dt;
                if (v.Since >= AdventureTuning.VentHold)
                {
                    v.DrainClock += Dt;
                    if (v.DrainClock >= AdventureTuning.VentDrain)
                    {
                        v.DrainClock = 0f;
                        v.Hits--;
                    }
                }
            }
            v.StreamTimer -= Dt;
            if (v.StreamTimer > 0f) continue;
            v.StreamTimer += every;
            // A dot wells out of its mouth into the nearest empty cell before it, pushing the stream outward.
            int from = InkDots.CellOf(v.Position + v.Facing * 0.6f);
            if (from < 0 || !dots.Open[from]) from = InkDots.CellOf(v.Position + v.Facing * 1.4f);
            dots.Add(dots.NearestEmpty(from, 8));
        }
    }

    /// <summary>One of her shots struck rock here: on an open vent's mouth it adds its light; full, the vent seals. True if it counted.</summary>
    bool StrikeVent(Vector2 at, float light = 1f)
    {
        foreach (var v in Vents)
        {
            if (v.Sealed || Vector2.Distance(at, v.Wall) > AdventureTuning.VentReach) continue;
            // An Inkwarden in reach guards it: her light is turned aside.
            if (Ward(v, v.Position, 1f) <= 0f) return true;
            v.Since = v.DrainClock = 0f;
            v.Credit += light;
            if (v.Credit < 0.999f) return true;
            v.Credit -= 1f;
            v.Hits++;
            Events.Add(new PlaneEvent(PlaneEventType.VentLit, v.Position, v.Facing, v.Glow));
            if (v.Hits >= v.Needed) SealVent(v);
            return true;
        }
        return false;
    }

    void SealVent(PlaneVent v)
    {
        v.Sealed = true;
        v.Hits = v.Needed;
        v.Wave = 0f;
        Events.Add(new PlaneEvent(PlaneEventType.VentSealed, v.Position, v.Facing, VentsLeft));
        // Her reward: shells burst out of its mouth into the water before it.
        var across = new Vector2(-v.Facing.Y, v.Facing.X);
        for (int i = 0; i < AdventureTuning.VentShells; i++)
            Shells.Add(new PlaneShell { Position = v.Position, Velocity = SafeNormalize(v.Facing + across * _drops.Range(-0.9f, 0.9f)) * _drops.Range(3f, 6f) });
        // A boss level's way down waits for its boss too (PlaneBoss's Release opens it once both are done).
        if (VentsLeft > 0 || ExitOpen || Boss is { Freed: false }) return;
        ExitOpen = true;
        Events.Add(new PlaneEvent(PlaneEventType.ExitOpened, Map.Exit.Position, Vector2.Zero));
    }

    /// <summary>Verification: this vent (or the first open one) sealed at once.</summary>
    public void DebugSealVent(PlaneVent? which = null)
    {
        if ((which ?? Vents.FirstOrDefault(v => !v.Sealed)) is { } v) SealVent(v);
    }

    /// <summary>Tests: one of her shots striking this vent's mouth.</summary>
    public void DebugStrikeVent(PlaneVent v, float light = 1f) => StrikeVent(v.Wall, light);

    /// <summary>Verification and tests: a dot at a point (false where none may lie or one already does).</summary>
    public bool DebugDot(Vector2 at) => Dots?.Add(InkDots.CellOf(at)) ?? false;

    // ───────────────────────── the ink ─────────────────────────

    /// <summary>Every whole dot beside an empty cell divides into it, each on its own beat once a spread period.</summary>
    void StepSpread(InkDots dots)
    {
        // Only a patch joined to a working vent spreads (looked at four times a second).
        if (Tick % 15 == 1) dots.Feed(VentMouths(dots));
        int period = SpreadPeriod;
        int count = dots.Live.Count;
        Span<int> free = stackalloc int[4];
        for (int k = 0; k < count; k++)
        {
            int i = dots.Live[k];
            if ((i * 7919L + Tick) % period != 0 || dots.Ink[i] < 1f || !dots.Fed[i]) continue;
            int n = 0;
            for (int s = 0; s < 4; s++)
            {
                int j = dots.Neighbour(i, s);
                if (j >= 0 && dots.Ink[j] <= 0f && !_rocks.Any(r => r.Contains(InkDots.Centre(j)))) free[n++] = j;
            }
            if (n > 0) dots.Add(free[_inkRng.Int(n)]);
        }
    }

    /// <summary>Dots cut off from every open vent decay: each pops on its own once it has been adrift long enough.</summary>
    void StepDecay(InkDots dots)
    {
        float after = AdventureTuning.DecayAfter(Menace);
        // Backwards: a removed dot's place is taken by the last, already seen.
        for (int k = dots.Live.Count - 1; k >= 0; k--)
        {
            int i = dots.Live[k];
            if (dots.Fed[i])
            {
                dots.Adrift[i] = 0f;
                continue;
            }
            dots.Adrift[i] += Dt;
            if (dots.Adrift[i] < after + AdventureTuning.DecayJitter * InkDots.DecayShare(i)) continue;
            dots.Remove(i);
            Events.Add(new PlaneEvent(PlaneEventType.DotDecayed, InkDots.Centre(i), Vector2.Zero));
        }
    }

    /// <summary>The dots at the open vents' mouths (within VentFeed of each): where the fed ink starts.</summary>
    IEnumerable<int> VentMouths(InkDots dots)
    {
        foreach (var v in Vents)
        {
            if (v.Sealed) continue;
            InkDots.Span(v.Position, AdventureTuning.VentFeed, out int x0, out int y0, out int x1, out int y1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = y * InkDots.N + x;
                if (dots.Ink[i] > 0f && Vector2.Distance(InkDots.Centre(i), v.Position) <= AdventureTuning.VentFeed) yield return i;
            }
        }
    }

    /// <summary>Corrupted creatures leave a thin trail of dots where they swim.</summary>
    void StepTrails(InkDots dots)
    {
        foreach (var m in Murklings)
        {
            if (!m.Alive || m.Clinging || m.Budding) continue;
            m.Trail += m.Velocity.Length() * Dt;
            if (m.Trail < AdventureTuning.TrailEvery) continue;
            m.Trail = 0f;
            dots.Add(InkDots.CellOf(m.Position));
        }
    }

    /// <summary>The spread's beat (ticks): a fed dot divides when (cell × 7919 + tick) is a multiple of it (the view winds it up before).</summary>
    public int SpreadPeriod => Math.Max(1, (int)MathF.Round(AdventureTuning.SpreadEvery / (1f + AdventureTuning.SpreadPerMenace * Menace) * TickRate));

    /// <summary>Ticks until this dot's next beat (0: this tick).</summary>
    public int TicksToSpread(int cell)
    {
        int period = SpreadPeriod;
        return (int)((period - (cell * 7919L + Tick) % period) % period);
    }

    /// <summary>A shot of her light flew from a to b: every dot it passed through is cleared.</summary>
    void ClearAlong(Vector2 a, Vector2 b, float radius)
    {
        var dots = Dots!;
        float reach = radius + AdventureTuning.DotRadius;
        InkDots.Span((a + b) * 0.5f, Vector2.Distance(a, b) * 0.5f + reach, out int x0, out int y0, out int x1, out int y1);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            int i = y * InkDots.N + x;
            if (dots.Ink[i] <= 0f || Geo.SegmentDistance(InkDots.Centre(i), a, b) > reach) continue;
            dots.Remove(i);
            Events.Add(new PlaneEvent(PlaneEventType.DotCleared, InkDots.Centre(i), Vector2.Zero));
        }
    }

    /// <summary>Her light on the dots: where she swims, where her shots pop (from the war's turn), a Starburst's burst.</summary>
    void LightDots(Vector2 at, float radius, float strength)
    {
        if (Dots is not { } dots) return;
        _cleared.Clear();
        dots.Light(at, radius, strength, Tick, _cleared);
        foreach (var c in _cleared) Events.Add(new PlaneEvent(PlaneEventType.DotCleared, c, Vector2.Zero));
    }

    // ───────────────────────── fish and anemones ─────────────────────────

    /// <summary>
    /// Healthy fish eat the dots they touch and fill over ink (draining over clean water); full, a fish turns (a school
    /// all together). A pufferfish fills the same way; full, it is an Inkwarden. (Anemones are only the reef's.)
    /// </summary>
    void StepGauges(InkDots dots)
    {
        foreach (var f in ReefFish)
        {
            if (f.Corrupted || f.Turning > 0f) continue;
            float r = f.Kind switch { ReefFishKind.Chromis => ReefFishTuning.ChromisRadius, ReefFishKind.Clownfish => ReefFishTuning.ClownRadius, _ => ReefFishTuning.TangRadius };
            int near = dots.Nearest(f.Position, AdventureTuning.FishSense);
            if (near >= 0 && Vector2.Distance(InkDots.Centre(near), f.Position) < r + AdventureTuning.DotRadius + 0.1f)
            {
                // Eaten: the dot is gone, its ink in the fish (a freed fish eats it harmlessly).
                dots.Remove(near);
                Events.Add(new PlaneEvent(PlaneEventType.DotEaten, InkDots.Centre(near), Vector2.Zero));
                if (!f.Freed) f.Ink += AdventureTuning.FishEat;
            }
            if (f.Freed) continue;
            if (f.Ink < 1f) f.Ink = Math.Clamp(f.Ink + (near >= 0 ? AdventureTuning.FishFill : -AdventureTuning.FishDrain) * Dt, 0f, 1f);
            if (f.Ink < 1f) continue;
            if (f.Kind == ReefFishKind.Chromis)
            {
                foreach (var o in ReefFish)
                    if (o.Kind == ReefFishKind.Chromis && o.Host == f.Host && o.Huntable) Turn(o);
            }
            else Turn(f);
        }
        // Pufferfish: they eat the dots they touch and fill over ink; full, one is an Inkwarden (the spiky ring).
        foreach (var f in Fish)
        {
            if (f.Freed || f.Gone) continue;
            int near = dots.Nearest(f.Position, AdventureTuning.PufferSense);
            if (near >= 0 && Vector2.Distance(InkDots.Centre(near), f.Position) < PufferlingTuning.CalmRadius + AdventureTuning.DotRadius + 0.1f)
            {
                dots.Remove(near);
                Events.Add(new PlaneEvent(PlaneEventType.DotEaten, InkDots.Centre(near), Vector2.Zero));
                f.Ink += AdventureTuning.FishEat;
            }
            if (f.Ink < 1f) f.Ink = Math.Clamp(f.Ink + (near >= 0 ? AdventureTuning.PufferFill : -AdventureTuning.PufferDrain) * Dt, 0f, 1f);
            if (f.Ink < 1f) continue;
            f.Gone = true;
            var w = NewWarden(f.Position, roams: false);
            w.Puffer = true;
            w.Home = f.Position;
            w.Target = f.Position;
            w.Spin = f.Heading;
            Inkwardens.Add(w);
            Events.Add(new PlaneEvent(PlaneEventType.FishTurned, f.Position, Vector2.Zero, WardenTuning.Radius, DamageSource.None));
        }

        void Turn(PlaneReefFish f)
        {
            f.Ink = 1f;
            f.Turning = MurkFishTuning.TurnSeconds;
        }
    }

    /// <summary>A spiked murkling's throw strikes a fish: its ink fills the gauges of the fish in its splash.</summary>
    void InkFish(MurkShot s, PlaneReefFish struck)
    {
        foreach (var f in ReefFish)
        {
            if (!f.Huntable || (f != struck && Vector2.Distance(f.Position, s.Position) > s.Splash)) continue;
            f.Ink = MathF.Min(1f, f.Ink + AdventureTuning.ThrowFill);
        }
    }

    // ───────────────────────── the three murklings ─────────────────────────

    /// <summary>A fish-born murkling of the adventure, by the fish it was.</summary>
    void StepAdventureMurkling(Murkling m)
    {
        switch (m.Fish!.Kind)
        {
            case ReefFishKind.BlueTang: StepSpiked(m); break;
            case ReefFishKind.Clownfish: StepTadpole(m); break;
            default: StepShoal(m); break;
        }
    }

    Vector2 Wobble(Murkling m, float amount) => new Vector2(MathF.Sin(Time * 2.3f + m.Seed), MathF.Cos(Time * 1.9f + m.Seed * 2f)) * amount;

    /// <summary>
    /// Spiked murkling: keeps KeepMin–KeepMax from her, circling, and throws at her (wound up as a murkling's throw);
    /// never clings. Touching it hurts and knocks her off.
    /// </summary>
    void StepSpiked(Murkling m)
    {
        var p = Player;
        var to = p.Position - m.Position;
        float d = to.Length();
        var dir = SafeNormalize(to);
        bool sees = d < AdventureTuning.SpikedSense;
        Vector2 wish = Wobble(m, 0.3f);
        if (sees && !NavLine(m.Position, p.Position))
            // Rock between: round it toward her, to where it can see her again.
            wish += WayToHer(m.Position);
        else if (sees)
        {
            float keep = d > AdventureTuning.KeepMax ? 1f : d < AdventureTuning.KeepMin ? -1f : 0f;
            float spin = m.Seed % 2f < 1f ? 1f : -1f;
            wish += dir * keep + new Vector2(-dir.Y, dir.X) * spin * 0.7f;
        }
        float pace = AdventureTuning.SpikedSpeed * (m.Windup > 0f ? BlightTuning.WindupSlow : 1f) * (sees ? 1f : 0.4f);
        m.Target = sees ? p.Position : m.Position;
        m.Velocity = MoveToward(m.Velocity, SafeNormalize(wish) * pace, 6f * Dt);
        Move(ref m.Position, ref m.Velocity, m.Body, report: false);

        if (Vector2.Distance(m.Position, p.Position) < m.Radius + Radius)
        {
            if (HurtPlayer(BlightTuning.ContactDamage, dir, DamageSource.Murkling)) p.Velocity += dir * AdventureTuning.SpikedKnock;
            m.Velocity -= dir * AdventureTuning.SpikedKnock * 0.5f;
        }
        m.ThrowAge += Dt;
        if (m.Windup > 0f) StepWindup(m);
        else
        {
            m.ThrowTimer -= Dt;
            if (sees && d < m.ThrowRange && m.ThrowTimer <= 0f && LineOfSight(m.Position, p.Position)) BeginThrow(m, null);
        }
    }

    /// <summary>Tadpole murkling: only chases her, a little faster than she cruises; clings on touch and drains her.</summary>
    void StepTadpole(Murkling m)
    {
        var p = Player;
        float speed = Tuning.PlaneCruiseSpeed * AdventureTuning.TadpoleSpeed;
        float d = Vector2.Distance(m.Position, p.Position);
        bool chase = d < AdventureTuning.TadpoleSense;
        Vector2 wish = (chase ? WayToHer(m.Position) * speed : Vector2.Zero) + Wobble(m, chase ? 0.5f : 0.6f);
        if (wish.Length() > speed) wish = Vector2.Normalize(wish) * speed;
        m.Target = chase ? p.Position : m.Position;
        m.Velocity = MoveToward(m.Velocity, wish, 14f * Dt);
        Move(ref m.Position, ref m.Velocity, m.Radius, report: false);
        if (d < m.Radius + Radius && p.DashInvulnerableTimer <= 0f && p.ShieldTimer <= 0f && LesserClingers < MurkFishTuning.MaxClinging)
        {
            m.Clinging = true;
            m.ClingOffset = SafeNormalize(m.Position - p.Position) * (Radius * 0.85f);
            Events.Add(new PlaneEvent(PlaneEventType.MurklingCling, m.Position, Vector2.Zero));
        }
    }

    long _shoalTick = -1;
    readonly Dictionary<int, List<Murkling>> _shoals = new();

    /// <summary>
    /// Shoal murkling: very fast; near her, its school stands on a ring round her, most of it on her aim's side, so it
    /// takes her shots. Harmless otherwise. Away from her, the school keeps together.
    /// </summary>
    void StepShoal(Murkling m)
    {
        var p = Player;
        if (_shoalTick != Tick)
        {
            _shoalTick = Tick;
            foreach (var list in _shoals.Values) list.Clear();
            foreach (var o in Murklings)
            {
                if (!o.Alive || o.Fish is not { Kind: ReefFishKind.Chromis }) continue;
                if (!_shoals.TryGetValue(o.Swarm, out var l)) _shoals[o.Swarm] = l = new List<Murkling>();
                l.Add(o);
            }
        }
        var school = _shoals[m.Swarm];
        Vector2 centre = Vector2.Zero;
        foreach (var o in school) centre += o.Position;
        centre /= school.Count;
        Vector2 wish;
        if (Vector2.Distance(centre, p.Position) < AdventureTuning.ShoalSense)
        {
            // Its place on the ring: evenly round her, the ring circling her (each school its own way, each layer a little
            // faster than the one inside it) — never tied to her aim, so she can time her shots through the gaps.
            int k = school.IndexOf(m), n = school.Count, layer = k % 3;
            float spin = (m.Swarm & 1) == 0 ? 1f : -1f;
            float a = MathF.Tau * k / n + spin * AdventureTuning.ShoalOrbit * (1f + 0.25f * layer) * Time;
            float r = Radius + AdventureTuning.ShoalRing + AdventureTuning.ShoalLayer * layer;
            var slot = p.Position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
            // (Rock between it and her: round the rock to her first.)
            wish = NavLine(m.Position, p.Position) ? (slot - m.Position) * 8f : WayToHer(m.Position) * AdventureTuning.ShoalSpeed;
            m.Target = p.Position;
        }
        else
        {
            Vector2 apart = Vector2.Zero;
            foreach (var o in school)
            {
                if (o == m) continue;
                Vector2 dd = m.Position - o.Position;
                float l = dd.Length();
                if (l < MurkFishTuning.Spacing) apart += (l > 1e-3f ? dd / l : new Vector2(MathF.Cos(m.Seed), MathF.Sin(m.Seed))) * (1f - l / MurkFishTuning.Spacing);
            }
            wish = (centre - m.Position) * 1.2f + apart * 4f + Wobble(m, 0.6f);
            m.Target = centre;
        }
        if (wish.Length() > AdventureTuning.ShoalSpeed) wish = Vector2.Normalize(wish) * AdventureTuning.ShoalSpeed;
        m.Velocity = MoveToward(m.Velocity, wish, 40f * Dt);
        Move(ref m.Position, ref m.Velocity, m.Body, report: false);
    }

    // ───────────────────────── her oral arm ─────────────────────────

    /// <summary>Where she has been lately (newest last) and when: the way her arm trails.</summary>
    readonly List<(Vector2 At, float Time)> _trail = new();
    readonly List<Vector2> _arm = new();

    /// <summary>Her oral arm's length now (it grows with every skill point she spends).</summary>
    public float ArmLength => AdventureTuning.ArmLength(Run.Upgrades);

    /// <summary>Her oral arm as the sim has it: from her, back along the way she swam, as far as it reaches.</summary>
    public IReadOnlyList<Vector2> Arm => _arm;

    /// <summary>Her oral arm trails her; a corrupted creature it brushes takes a little of her light.</summary>
    void StepArm()
    {
        var p = Player;
        if (_trail.Count == 0 || Vector2.Distance(_trail[^1].At, p.Position) > 0.1f) _trail.Add((p.Position, Time));
        while (_trail.Count > 0 && Time - _trail[0].Time > AdventureTuning.ArmMemory) _trail.RemoveAt(0);
        _arm.Clear();
        _arm.Add(p.Position);
        float left = ArmLength;
        for (int i = _trail.Count - 1; i >= 0 && left > 0f; i--)
        {
            var from = _arm[^1];
            float d = Vector2.Distance(from, _trail[i].At);
            if (d < 1e-4f) continue;
            if (d >= left)
            {
                _arm.Add(from + (_trail[i].At - from) * (left / d));
                break;
            }
            _arm.Add(_trail[i].At);
            left -= d;
        }
        if (_arm.Count < 2 || Defeated) return;

        float light = PlaneCombatTuning.ShotDamage * DamageScale * AdventureTuning.ArmShare;
        foreach (var m in Murklings)
        {
            m.ArmCooldown -= Dt;
            if (!m.Alive || m.Budding || m.Clinging || m.ArmCooldown > 0f || ArmDistance(m.Position) > m.Radius + AdventureTuning.ArmWidth) continue;
            m.ArmCooldown = AdventureTuning.ArmEvery;
            float dealt = Ward(m, m.Position, light);
            bool emptied = dealt > 0f && m.Corruption.Hit(dealt);
            Events.Add(new PlaneEvent(emptied ? PlaneEventType.MobDefeated : PlaneEventType.MobHit, m.Position, SafeNormalize(m.Position - p.Position), dealt));
            if (emptied) BurstMurkling(m, ember: true);
        }
        foreach (var w in Inkwardens)
        {
            w.ArmCooldown -= Dt;
            if (!w.Alive || w.ArmCooldown > 0f || ArmDistance(w.Position) > WardenTuning.Radius + AdventureTuning.ArmWidth) continue;
            w.ArmCooldown = AdventureTuning.ArmEvery;
            bool emptied = w.Corruption.Hit(light);
            Events.Add(new PlaneEvent(emptied ? PlaneEventType.MobDefeated : PlaneEventType.MobHit, w.Position, SafeNormalize(w.Position - p.Position), light));
            if (emptied) FreeWarden(w);
        }
        foreach (var f in Flounders)
        {
            if (!f.Corrupted || f.ArmCooldown > 0f || ArmDistance(f.Position) > FlounderTuning.Radius + AdventureTuning.ArmWidth) continue;
            f.ArmCooldown = AdventureTuning.ArmEvery;
            HitFlounder(f, Ward(f, f.Position, light), SafeNormalize(f.Position - p.Position));
        }
    }

    /// <summary>How far a point is from her oral arm.</summary>
    float ArmDistance(Vector2 at)
    {
        float best = float.MaxValue;
        for (int i = 1; i < _arm.Count; i++) best = MathF.Min(best, Geo.SegmentDistance(at, _arm[i - 1], _arm[i]));
        return best;
    }

    /// <summary>Tests: her trail laid out behind her (oldest first), as if she had just swum it.</summary>
    public void DebugTrail(IEnumerable<Vector2> path)
    {
        _trail.Clear();
        foreach (var at in path) _trail.Add((at, Time));
    }
}
