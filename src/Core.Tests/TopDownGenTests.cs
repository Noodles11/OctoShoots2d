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
        TopDownGenerator.Generate(new RunStreams(SeedCode.Parse(seed)), id ?? LevelId.First, cosmeticSalt: salt);

    static LevelMap Map(string seed, LevelId? id = null) => Cache.GetOrAdd((seed, id ?? LevelId.First), k => Fresh(k.Item1, k.Item2));

    /// <summary>The boss level of the seed's first depth.</summary>
    static LevelId BossLevel(string seed) => new(1, 1, LevelPlan.BossLevel(SeedCode.Parse(seed), 1, 1));

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
        Assert.NotEqual(Map("KELP7Q2Z").ToBytes(), Map("KELP7Q2Z", new LevelId(1, 1, 2)).ToBytes());
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
            .SelectMany(s => Enumerable.Range(1, 3).Select(l => (s, new LevelId(1, 1, l))))
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
            Assert.Equal(second == 0 ? 1 : 2, total);
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryDepthEndsWithItsBossOnLevelFourOrFive(string seed)
    {
        var code = SeedCode.Parse(seed);
        var id = LevelId.First;
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
                Assert.Equal(id.Depth == LevelPlan.Depths ? new LevelId(id.Cycle + 1, 1, 1) : new LevelId(id.Cycle, id.Depth + 1, 1), next);
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
    public void TheNextLevelStartsInTheRockAroundTheHole(string seed)
    {
        var code = SeedCode.Parse(seed);
        foreach (var id in new[] { LevelId.First, BossLevel(seed) })
        {
            var above = Map(seed, id);
            var below = Map(seed, LevelPlan.NextOf(code, id));
            int matched = 0;
            for (float y = -Stamp.Pin + 0.5f; y <= Stamp.Pin - 0.5f; y += 1f)
            for (float x = -Stamp.Pin + 0.5f; x <= Stamp.Pin - 0.5f; x += 1f)
            {
                var off = new Vector2(x, y);
                if (off.Length() > Stamp.Pin - 1f || above.Shaft.Contains(above.Exit.Position + off, 1f)) continue;
                float a = above.HeightAt(above.Exit.Position + off), b = below.HeightAt(below.Start.Position + off);
                Assert.True(MathF.Abs(a - b) <= 0.15f, $"{id}: {a:0.00} above vs {b:0.00} below at {off}");
                matched++;
            }
            Assert.True(matched > 300);
            // Below the hole the next level has a floor: no shaft there.
            Assert.False(below.Shaft.Contains(below.Start.Position, Stamp.Pin));
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void TheShaftIsTheOnlyDeepWater(string seed)
    {
        foreach (var map in new[] { Map(seed), Map(seed, BossLevel(seed)) })
        {
            Assert.True(map.Shaft.Contains(LevelValidator.DeepestPoint(map), 1f));
            Assert.True(map.HeightAt(map.Shaft.Center) <= LevelMap.ShaftBottom + 0.01f);
            Assert.True(map.Shaft.Contains(map.Exit.Position));
            for (int y = 0; y <= LevelMap.Size; y++)
            for (int x = 0; x <= LevelMap.Size; x++)
                if (!map.Shaft.Contains(new Vector2(x, y), 1.5f)) Assert.True(map[x, y] > -4.5f, $"deep water at ({x}, {y}): {map[x, y]:0.0} m");
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
    public void MostOfTheLevelIsFreeSwimmingWater(string seed)
    {
        float open = LevelValidator.OpenFraction(Map(seed));
        Assert.True(open >= LevelValidator.MinOpen, $"only {open:P0} of the level is swimmable");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void RoutesKeepARidgeBetweenThemAwayFromWhereTheyMeet(string seed)
    {
        float run = LevelValidator.LongestParallelRun(Map(seed));
        Assert.True(run <= LevelValidator.MaxSideBySide, $"two routes run within {LevelValidator.SideBySideGap} m of each other for {run:0} m");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void SideCanyonsMakeALabyrinth(string seed)
    {
        var map = Map(seed);
        var sides = map.Corridors.Where(c => c.Kind == CorridorKind.Side).ToList();
        Assert.True(sides.Count >= LevelValidator.MinSideCanyons, $"{sides.Count} side canyons");
        Assert.All(sides, c => Assert.InRange(c.Width, 4f, 6.5f));
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
        var mains = map.Corridors.Where(c => c.Kind == CorridorKind.Main).ToList();
        Assert.InRange(mains.Count, 3, 5);
        Assert.All(mains, c => Assert.InRange(c.Width, 6f, 10f));
        Assert.All(mains, c => Assert.Equal(map.Start.Position, c.Points[0]));
        Assert.All(mains, c => Assert.Equal(map.Exit.Position, c.Points[^1]));
        Assert.InRange(map.Plazas.Count, 1, 4);
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
        // Where flanks allow (a smaller square has fewer, and the stamps keep theirs bare).
        var arches = map.Canopies.Where(c => c.Kind == CanopyKind.Arch).ToList();
        Assert.InRange(arches.Count, 0, 5);
        foreach (var arch in arches)
        {
            Assert.InRange(arch.HalfLength * 2f, 10f, 20f);
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
        foreach (var id in new[] { LevelId.First, new LevelId(1, 1, 2), new LevelId(1, 1, 3) })
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
                Assert.True(Vector2.Distance(others[i].Position, exit) >= 40f);
                Assert.True(Vector2.Distance(others[i].Position, start) >= 39f);
                for (int j = i + 1; j < others.Count; j++)
                    Assert.True(Vector2.Distance(others[i].Position, others[j].Position) >= 21f);
            }
            if (others.Count == 0) continue;
            // The first place she meets sits just past the start's stamp (unless the shell cache is the only place).
            if (others.Any(p => p.Kind != PoiKind.ShellCache))
            {
                var early = Assert.Single(others, p => p.Early);
                Assert.InRange(Vector2.Distance(early.Position, start), 39f, 47f);
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
        Assert.InRange(map.Pockets.Count, 3, 5);
        Assert.InRange(map.Coins.Count, 6, 10);
        Assert.All(map.Coins, c => Assert.True(map.HeightAt(c.Position) <= -1f, "coins lie in the seabed under open water"));
        Assert.Contains(map.Spawns, s => s.Role == SpawnRole.Patrol);
        Assert.Contains(map.Spawns, s => s.Role == SpawnRole.Ambush);
        // A shop is safe water: no den in it.
        foreach (var shop in map.Pois.Where(p => p.Kind == PoiKind.Shop))
            Assert.DoesNotContain(map.Spawns, s => s.Role == SpawnRole.Den && Vector2.Distance(s.Position, shop.Position) < shop.Radius);
        Assert.NotEmpty(map.Decor);
    }
}
