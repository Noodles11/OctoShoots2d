using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;
using Xunit;
using Xunit.Abstractions;

namespace OctoShoots.Core.Tests;

/// <summary>The top-down level generator (DESIGN-TOPDOWN §4.1): determinism and the generation invariants.</summary>
public class TopDownGenTests
{
    static readonly ConcurrentDictionary<(string, LevelId), LevelMap> Cache = new();
    readonly ITestOutputHelper _out;

    public TopDownGenTests(ITestOutputHelper output) => _out = output;

    public static readonly TheoryData<string> Seeds = new() { "KELP7Q2Z", "AAAAAAAA", "REEF2345", "DEEPSEA9", "WAVEDRFT", "CRABSHLL", "TANKHAND", "CRACKRFT" };

    static LevelMap Fresh(string seed, LevelId? id = null, string salt = "") =>
        TopDownGenerator.Generate(new RunStreams(SeedCode.Parse(seed)), id ?? LevelId.FirstRun, cosmeticSalt: salt);

    static LevelMap Map(string seed, LevelId? id = null) => Cache.GetOrAdd((seed, id ?? LevelId.FirstRun), k => Fresh(k.Item1, k.Item2));

    /// <summary>The boss level of the seed's first depth.</summary>
    static LevelId BossLevel(string seed) => new(1, 2, LevelPlan.BossLevel(SeedCode.Parse(seed), 1, 2));

    // ───────────────────────── determinism ─────────────────────────

    [Theory]
    [MemberData(nameof(Seeds))]
    public void TheSameSeedGivesAByteIdenticalLevel(string seed)
    {
        byte[] a = Fresh(seed).ToBytes(), b = Fresh(seed).ToBytes();
        Assert.Equal(a, b);
        _out.WriteLine($"{seed}: {a.Length} bytes");
    }

    [Fact]
    public void DifferentSeedsAndLevelsGiveDifferentLevels()
    {
        Assert.NotEqual(Map("KELP7Q2Z").ToBytes(), Map("AAAAAAAA").ToBytes());
        Assert.NotEqual(Map("KELP7Q2Z").ToBytes(), Map("KELP7Q2Z", new LevelId(1, 2, 2)).ToBytes());
    }

    [Fact]
    public void TheCosmeticStreamNeverTouchesGameplay()
    {
        var plain = Fresh("REEF2345");
        var salted = Fresh("REEF2345", salt: "/other");
        Assert.NotEqual(plain.Decor, salted.Decor);
        plain.Decor.Clear();
        salted.Decor.Clear();
        Assert.Equal(plain.ToBytes(), salted.ToBytes());
    }

    [Fact]
    public void AFailedAttemptRetriesFromTheNextSubSeedDeterministically()
    {
        // A level that needs several attempts: the attempt that succeeds is always the same one.
        var (seed, id) = Seeds.Cast<object[]>().Select(row => (string)row[0])
            .SelectMany(s => Enumerable.Range(1, 3).Select(l => (s, new LevelId(1, 2, l))))
            .First(x => Map(x.s, x.Item2).Attempt > 0);
        var a = Fresh(seed, id);
        var b = Fresh(seed, id);
        Assert.True(a.Attempt > 0);
        Assert.Equal(a.Attempt, b.Attempt);
    }

    // ───────────────────────── the plan ─────────────────────────

    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryDepthHasOneOrTwoTreasureRooms(string seed)
    {
        var code = SeedCode.Parse(seed);
        for (int depth = 1; depth <= LevelPlan.Depths; depth++)
        {
            int boss = LevelPlan.BossLevel(code, 1, depth);
            int second = LevelPlan.SecondTreasureLevel(code, 1, depth);
            Assert.True(second == 0 || (second >= 2 && second <= boss), $"depth {depth}: second treasure on level {second}");
            int total = 0;
            for (int level = 1; level <= boss; level++)
            {
                int t = LevelPlan.For(code, new LevelId(1, depth, level)).Treasures;
                Assert.InRange(t, 0, 1);
                total += t;
            }
            // The general roll's second always comes (on its level, or on a level whose template requires one);
            // with no second rolled, a template that requires a clam room may still bring one.
            if (second == 0) Assert.InRange(total, 1, 2);
            else Assert.Equal(2, total);
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryDepthEndsWithItsBossOnLevelFourOrFive(string seed)
    {
        var code = SeedCode.Parse(seed);
        var id = LevelId.FirstRun;
        for (int step = 0; step < 40; step++)
        {
            var plan = LevelPlan.For(code, id);
            int boss = LevelPlan.BossLevel(code, id.Cycle, id.Depth);
            Assert.InRange(boss, 4, 5);
            Assert.Equal(id.Level == boss, plan.HasBoss);
            Assert.True(plan.Places <= LevelStocking.MaxPlaces, $"{id}: {plan.Places} places");
            Assert.True(plan.Ambushes <= LevelStocking.MaxAmbushes);
            if (id.Level == 1)
            {
                Assert.Equal(1, plan.Treasures);
                Assert.Equal(0, plan.Shops);
                Assert.Equal(0, plan.Caches);
            }
            var next = LevelPlan.NextOf(code, id);
            // After the boss: the next depth (or, after the last, the first again in the next loop), with Menace up.
            if (plan.HasBoss)
            {
                Assert.Equal(1, next.Level);
                Assert.Equal(id.Depth == LevelPlan.Depths ? new LevelId(id.Cycle + 1, LevelPlan.Tutorial + 1, 1) : new LevelId(id.Cycle, id.Depth + 1, 1), next);
                Assert.True(LevelPlan.MenaceOf(next) > plan.Menace);
            }
            else
            {
                Assert.Equal(id with { Level = id.Level + 1 }, next);
                Assert.Equal(plan.Menace, LevelPlan.MenaceOf(next));
            }
            Assert.Equal(id, LevelPlan.PreviousOf(code, next));
            // The hole of this level and the start of the next share one stamp.
            Assert.Equal(plan.ExitStamp, LevelPlan.For(code, next).EntryStamp);
            Assert.Equal(plan.HasBoss, LevelPlan.For(code, next).EntryFromArena);
            id = next;
        }
        Assert.True(id.Cycle >= 2, "40 levels loop past the seventh depth");
    }

    // ───────────────────────── the way down ─────────────────────────

    [Theory]
    [MemberData(nameof(Seeds))]
    public void TheNextLevelStartsInOpenWater(string seed)
    {
        var code = SeedCode.Parse(seed);
        foreach (var id in new[] { LevelId.First, new LevelId(1, LevelPlan.Tutorial, 2), BossLevel(seed) })
        {
            var below = Map(seed, LevelPlan.NextOf(code, id));
            // She lands in the start's chamber: open water all round, and a floor under her (no shaft).
            for (int i = 0; i < 16; i++)
            {
                float a = i * MathF.Tau / 16f;
                Assert.True(below.IsOpen(below.Start.Position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 4f));
            }
            Assert.False(below.Shaft.Contains(below.Start.Position, 20f));
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void OnlyTheWayDownIsDeepWater(string seed)
    {
        foreach (var map in new[] { Map(seed), Map(seed, BossLevel(seed)) })
        {
            Assert.True(map.Shaft.Contains(LevelValidator.DeepestPoint(map), 1f));
            // The Crack's floor is the level below's seabed; an exit chamber's bowl stops just over it.
            Assert.True(map.HeightAt(map.Shaft.Center) <= LevelMap.ShaftBottom + (map.HasBoss ? 0.01f : 1f));
            Assert.True(map.Shaft.Contains(map.Exit.Position));
            // The Crack drops sheer from the arena; an ordinary exit's whole chamber slopes down to its dive zone.
            float room = map.HasBoss ? 1.5f : map.Chambers.OrderBy(c => Vector2.Distance(c.At, map.Exit.Position)).First().Radius + 4.5f;
            for (int y = 0; y <= LevelMap.Size; y++)
            for (int x = 0; x <= LevelMap.Size; x++)
            {
                var p = new Vector2(x, y);
                bool way = map.HasBoss ? map.Shaft.Contains(p, room) : Vector2.Distance(p, map.Exit.Position) < room + map.Shaft.HalfLength;
                if (!way) Assert.True(map[x, y] > -4.5f, $"deep water at ({x}, {y}): {map[x, y]:0.0} m");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void SealedWallsStandTallAndShutTheBossArenaUntilOpened(string seed)
    {
        foreach (var map in new[] { Map(seed), Map(seed, BossLevel(seed)) })
        {
            foreach (var rock in map.WeakRocks)
            {
                // A real wall: it stands well over her plane somewhere across its passage.
                float tallest = 0f;
                for (int y = 0; y <= LevelMap.Size; y++)
                for (int x = 0; x <= LevelMap.Size; x++)
                    if (!float.IsNaN(map.SealedAt(x, y)) && rock.Contains(new Vector2(x, y))) tallest = MathF.Max(tallest, map.SealedAt(x, y));
                Assert.True(tallest > LevelMap.SwimBand + 2f, $"{rock.Kind} wall only {tallest:0.0} m high");
                // Nothing was left inside it.
                Assert.DoesNotContain(map.Spawns, sp => rock.Contains(sp.Position, 0.5f));
            }
            if (!map.HasBoss || map.Rift) continue;
            Assert.Contains(map.WeakRocks, r => r.Kind == SealKind.Boss);
            // With its walls standing, the arena cannot be reached from the start.
            const int N = LevelMap.Samples;
            bool Open(int x, int y) => map[x, y] < 0.2f && !map.WeakRocks.Any(r => r.Contains(new Vector2(x, y)));
            var seen = new bool[N * N];
            var queue = new System.Collections.Generic.Queue<(int X, int Y)>();
            int sx = (int)MathF.Round(map.Start.Position.X), sy = (int)MathF.Round(map.Start.Position.Y);
            queue.Enqueue((sx, sy));
            seen[sy * N + sx] = true;
            int ex = (int)MathF.Round(map.Exit.Position.X), ey = (int)MathF.Round(map.Exit.Position.Y);
            bool reached = false;
            while (queue.Count > 0 && !reached)
            {
                var (x, y) = queue.Dequeue();
                reached = x == ex && y == ey;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= N || ny >= N || seen[ny * N + nx] || !Open(nx, ny)) continue;
                    seen[ny * N + nx] = true;
                    queue.Enqueue((nx, ny));
                }
            }
            Assert.False(reached, "the arena is open past its sealed walls");
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void KelpGrowsInDenseBedsOnTheOpenFloorAwayFromThePlaces(string seed)
    {
        var map = Map(seed);
        Assert.InRange(map.Kelp.Count, 1, TopDownGenerator.KelpDepthMax);
        foreach (var bed in map.Kelp)
        {
            Assert.True(bed.Roots.Count >= TopDownGenerator.KelpLeast);
            Assert.Equal(bed.Roots.Count, bed.Lengths.Count);
            // Dense: about a strand to every half square metre of it.
            Assert.True(bed.Roots.Count >= 0.5f * MathF.PI * bed.Radius * bed.Radius * 0.6f, $"{bed.Roots.Count} strands in a {bed.Radius:0.0} m bed");
            foreach (var (root, i) in bed.Roots.Select((r, i) => (r, i)))
            {
                Assert.True(map.HeightAt(root) <= LevelMap.BlockHeight - 0.2f, "rooted in the rock");
                Assert.InRange(bed.Lengths[i], TopDownGenerator.KelpLengthMin, TopDownGenerator.KelpLengthMax);
            }
            Assert.True(Vector2.Distance(bed.Center, map.Start.Position) > map.Start.Radius + bed.Radius);
            Assert.True(Vector2.Distance(bed.Center, map.Exit.Position) > map.Exit.Radius + bed.Radius);
            Assert.All(map.Pois.Where(p => p.Kind is not (PoiKind.Start or PoiKind.Exit)), p => Assert.True(Vector2.Distance(bed.Center, p.Position) > p.Radius + bed.Radius));
            Assert.All(map.Kelp.Where(o => o != bed), o => Assert.True(Vector2.Distance(o.Center, bed.Center) >= o.Radius + bed.Radius + TopDownGenerator.KelpApart - 0.01f));
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void TheExitChamberSlopesDownFromItsWallsToASmallDiveZone(string seed)
    {
        var map = Map(seed);
        Assert.False(map.HasBoss);
        Assert.True(map.Shaft.HalfLength <= 3f, $"dive zone {map.Shaft.HalfLength:0.0} m");
        // Going down from the walls: halfway out from the middle the floor is already well under the seabed, and it
        // keeps sinking toward the middle.
        var c = map.Shaft.Center;
        for (int i = 0; i < 16; i++)
        {
            var dir = new Vector2(MathF.Cos(i * MathF.Tau / 16f), MathF.Sin(i * MathF.Tau / 16f));
            float wall = 0f;
            while (wall < 20f && map.HeightAt(c + dir * (wall + 0.5f)) <= 0f) wall += 0.5f;
            if (wall < 4f) continue;
            float half = map.HeightAt(c + dir * wall * 0.5f), quarter = map.HeightAt(c + dir * wall * 0.25f);
            Assert.True(half < -2f, $"halfway to the wall {half:0.0} m");
            Assert.True(quarter <= half + 0.05f, "it sinks toward the middle");
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void TheBossArenaIsLevelAroundTheCrack(string seed)
    {
        var map = Map(seed, BossLevel(seed));
        Assert.True(map.HasBoss);
        var arena = map.Exit;
        Assert.Equal(15f, arena.Radius);
        Assert.True(map.Shaft.HalfLength > map.Shaft.HalfWidth, "the Crack is a fissure");
        for (int i = 0; i < 32; i++)
        {
            float a = i * MathF.Tau / 32f;
            float h = map.HeightAt(arena.Position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * arena.Radius * 0.8f);
            Assert.InRange(h, -1.05f, -0.95f);
        }
        // An ordinary level's exit is a round blue hole and no boss stands there.
        var plain = Map(seed);
        Assert.False(plain.HasBoss);
        Assert.Equal(plain.Shaft.HalfLength, plain.Shaft.HalfWidth);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void TheExitIsAtLeast90MetresAwayBySwimming(string seed)
    {
        foreach (var map in new[] { Map(seed), Map(seed, BossLevel(seed)) })
        {
            float geodesic = LevelValidator.DistanceAt(LevelValidator.Distances(map, map.Start.Position), map.Exit.Position);
            _out.WriteLine($"{seed} {map.Id}: exit {geodesic:0} m by swimming, {Vector2.Distance(map.Start.Position, map.Exit.Position):0} m straight, attempt {map.Attempt + 1}");
            Assert.True(geodesic >= LevelValidator.MinExitGeodesic, $"{geodesic:0} m");
        }
    }

    // ───────────────────────── reachability ─────────────────────────

    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryPoiCanBeReachedFromTheStart(string seed)
    {
        var map = Map(seed);
        var dist = LevelValidator.Distances(map, map.Start.Position);
        foreach (var poi in map.Pois)
            Assert.False(float.IsPositiveInfinity(LevelValidator.DistanceAt(dist, poi.Position)), $"{poi.Kind} unreachable");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void ThePlayableAreaKeepsTheAreaRule(string seed)
    {
        foreach (var map in new[] { Map(seed), Map(seed, BossLevel(seed)) })
        {
            int area = LevelValidator.WaterArea(map);
            _out.WriteLine($"{seed} {map.Id}: {area} m² of water, attempt {map.Attempt + 1}");
            Assert.InRange(area, LevelValidator.AreaMin, LevelValidator.AreaMax);
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void SideTunnelsMakeALabyrinth(string seed)
    {
        var map = Map(seed);
        var sides = map.Corridors.Where(c => c.Kind == CorridorKind.Side).ToList();
        Assert.True(sides.Count >= 3, $"{sides.Count} side tunnels");
        Assert.All(sides, c => Assert.InRange(c.Width, 4.5f, 7f));
        // Some side branches close a loop back into the cave.
        Assert.True(map.Plazas.Count >= 6, $"{map.Plazas.Count} chambers");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void ASecretCaveIsPluggedByWeakRock(string seed)
    {
        var map = Map(seed);
        foreach (var poi in map.Pois.Where(p => p.Kind == PoiKind.Secret))
        {
            var rock = Assert.Single(map.WeakRocks, r => r.Cave == poi.Cave);
            Assert.True(Vector2.Distance(rock.Center, map.Caves[poi.Cave].Mouth) < 3f);
        }
    }

    // ───────────────────────── paths and terrain ─────────────────────────

    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryPathLiesUnderOpenWater(string seed)
    {
        var map = Map(seed);
        Assert.Null(LevelValidator.CheckCorridorFloors(map));
        // Nothing on a path rises to the swim level.
        foreach (var c in map.Corridors)
            foreach (var p in c.Points)
                Assert.True(map.HeightAt(p) <= -LevelValidator.PathClearance);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void ThePathNetworkKeepsItsShape(string seed)
    {
        var map = Map(seed);
        // The main chain: tunnels from chamber to chamber, from the start to the way down.
        var mains = map.Corridors.Where(c => c.Kind == CorridorKind.Main).ToList();
        Assert.InRange(mains.Count, 2, 10);
        Assert.All(mains, c => Assert.InRange(c.Width, 6f, 9f));
        Assert.Equal(map.Start.Position, mains[0].Points[0]);
        Assert.Equal(map.Exit.Position, mains[^1].Points[^1]);
        for (int i = 1; i < mains.Count; i++) Assert.Equal(mains[i - 1].Points[^1], mains[i].Points[0]);
        Assert.All(map.Corridors.Where(c => c.Kind == CorridorKind.Spur), c => Assert.True(c.Length <= LevelValidator.MaxSpur, $"spur {c.Length:0.0} m"));
        Assert.All(map.Corridors.Where(c => c.Kind == CorridorKind.Pass), c => Assert.InRange(c.Width, 3f, 5f));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void MountainsStayInRangeAndTheRimHolds(string seed)
    {
        var map = Map(seed);
        Assert.InRange(LevelValidator.InteriorPeak(map), 3f, 14f);
        // The rim is impassable all the way round.
        for (int i = 0; i <= LevelMap.Size; i++)
        {
            Assert.False(map.IsOpen(new Vector2(i, 1f)));
            Assert.False(map.IsOpen(new Vector2(1f, i)));
            Assert.False(map.IsOpen(new Vector2(i, LevelMap.Size - 1f)));
            Assert.False(map.IsOpen(new Vector2(LevelMap.Size - 1f, i)));
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void ArchesSpanTheirCorridors(string seed)
    {
        var map = Map(seed);
        // Where flanks allow.
        var arches = map.Canopies.Where(c => c.Kind == CanopyKind.Arch).ToList();
        Assert.InRange(arches.Count, 0, 5);
        foreach (var arch in arches)
        {
            Assert.InRange(arch.HalfLength * 2f, TopDownGenerator.ArchSpanMin, TopDownGenerator.ArchSpanMax);
            Assert.True(LevelValidator.ArchCrossesCorridor(map, arch));
            Assert.True(arch.Bottom > LevelMap.SwimBand + 1f, "arch body must clear the swim band");
            // Both footings stand on raised ground; the corridor under the span is open.
            Assert.True(map.HeightAt(arch.EndA) > LevelMap.BlockHeight);
            Assert.True(map.HeightAt(arch.EndB) > LevelMap.BlockHeight);
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void CaveMouthsFaceCorridorsOrOpenWater(string seed)
    {
        var map = Map(seed);
        // Level 1 always has a treasure room, in a cave.
        Assert.NotEmpty(map.Caves);
        foreach (var cave in map.Caves)
        {
            Assert.True(LevelValidator.CaveMouthOpens(map, cave));
            Assert.Contains(map.Corridors, c => c.Kind == CorridorKind.Spur && c.Poi == cave.Poi);
            // Roofed: every chamber has canopy over it.
            foreach (var (center, _) in cave.Chambers) Assert.True(map.CanopyAt(center) >= 0);
        }
        for (int i = 0; i < map.Caves.Count; i++)
        for (int j = i + 1; j < map.Caves.Count; j++)
            Assert.True(Vector2.Distance(map.Pois[map.Caves[i].Poi].Position, map.Pois[map.Caves[j].Poi].Position) >= 25f);
    }

    // ───────────────────────── POIs, loot and spawns ─────────────────────────

    [Theory]
    [MemberData(nameof(Seeds))]
    public void PoisFollowThePlanAndTheScatterRules(string seed)
    {
        foreach (var id in new[] { LevelId.First, new LevelId(1, LevelPlan.Tutorial, 2), LevelId.FirstRun })
        {
            var map = Map(seed, id);
            var plan = LevelPlan.For(SeedCode.Parse(seed), id);
            int Count(PoiKind k) => map.Pois.Count(p => p.Kind == k);
            Assert.Equal(plan.Treasures, Count(PoiKind.TreasureCave));
            Assert.Equal(plan.Secrets, Count(PoiKind.Secret));
            Assert.Equal(plan.Curses, Count(PoiKind.CurseDen));
            Assert.Equal(plan.Shops, Count(PoiKind.Shop));
            Assert.Equal(plan.Caches, Count(PoiKind.ShellCache));
            Assert.Equal(plan.Ambushes, Count(PoiKind.Ambush));

            var start = map.Start.Position;
            var exit = map.Exit.Position;
            var others = map.Pois.Where(p => p.Kind is not (PoiKind.Start or PoiKind.Exit)).ToList();
            for (int i = 0; i < others.Count; i++)
            {
                Assert.True(Vector2.Distance(others[i].Position, exit) >= CaveLayout.FromExit);
                Assert.True(Vector2.Distance(others[i].Position, start) >= CaveLayout.FromStart);
                for (int j = i + 1; j < others.Count; j++)
                    Assert.True(Vector2.Distance(others[i].Position, others[j].Position) >= CaveLayout.Spacing);
            }
            if (others.Count == 0) continue;
            // The first place she meets sits just past the start (unless the shell cache is the only place).
            if (others.Any(p => p.Kind != PoiKind.ShellCache))
            {
                var early = Assert.Single(others, p => p.Early);
                Assert.InRange(Vector2.Distance(early.Position, start), CaveLayout.FromStart, CaveLayout.EarlyMax);
            }
            else Assert.DoesNotContain(others, p => p.Early);

            // A shell cache sits 40–60% along the start→exit axis, off the main corridors.
            foreach (var cache in map.Pois.Where(p => p.Kind == PoiKind.ShellCache))
            {
                Vector2 axis = exit - start;
                float t = Vector2.Dot(cache.Position - start, axis) / axis.LengthSquared();
                Assert.InRange(t, 0.3f, 0.7f);
                Assert.False(map.Corridors.Where(c => c.Kind == CorridorKind.Main).Any(c => c.Contains(cache.Position)));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void LootAndSpawnsAreLaidOut(string seed)
    {
        var map = Map(seed);
        Assert.InRange(map.Pockets.Count, TopDownGenerator.PocketsMin, TopDownGenerator.PocketsMax);
        Assert.InRange(map.Coins.Count, TopDownGenerator.CoinsMin, TopDownGenerator.CoinsMax);
        Assert.All(map.Coins, c => Assert.True(map.HeightAt(c.Position) <= -1f, "coins lie in the seabed under open water"));
        Assert.Contains(map.Spawns, s => s.Role == SpawnRole.Patrol);
        Assert.Contains(map.Spawns, s => s.Role == SpawnRole.Ambush);
        // A shop is safe water: no den in it.
        foreach (var shop in map.Pois.Where(p => p.Kind == PoiKind.Shop))
            Assert.DoesNotContain(map.Spawns, s => s.Role == SpawnRole.Den && Vector2.Distance(s.Position, shop.Position) < shop.Radius);
        Assert.NotEmpty(map.Decor);
    }
}
