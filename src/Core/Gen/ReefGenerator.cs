using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using OctoShoots.Core.Loot;
using OctoShoots.Core.Run;
using OctoShoots.Core.Terrain;
using static OctoShoots.Core.Terrain.SdfMath;

namespace OctoShoots.Core.Gen;

/// <summary>
/// Builds a reef (DESIGN-3D §6): a big open-sea basin ringed by cliffs, with a rolling seabed that
/// deepens from the sunlit start toward the boss reef, reef formations (some holding caves),
/// arches and pillars as landmarks, sealed pockets and buried coins, and the loot plan.
/// Baked into a sparse 0.5 m voxel SDF (clamped to ±4 m so open water and deep rock stay uniform)
/// and validated with flood fills. Failed layouts retry with the next seeded attempt.
/// </summary>
public static partial class ReefGenerator
{
    public const float Cell = 0.5f;
    public const float Shell = 2f;
    public const float Clearance = 0.45f;
    public const float ArenaRadius = 17.5f;
    public const float FieldClamp = 4f;
    const int MaxAttempts = 8;

    sealed class Shape
    {
        public required Vector3 Min;
        public required Vector3 Max;
        public required Func<Vector3, float> Sdf;
    }

    /// <summary>Everything placed on the seabed so far, to keep things apart.</summary>
    sealed class Placer(Rng rng, float width, float length)
    {
        readonly List<(Vector2 At, float R)> _taken = new();

        public void Reserve(Vector2 at, float r) => _taken.Add((at, r));

        /// <summary>A copy that sees everything placed so far; what it places does not block this one.</summary>
        public Placer Fork()
        {
            var copy = new Placer(rng, width, length);
            copy._taken.AddRange(_taken);
            return copy;
        }

        public bool TryPlace(float r, float border, float gap, out Vector2 at, int tries = 80, Func<Vector2, bool>? accept = null)
        {
            for (int i = 0; i < tries; i++)
            {
                at = new Vector2(rng.Range(border + r, width - border - r), rng.Range(border + r, length - border - r));
                var p = at;
                if (_taken.All(t => Vector2.Distance(t.At, p) > t.R + r + gap) && (accept?.Invoke(p) ?? true))
                {
                    _taken.Add((at, r));
                    return true;
                }
            }
            at = default;
            return false;
        }
    }

    /// <param name="log">Optional progress lines (timings, rejected attempts).</param>
    public static ReefLayout Generate(RunStreams streams, ReefSpec spec, Action<string>? log = null)
    {
        var reasons = new List<string>();
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var layout = Build(streams.Layout(spec.Depth, spec.Reef, attempt), spec, attempt + 1);
            long built = watch.ElapsedMilliseconds;
            string? problem = Validate(layout);
            log?.Invoke($"{spec} attempt {attempt + 1}: built in {built} ms ({layout.Cave.Sdf.AllocatedChunks} surface chunks), validated in {watch.ElapsedMilliseconds - built} ms{(problem is null ? "" : " — rejected: " + problem)}");
            if (problem is null) return layout;
            reasons.Add(problem);
        }
        throw new InvalidOperationException($"No valid reef for {spec} after {MaxAttempts} attempts: {string.Join("; ", reasons)}");
    }

    // ───────────────────────── layout ─────────────────────────

    static Vector3 Polar(Rng rng, float minR, float maxR)
    {
        float a = rng.Range(0f, MathF.Tau);
        float r = rng.Range(minR, maxR);
        return new Vector3(MathF.Cos(a) * r, 0f, MathF.Sin(a) * r);
    }

    static ReefLayout Build(Rng rng, ReefSpec spec, int attempt)
    {
        float W = spec.Width, D = spec.Width, H = spec.Height;
        float surface = H - 1.5f;
        uint seed = (uint)(rng.NextU64() >> 32);

        // Start in a sunlit corner; the boss reef stands in the opposite one, where the seabed is deepest.
        int corner = rng.Int(4);
        Vector2 Corner(int k, float inset) => new((k & 1) == 0 ? inset : W - inset, (k & 2) == 0 ? inset : D - inset);
        Vector2 start2 = Corner(corner, 40f), boss2 = Corner(3 - corner, 50f);
        var heights = BuildHeights(W, D, start2, boss2, seed);
        float Floor(float x, float z) => SampleHeights(heights, (int)W, (int)D, x, z);

        var rock = new List<Shape>();
        var carve = new List<Shape>();
        var late = new List<Shape>();
        var plugs = new List<Shape>();
        var formations = new List<Formation>();
        var chambers = new List<Chamber>();
        var loot = new LootPlan { Depth = spec.Depth, Reef = spec.Reef };
        var placer = new Placer(rng, W, D);
        placer.Reserve(start2, 26f);

        // ── boss reef ──
        const float bossFormationR = 26f;
        placer.Reserve(boss2, bossFormationR + 6f);
        var boss = new Chamber { Index = 0, Role = ChamberRole.Boss, Radii = new Vector3(ArenaRadius) };
        {
            float h = Floor(boss2.X, boss2.Y);
            boss.FloorY = h + 2f;
            boss.Center = new Vector3(boss2.X, boss.FloorY + ArenaRadius * 0.55f, boss2.Y);
            float top = MathF.Min(boss.Center.Y + ArenaRadius + 6f, surface - 3f);
            var lobes = new List<(Vector3, Vector3)>
            {
                (new Vector3(boss2.X, (h + top) / 2f, boss2.Y), new Vector3(bossFormationR, (top - h) / 2f + 2f, bossFormationR)),
                (new Vector3(boss2.X, h, boss2.Y), new Vector3(bossFormationR + 4f, 5f, bossFormationR + 4f)),
            };
            for (int i = 0; i < 3; i++)
                lobes.Add((new Vector3(boss2.X, h + rng.Range(6f, 16f), boss2.Y) + Polar(rng, 14f, 20f), new Vector3(rng.Range(9f, 13f), rng.Range(8f, 14f), rng.Range(9f, 13f))));
            AddFormation(formations, rock, new Vector3(boss2.X, h, boss2.Y), bossFormationR, lobes);

            Vector3 centre = boss.Center;
            float floor = boss.FloorY;
            carve.Add(Box(centre, new Vector3(ArenaRadius), p => MathF.Max(Sphere(p, centre, ArenaRadius), floor - p.Y)));

            var dir = Vector3.Normalize(new Vector3(start2.X - boss2.X, 0f, start2.Y - boss2.Y));
            AddTunnel(rng, boss, new Vector3(centre.X, floor + 2.2f, centre.Z), dir, bossFormationR + 7f, Floor, carve);
            SetBounds(boss, formations[^1], H);
        }
        var side = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, boss.Mouth - boss.Center));
        var crackA = new Vector3(boss.Center.X, boss.FloorY - 0.2f, boss.Center.Z) - side * 7f;
        var crackB = new Vector3(boss.Center.X, boss.FloorY - 0.2f, boss.Center.Z) + side * 7f;
        carve.Add(new Shape { Min = Vector3.Min(crackA, crackB) - new Vector3(1f), Max = Vector3.Max(crackA, crackB) + new Vector3(1f), Sdf = p => Capsule(p, crackA, crackB, 0.9f) });
        chambers.Add(boss);

        // ── cave formations ──
        var roles = new List<ChamberRole> { ChamberRole.Treasure, ChamberRole.Shop, ChamberRole.Curse, ChamberRole.Secret, ChamberRole.Normal, ChamberRole.Normal };
        if (spec.Depth >= 3) roles.Add(ChamberRole.Normal);
        foreach (var role in roles)
        {
            float caveR = rng.Range(7f, 9f), caveRy = rng.Range(4f, 5f);
            float formationR = caveR + 5f;
            // Caves matter: try hard, letting the gap shrink before giving up.
            Vector2 at = default;
            if (!new[] { 10f, 6f, 3f }.Any(gap => placer.TryPlace(formationR + 2f, 28f, gap, out at, 200))) continue;
            float h = Floor(at.X, at.Y);
            var cave = new Chamber { Index = chambers.Count, Role = role, Radii = new Vector3(caveR, caveRy, caveR), FloorY = h + 2f };
            cave.Center = new Vector3(at.X, cave.FloorY + caveRy * 0.65f, at.Y);

            float top = MathF.Min(cave.Center.Y + caveRy + rng.Range(5f, 12f), surface - 4f);
            var lobes = new List<(Vector3, Vector3)>
            {
                (new Vector3(at.X, (h + top) / 2f, at.Y), new Vector3(formationR, (top - h) / 2f + 1f, formationR)),
                (new Vector3(at.X, h, at.Y), new Vector3(formationR + 2f, 4f, formationR + 2f)),
            };
            int extra = 1 + rng.Int(3);
            for (int i = 0; i < extra; i++)
                lobes.Add((new Vector3(at.X, h + rng.Range(3f, 10f), at.Y) + Polar(rng, 5f, formationR * 0.7f), new Vector3(rng.Range(5f, 8f), rng.Range(5f, 10f), rng.Range(5f, 8f))));
            AddFormation(formations, rock, new Vector3(at.X, h, at.Y), formationR, lobes);

            Vector3 c = cave.Center, r = cave.Radii;
            float floorY = cave.FloorY;
            carve.Add(Box(c, r + new Vector3(1f), p => MathF.Max(Ellipsoid(p, c, r), floorY - p.Y)));
            float a = rng.Range(0f, MathF.Tau);
            AddTunnel(rng, cave, new Vector3(c.X, floorY + 1.8f, c.Z), new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)), formationR + 6f, Floor, carve);
            if (role == ChamberRole.Secret)
            {
                cave.Sealed = true;
                Vector3 mid = cave.TunnelMid;
                Vector3 n = Vector3.Normalize(cave.Tunnel[cave.Tunnel.Length / 2 + 1] - cave.Tunnel[cave.Tunnel.Length / 2 - 1]);
                float plugR = cave.TunnelRadius + 1.6f;
                plugs.Add(Box(mid, new Vector3(plugR), p => Plug(p, mid, n, 1.6f, plugR)));
            }
            SetBounds(cave, formations[^1], H);
            chambers.Add(cave);

            Vector3 floorAt = new(c.X, floorY + 0.5f, c.Z);
            switch (role)
            {
                case ChamberRole.Treasure: loot.Shells.Add(new ShellSpot(floorAt, "treasure", false)); break;
                case ChamberRole.Curse: loot.Shells.Add(new ShellSpot(floorAt, "curse", false)); break;
                case ChamberRole.Secret: loot.Shells.Add(new ShellSpot(floorAt, "secret", false)); break;
                case ChamberRole.Shop:
                    var across = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, cave.Mouth - c));
                    for (int i = -1; i <= 1; i++) loot.Shells.Add(new ShellSpot(floorAt + across * (2.6f * i), "shop", true));
                    break;
                case ChamberRole.Normal: loot.Chests.Add(new ChestSpot(floorAt + new Vector3(2f, 0f, 0f))); break;
            }
        }

        // ── plain formations, arches and pillars ──
        int plain = 5 + spec.Depth;
        for (int i = 0; i < plain; i++)
        {
            float fr = rng.Range(7f, 14f);
            if (!placer.TryPlace(fr, 26f, 8f, out var at)) continue;
            float h = Floor(at.X, at.Y);
            float top = MathF.Min(h + rng.Range(8f, 28f), surface - 4f);
            var lobes = new List<(Vector3, Vector3)> { (new Vector3(at.X, (h + top) / 2f, at.Y), new Vector3(fr, (top - h) / 2f + 1f, fr * rng.Range(0.7f, 1.1f))) };
            int extra = 1 + rng.Int(3);
            for (int k = 0; k < extra; k++)
                lobes.Add((new Vector3(at.X, h + rng.Range(2f, (top - h) * 0.8f), at.Y) + Polar(rng, 3f, fr * 0.8f), new Vector3(rng.Range(3f, 7f), rng.Range(3f, 8f), rng.Range(3f, 7f))));
            AddFormation(formations, rock, new Vector3(at.X, h, at.Y), fr, lobes);
        }
        int arches = 3 + rng.Int(3);
        for (int i = 0; i < arches; i++)
        {
            float major = rng.Range(4f, 6.5f), minor = rng.Range(1f, 1.4f);
            if (!placer.TryPlace(major + minor, 26f, 4f, out var at)) continue;
            var c = new Vector3(at.X, Floor(at.X, at.Y), at.Y);
            float yaw = rng.Range(0f, MathF.PI);
            rock.Add(Box(c, new Vector3(major + minor), p => Arch(p, c, yaw, major, minor)));
        }
        int pillars = 6 + rng.Int(5);
        for (int i = 0; i < pillars; i++)
        {
            float r = rng.Range(1f, 2.2f);
            if (!placer.TryPlace(r + 1f, 26f, 4f, out var at)) continue;
            float h = Floor(at.X, at.Y);
            var a = new Vector3(at.X, h - 2f, at.Y);
            var b = new Vector3(at.X + rng.Range(-2f, 2f), MathF.Min(h + rng.Range(10f, 30f), surface - 4f), at.Y + rng.Range(-2f, 2f));
            rock.Add(new Shape { Min = Vector3.Min(a, b) - new Vector3(r), Max = Vector3.Max(a, b) + new Vector3(r), Sdf = p => Capsule(p, a, b, r) });
        }

        // ── secrets under the seabed (2D §26) ──
        var pockets = new List<SealedPocket>();
        int pocketCount = 4 + rng.Int(3) + (spec.Depth - 1) / 2;
        const float pocketR = 1.6f;
        for (int i = 0; i < pocketCount; i++)
        {
            if (!placer.TryPlace(4f, 30f, 3f, out var at)) continue;
            if (Floor(at.X, at.Y) < 8f) continue; // keep clear of the bottom shell
            var c = new Vector3(at.X, Floor(at.X, at.Y) - 1.4f - pocketR, at.Y);
            pockets.Add(new SealedPocket(c, pocketR));
            late.Add(Box(c, new Vector3(pocketR), p => Sphere(p, c, pocketR)));
            var bottom = c - new Vector3(0f, pocketR - 0.5f, 0f);
            if (i == 0) loot.Shells.Add(new ShellSpot(bottom, "treasure", false));
            else if (rng.NextFloat() < 0.5f) loot.Chests.Add(new ChestSpot(bottom));
            else for (int k = 0; k < 3 + rng.Int(3); k++) loot.Pickups.Add(new PickupSpot(bottom + Polar(rng, 0f, 0.6f), PickupKind.Coin));
        }
        int buried = 8 + rng.Int(5);
        for (int i = 0; i < buried; i++)
        {
            if (!placer.TryPlace(1.5f, 30f, 1f, out var at)) continue;
            loot.Buried.Add(new BuriedCoins(new Vector3(at.X, Floor(at.X, at.Y) - 0.4f, at.Y), rng.NextFloat() < 0.1f ? 5 : 2 + rng.Int(2)));
        }

        // ── loot on the seabed ──
        int chests = 6 + spec.Depth;
        for (int i = 0; i < chests; i++)
            if (placer.TryPlace(2.5f, 30f, 2f, out var at)) loot.Chests.Add(new ChestSpot(new Vector3(at.X, Floor(at.X, at.Y) + 0.45f, at.Y)));
        int loose = 12 + rng.Int(7);
        for (int i = 0; i < loose; i++)
            if (placer.TryPlace(1.2f, 30f, 1f, out var at)) loot.Pickups.Add(new PickupSpot(new Vector3(at.X, Floor(at.X, at.Y) + 0.3f, at.Y), LootRules.RollLoose(rng)));

        // ── boulders and patch reefs on the open seabed ──
        // They keep clear of the loot and caves but don't crowd out the anemones placed later (those may sit on top).
        var rocks = placer.Fork();
        int boulders = 45 + 8 * spec.Depth;
        for (int i = 0; i < boulders; i++)
        {
            float r = rng.Range(0.7f, 2.4f);
            if (!rocks.TryPlace(r + 0.5f, 28f, 1.5f, out var at, 40)) continue;
            var c = new Vector3(at.X, Floor(at.X, at.Y) + r * rng.Range(0.1f, 0.45f), at.Y);
            uint bs = seed + 300u + (uint)i;
            var squash = new Vector3(1f, rng.Range(0.55f, 0.85f), rng.Range(0.8f, 1.15f));
            rock.Add(Box(c, new Vector3(r * 1.4f), p => Ellipsoid(p, c, squash * r) + 0.3f * r * Noise((p - c) * (1.4f / r), bs)));
        }
        // Patch reefs ("bommies"): knobbly coral heads a few metres tall, where the reef life crowds.
        int bommies = 10 + 2 * spec.Depth;
        for (int i = 0; i < bommies; i++)
        {
            float r = rng.Range(2.2f, 3.6f);
            if (!rocks.TryPlace(r + 1.5f, 30f, 3f, out var at, 60)) continue;
            float h = Floor(at.X, at.Y);
            float top = MathF.Min(h + rng.Range(3f, 6.5f), surface - 4f);
            var lumps = new List<(Vector3 C, float R)> { (new Vector3(at.X, h + (top - h) * 0.4f, at.Y), r) };
            int knobs = 3 + rng.Int(3);
            for (int k = 0; k < knobs; k++)
            {
                float a = rng.Range(0f, MathF.Tau);
                lumps.Add((new Vector3(at.X + MathF.Cos(a) * r * 0.6f, rng.Range(h + 1f, top), at.Y + MathF.Sin(a) * r * 0.6f), rng.Range(0.9f, 1.8f)));
            }
            uint bs = seed + 500u + (uint)i;
            var centre = new Vector3(at.X, (h + top) * 0.5f, at.Y);
            rock.Add(Box(centre, new Vector3(r * 1.8f, (top - h) * 0.5f + 2f, r * 1.8f), p =>
            {
                float d = float.MaxValue;
                foreach (var (lc, lr) in lumps) d = SMin(d, Sphere(p, lc, lr), 1.2f);
                return d + 0.35f * Noise(p * 0.9f, bs);
            }));
        }

        // ── bake ──
        var sdf = Bake(W, D, H, heights, rock, carve, late, plugs, seed);
        var startPos = new Vector3(start2.X, MathF.Min(Floor(start2.X, start2.Y) + 10f, surface - 4f), start2.Y);
        var enemySpawns = PlaceSpawns(rng, sdf, chambers, W, D, surface, Floor, spec.Depth);
        var anemones = PlaceAnemones(rng, placer, sdf, Floor, start2, spec);
        var dens = PlaceDens(rng, sdf, Floor, start2, boss2, formations, chambers, W, D, surface, spec);
        var cleanLoot = new LootPlan
        {
            Depth = loot.Depth,
            Reef = loot.Reef,
            Shells = loot.Shells.Select(s => s with { Position = Lift(sdf, s.Position) }).ToList(),
            Chests = loot.Chests.Select(s => s with { Position = Lift(sdf, s.Position) }).ToList(),
            Pickups = loot.Pickups.Select(s => s with { Position = Lift(sdf, s.Position) }).ToList(),
            Buried = loot.Buried,
        };

        var terrain = new Cave
        {
            Sdf = sdf,
            ShellCells = (int)(Shell / Cell),
            SurfaceY = surface,
            PlayerSpawn = startPos,
            EnemySpawns = enemySpawns,
            Loot = cleanLoot,
            Anemones = anemones,
            Dens = dens,
        };
        return new ReefLayout
        {
            Spec = spec,
            Size = new Vector3(W, H, D),
            SurfaceY = surface,
            StartPosition = startPos,
            Formations = formations,
            Chambers = chambers,
            Pockets = pockets,
            CrackA = crackA,
            CrackB = crackB,
            ArenaRadius = ArenaRadius,
            Cave = terrain,
            Heights = heights,
            Attempts = attempt,
        };
    }

    static Shape Box(Vector3 centre, Vector3 halfSize, Func<Vector3, float> sdf) =>
        new() { Min = centre - halfSize, Max = centre + halfSize, Sdf = sdf };

    static void AddFormation(List<Formation> formations, List<Shape> rock, Vector3 baseCentre, float radius, List<(Vector3 C, Vector3 R)> lobes)
    {
        var array = lobes.ToArray();
        Vector3 min = array.Select(l => l.C - l.R).Aggregate(Vector3.Min);
        Vector3 max = array.Select(l => l.C + l.R).Aggregate(Vector3.Max);
        rock.Add(new Shape
        {
            Min = min,
            Max = max,
            Sdf = p =>
            {
                float d = Ellipsoid(p, array[0].C, array[0].R);
                for (int i = 1; i < array.Length; i++) d = SMin(d, Ellipsoid(p, array[i].C, array[i].R), 3f);
                return d;
            },
        });
        formations.Add(new Formation(baseCentre, radius, array));
    }

    /// <summary>A wandering tube from inside a cave out into open water, opening above the seabed.</summary>
    static void AddTunnel(Rng rng, Chamber cave, Vector3 from, Vector3 dir, float length, Func<float, float, float> floor, List<Shape> carve)
    {
        Vector3 mouth = from + dir * length;
        mouth.Y = MathF.Max(from.Y, floor(mouth.X, mouth.Z) + 2.5f);
        Vector3 beyond = mouth + dir * 3f;
        Vector3 side = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, dir));
        Vector3 Jitter() => side * rng.Range(-2.5f, 2.5f) + Vector3.UnitY * rng.Range(-0.8f, 0.8f);
        var control = new[] { from, from, Vector3.Lerp(from, mouth, 0.35f) + Jitter(), Vector3.Lerp(from, mouth, 0.7f) + Jitter(), mouth, beyond, beyond };
        var path = new List<Vector3>();
        for (int span = 1; span < control.Length - 2; span++)
        for (int i = 0; i < 4; i++)
            path.Add(CatmullRom(control[span - 1], control[span], control[span + 1], control[span + 2], i / 4f));
        path.Add(beyond);

        cave.Mouth = mouth;
        cave.Tunnel = path.ToArray();
        cave.TunnelRadius = rng.Range(1.8f, 2.1f);
        float r = cave.TunnelRadius;
        for (int i = 0; i + 1 < path.Count; i++)
        {
            Vector3 p0 = path[i], p1 = path[i + 1];
            carve.Add(new Shape { Min = Vector3.Min(p0, p1) - new Vector3(r), Max = Vector3.Max(p0, p1) + new Vector3(r), Sdf = p => Capsule(p, p0, p1, r) });
        }
    }

    static void SetBounds(Chamber cave, Formation formation, float height)
    {
        Vector3 min = formation.Lobes.Select(l => l.C - l.R).Aggregate(Vector3.Min) - new Vector3(6f);
        Vector3 max = formation.Lobes.Select(l => l.C + l.R).Aggregate(Vector3.Max) + new Vector3(6f);
        min = Vector3.Min(min, cave.Mouth - new Vector3(6f));
        max = Vector3.Max(max, cave.Mouth + new Vector3(6f));
        cave.BoundsMin = Vector3.Max(min, Vector3.Zero);
        cave.BoundsMax = Vector3.Min(max, new Vector3(float.MaxValue, height, float.MaxValue));
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
    }

    // ───────────────────────── seabed ─────────────────────────

    /// <summary>
    /// Seabed heights on a 1 m grid: shallow (18 m) near the start, deep (10 m) toward the boss reef,
    /// rolling dunes, a trench before the boss, and cliffs rising out of the water around the rim.
    /// </summary>
    static float[] BuildHeights(float W, float D, Vector2 start, Vector2 boss, uint seed)
    {
        int w = (int)W, d = (int)D;
        var heights = new float[(w + 1) * (d + 1)];
        Vector2 axis = boss - start;
        float axisLen2 = axis.LengthSquared();
        Parallel.For(0, d + 1, z =>
        {
            for (int x = 0; x <= w; x++)
            {
                var p = new Vector2(x, z);
                float t = Math.Clamp(Vector2.Dot(p - start, axis) / axisLen2, 0f, 1f);
                float h = 18f - 8f * t;
                h += 4f * Noise(new Vector3(x * 0.02f, 0f, z * 0.02f), seed + 101u) + 2f * Noise(new Vector3(x * 0.045f, 3f, z * 0.045f), seed + 103u);
                h += 1.2f * Noise(new Vector3(x * 0.11f, 7f, z * 0.11f), seed + 107u);
                // Spur-and-groove: long sand channels between coral spurs, running down the slope (the fore-reef).
                float along = Vector2.Dot(p - start, axis) / MathF.Sqrt(axisLen2);
                float across = (p.X - start.X) * -axis.Y / MathF.Sqrt(axisLen2) + (p.Y - start.Y) * axis.X / MathF.Sqrt(axisLen2);
                float wobble = 3f * Noise(new Vector3(along * 0.03f, 11f, across * 0.03f), seed + 109u);
                float spur = 1f - MathF.Abs(Noise(new Vector3((across + wobble) * 0.09f, 13f, along * 0.012f), seed + 113u));
                h += 1.8f * (spur * spur - 0.5f) * MathUtil.Smoothstep((t - 0.15f) / 0.25f);
                // Low sand waves.
                h += 0.35f * MathF.Sin(along * 0.45f + 2f * Noise(new Vector3(x * 0.05f, 17f, z * 0.05f), seed + 127u));
                float toBoss = Vector2.Distance(p, boss);
                h -= 4f * MathF.Exp(-(toBoss - 34f) * (toBoss - 34f) / 200f);
                float edge = MathF.Min(MathF.Min(x, W - x), MathF.Min(z, D - z));
                float rim = 1f - MathUtil.Smoothstep((edge - 4f) / 22f);
                h += 70f * rim * rim;
                heights[z * (w + 1) + x] = MathF.Max(h, 7f);
            }
        });
        return heights;
    }

    public static float SampleHeights(float[] heights, int w, int d, float x, float z)
    {
        x = Math.Clamp(x, 0f, w - 0.001f);
        z = Math.Clamp(z, 0f, d - 0.001f);
        int ix = (int)x, iz = (int)z;
        float tx = x - ix, tz = z - iz;
        int row = w + 1;
        float a = MathUtil.Lerp(heights[iz * row + ix], heights[iz * row + ix + 1], tx);
        float b = MathUtil.Lerp(heights[(iz + 1) * row + ix], heights[(iz + 1) * row + ix + 1], tx);
        return MathUtil.Lerp(a, b, tz);
    }

    // ───────────────────────── baking ─────────────────────────

    static VoxelSdf Bake(float W, float D, float H, float[] heights, List<Shape> rock, List<Shape> carve, List<Shape> late, List<Shape> plugs, uint seed)
    {
        int nx = (int)(W / Cell) + 1, ny = (int)(H / Cell) + 1, nz = (int)(D / Cell) + 1;
        var sdf = new VoxelSdf(nx, ny, nz, Cell, Vector3.Zero, FieldClamp);
        int w = (int)W, d = (int)D;
        const int size = VoxelSdf.ChunkSize;
        const float margin = FieldClamp;

        Parallel.For(0, sdf.ChunksX * sdf.ChunksY * sdf.ChunksZ, chunk =>
        {
            int cx = chunk % sdf.ChunksX, cy = chunk / sdf.ChunksX % sdf.ChunksY, cz = chunk / (sdf.ChunksX * sdf.ChunksY);
            Vector3 lo = new Vector3(cx, cy, cz) * size * Cell;
            Vector3 hi = lo + new Vector3(size - 1) * Cell;
            bool Near(Shape s, float m) => s.Max.X >= lo.X - m && s.Min.X <= hi.X + m && s.Max.Y >= lo.Y - m && s.Min.Y <= hi.Y + m && s.Max.Z >= lo.Z - m && s.Min.Z <= hi.Z + m;

            float hMin = float.MaxValue, hMax = float.MinValue;
            // Seabed range under the chunk and 4 m around it (a cliff next door still counts).
            for (float z = lo.Z - margin; z <= hi.Z + margin + 1f; z += 1f)
            for (float x = lo.X - margin; x <= hi.X + margin + 1f; x += 1f)
            {
                float h = SampleHeights(heights, w, d, x, z);
                hMin = MathF.Min(hMin, h);
                hMax = MathF.Max(hMax, h);
            }

            var r = rock.Where(s => Near(s, margin + 2f)).ToArray();
            var c = carve.Where(s => Near(s, margin)).ToArray();
            var l = late.Where(s => Near(s, margin)).ToArray();
            var pl = plugs.Where(s => Near(s, margin)).ToArray();
            bool nearSide = lo.X < Shell + margin || lo.Z < Shell + margin || hi.X > W - Shell - margin || hi.Z > D - Shell - margin || lo.Y < Shell + margin;

            if (!nearSide && r.Length == 0 && c.Length == 0 && l.Length == 0 && lo.Y > hMax + 6f)
            {
                sdf.SetUniform(cx, cy, cz, FieldClamp);
                return;
            }
            if (!nearSide && c.Length == 0 && l.Length == 0 && hi.Y < hMin - 6f)
            {
                sdf.SetUniform(cx, cy, cz, -FieldClamp);
                return;
            }

            // Seabed height and slope factor per column of the chunk (shared by all 32 points above it).
            var columnH = new float[size * size];
            var columnK = new float[size * size];
            for (int lz = 0; lz < size; lz++)
            for (int lx = 0; lx < size; lx++)
            {
                float x = (cx * size + lx) * Cell, z = (cz * size + lz) * Cell;
                float gx = SampleHeights(heights, w, d, x + 0.5f, z) - SampleHeights(heights, w, d, x - 0.5f, z);
                float gz = SampleHeights(heights, w, d, x, z + 0.5f) - SampleHeights(heights, w, d, x, z - 0.5f);
                columnH[lz * size + lx] = SampleHeights(heights, w, d, x, z);
                columnK[lz * size + lx] = 1f / MathF.Sqrt(1f + gx * gx + gz * gz);
            }

            var data = new float[size * size * size];
            for (int lz = 0; lz < size; lz++)
            for (int ly = 0; ly < size; ly++)
            for (int lx = 0; lx < size; lx++)
            {
                var p = new Vector3(cx * size + lx, cy * size + ly, cz * size + lz) * Cell;

                // Seabed: height difference scaled by the slope, so steep cliffs still give honest distances.
                int column = lz * size + lx;
                float open = (p.Y - columnH[column]) * columnK[column];

                foreach (var s in r)
                    if (BoxDistance(p, s) < open + 2f) open = SMin(open, s.Sdf(p), 2f);
                if (MathF.Abs(open) < 6f)
                {
                    open += 0.8f * Noise(p * 0.17f, seed) + 0.35f * Noise(p * 0.45f, seed + 17u) + 0.15f * Noise(p * 1.1f, seed + 29u);
                    // Rock above the seabed weathers into limestone ledges and pockmarks.
                    float aboveSeabed = p.Y - columnH[column];
                    if (aboveSeabed > 1.5f)
                    {
                        float strata = MathF.Sin(p.Y * 1.4f + 2.5f * Noise(p * 0.08f, seed + 31u));
                        float k = MathUtil.Smoothstep((aboveSeabed - 1.5f) / 3f);
                        open += k * (0.28f * strata - 0.22f * MathF.Max(0f, Noise(p * 0.7f, seed + 37u)));
                    }
                }
                foreach (var s in c)
                    if (BoxDistance(p, s) < margin) open = SMax(open, -s.Sdf(p), 1f);
                foreach (var s in l)
                    if (BoxDistance(p, s) < margin) open = SMax(open, -s.Sdf(p), 0.5f);
                foreach (var s in pl)
                    if (BoxDistance(p, s) < margin) open = SMin(open, s.Sdf(p), 0.5f);

                // Indestructible shell on the sides and bottom; the top is the open sea surface.
                float box = MathF.Min(MathF.Min(MathF.Min(p.X, W - p.X), MathF.Min(p.Z, D - p.Z)), p.Y) - Shell;
                data[(lz * size + ly) * size + lx] = Math.Clamp(MathF.Min(open, box), -FieldClamp, FieldClamp);
            }
            sdf.SetChunk(cx, cy, cz, data);
        });
        return sdf;
    }

    /// <summary>Distance from a point to a shape's bounding box (0 inside); a lower bound of its distance to the shape.</summary>
    static float BoxDistance(Vector3 p, Shape s) => Vector3.Max(Vector3.Max(s.Min - p, p - s.Max), Vector3.Zero).Length();

    /// <summary>Moves a loot spot up until it sits in water (it may have landed in rock noise).</summary>
    static Vector3 Lift(VoxelSdf sdf, Vector3 p)
    {
        for (int i = 0; i < 12 && sdf.Sample(p) < 0.35f; i++) p.Y += 0.4f;
        return p;
    }

    /// <summary>
    /// Anemones rooted on the seabed, found by dropping a ray onto the baked terrain: mostly decoration
    /// (55 + 8 per depth), plus 6 + depth nests (at most 12) at least 50 m from the start, each hosting a
    /// school of 3–5 clownfish and a ninja.
    /// </summary>
    static List<AnemoneSpot> PlaceAnemones(Rng rng, Placer placer, VoxelSdf sdf, Func<float, float, float> floor, Vector2 start, ReefSpec spec)
    {
        var list = new List<AnemoneSpot>();
        int nests = Math.Min(12, 6 + spec.Depth);
        int decor = 55 + 8 * spec.Depth;
        for (int i = 0; i < nests + decor; i++)
        {
            bool nest = i < nests;
            float r = nest ? rng.Range(1.5f, 2.1f) : rng.Range(0.7f, 1.5f);
            Vector3 pos = default, up = default;
            bool Accept(Vector2 at)
            {
                if (nest && Vector2.Distance(at, start) < 50f) return false;
                if (!Snap(sdf, at.X, at.Y, floor(at.X, at.Y), out pos, out up) || up.Y < 0.8f) return false;
                // Open water above it, wide enough for tentacles: no overhang or crevice.
                return sdf.Sample(pos + up * (r * 1.3f)) > r * 0.9f && sdf.Sample(pos + up * (r * 0.6f)) > 0.15f && sdf.Sample(pos + up * 0.4f) > 0.25f;
            }
            // Nests are the point: try much harder for them than for decoration.
            if (!placer.TryPlace(r + 0.5f, 28f, nest ? 4f : 1.5f, out _, nest ? 300 : 60, Accept)) continue;
            list.Add(new AnemoneSpot(pos, up, r, nest, nest ? 3 + rng.Int(3) : 0, nest ? rng.Int(2) : rng.Int(8)));
        }
        return list;
    }

    /// <summary>Finds the seabed under (x, z) by casting a ray down from above it.</summary>
    static bool Snap(VoxelSdf sdf, float x, float z, float floorY, out Vector3 pos, out Vector3 up)
    {
        pos = default;
        up = Vector3.UnitY;
        var from = new Vector3(x, floorY + 3.5f, z);
        if (sdf.Sample(from) < 1f || !sdf.Raycast(from, -Vector3.UnitY, 8f, out float hit)) return false;
        up = sdf.Gradient(from - Vector3.UnitY * hit);
        pos = from - Vector3.UnitY * (hit + 0.08f);
        return true;
    }

    /// <summary>Creature spawns: a wide scatter through open water plus a few in ordinary caves and the curse den.</summary>
    static Vector3[] PlaceSpawns(Rng rng, VoxelSdf sdf, List<Chamber> chambers, float W, float D, float surface, Func<float, float, float> floor, int depth)
    {
        var spawns = new List<Vector3>();
        int open = 50 + 10 * depth;
        for (int i = 0; i < open; i++)
        {
            float x = rng.Range(30f, W - 30f), z = rng.Range(30f, D - 30f);
            float y = MathF.Min(floor(x, z) + rng.Range(3f, 16f), surface - 4f);
            var p = new Vector3(x, y, z);
            if (sdf.Sample(p) >= 2f) spawns.Add(p);
        }
        foreach (var c in chambers)
        {
            int want = c.Role switch { ChamberRole.Normal => 2 + rng.Int(2), ChamberRole.Curse => 3, ChamberRole.Treasure => 1, _ => 0 };
            for (int tries = 0; tries < 30 && c.Spawns.Count < want; tries++)
            {
                Vector3 p = c.Center + Polar(rng, 0f, c.Radii.X * 0.6f);
                p.Y = rng.Range(c.FloorY + 1.5f, c.Center.Y + c.Radii.Y * 0.4f);
                if (sdf.Sample(p) >= 1.5f) c.Spawns.Add(p);
            }
            spawns.AddRange(c.Spawns);
        }
        return spawns.ToArray();
    }

    // ───────────────────────── validation ─────────────────────────

    /// <summary>Null when the reef is playable; otherwise what's wrong (§6.2 "Validation").</summary>
    public static string? Validate(ReefLayout layout)
    {
        var sdf = layout.Cave.Sdf;
        if (sdf.Sample(layout.StartPosition) < 1.5f) return "start is inside rock";
        foreach (var role in new[] { ChamberRole.Boss, ChamberRole.Treasure, ChamberRole.Shop, ChamberRole.Curse, ChamberRole.Secret })
            if (layout.Chambers.All(c => c.Role != role)) return $"no room for the {role} cave";

        // The open sea, checked on a coarse 2 m lattice: every cave mouth must be swimmable from the start.
        var openSea = CoarseFlood(sdf, layout.StartPosition, layout.SurfaceY);
        foreach (var c in layout.Chambers)
        {
            if (sdf.Sample(c.Mouth) < 1f) return $"{c.Role} cave mouth is buried";
            if (!openSea(c.Mouth)) return $"{c.Role} cave mouth is cut off from the open sea";

            // Locally, from the mouth: the cave must be reachable, except the secret cave which must be sealed.
            var reached = Reachability.LocalFlood(sdf, c.Mouth, Clearance, c.BoundsMin, c.BoundsMax);
            bool any = c.Probes().Any(reached);
            if (c.Role == ChamberRole.Secret && any) return "the secret cave is not sealed";
            if (c.Role != ChamberRole.Secret && !any) return $"{c.Role} cave is unreachable from its mouth";
        }

        // The boss arena hangs on its one tunnel: block it and the arena drops out of reach.
        var boss = layout.Boss;
        Vector3 gate = BossGate(layout);
        var blocked = Reachability.LocalFlood(sdf, boss.Mouth, Clearance, boss.BoundsMin, boss.BoundsMax, gate, boss.TunnelRadius + 2.5f);
        if (blocked(boss.Center)) return "the boss arena has a second way in";

        foreach (var a in layout.Cave.Anemones)
        {
            if (sdf.Sample(a.Position + a.Up * (a.Radius * 0.6f)) < 0.1f) return "an anemone is buried in rock";
            if (a.Nest && !openSea(a.Position + a.Up * 1.5f)) return "a nest is cut off from the open sea";
        }

        foreach (var pocket in layout.Pockets)
        {
            if (sdf.Sample(pocket.Center) < 0.5f) return "a sealed pocket has no space inside";
            var above = new Vector3(pocket.Center.X, layout.FloorHeight(pocket.Center.X, pocket.Center.Z) + 1.5f, pocket.Center.Z);
            var near = Reachability.LocalFlood(sdf, above, Clearance, pocket.Center - new Vector3(6f), pocket.Center + new Vector3(6f, 9f, 6f));
            if (near(pocket.Center)) return "a sealed pocket is open";
        }
        return null;
    }

    /// <summary>The point of the arena tunnel just outside the arena wall.</summary>
    public static Vector3 BossGate(ReefLayout layout)
    {
        var boss = layout.Boss;
        return boss.Tunnel
            .Where(q => (q - boss.Center).Length() > layout.ArenaRadius + 1.5f)
            .OrderBy(q => (q - boss.Center).Length())
            .First();
    }

    /// <summary>Flood fill on a 2 m lattice below the surface; returns a reached-test for points.</summary>
    static Func<Vector3, bool> CoarseFlood(VoxelSdf sdf, Vector3 start, float surface)
    {
        const float step = 2f;
        int w = (int)(sdf.Size.X / step) + 1, h = (int)(MathF.Min(sdf.Size.Y, surface) / step) + 1, d = (int)(sdf.Size.Z / step) + 1;
        var visited = new bool[w * h * d];
        int Index(int x, int y, int z) => (z * h + y) * w + x;
        (int, int, int) Node(Vector3 p) => (Math.Clamp((int)MathF.Round(p.X / step), 0, w - 1), Math.Clamp((int)MathF.Round(p.Y / step), 0, h - 1), Math.Clamp((int)MathF.Round(p.Z / step), 0, d - 1));
        bool Open(int x, int y, int z) => sdf.Sample(new Vector3(x, y, z) * step) >= 1f;

        var (sx, sy, sz) = Node(start);
        var queue = new Queue<(int, int, int)>();
        if (Open(sx, sy, sz))
        {
            visited[Index(sx, sy, sz)] = true;
            queue.Enqueue((sx, sy, sz));
        }
        var steps = new[] { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) };
        while (queue.Count > 0)
        {
            var (x, y, z) = queue.Dequeue();
            foreach (var (dx, dy, dz) in steps)
            {
                int nx = x + dx, ny = y + dy, nz = z + dz;
                if (nx < 0 || ny < 0 || nz < 0 || nx >= w || ny >= h || nz >= d) continue;
                int i = Index(nx, ny, nz);
                if (visited[i] || !Open(nx, ny, nz)) continue;
                visited[i] = true;
                queue.Enqueue((nx, ny, nz));
            }
        }

        return p =>
        {
            // Any reached lattice node within one step counts (mouths sit close to rock).
            var (x, y, z) = Node(p);
            for (int dz = -1; dz <= 1; dz++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy, nz = z + dz;
                if (nx < 0 || ny < 0 || nz < 0 || nx >= w || ny >= h || nz >= d) continue;
                if (visited[Index(nx, ny, nz)]) return true;
            }
            return false;
        };
    }
}
