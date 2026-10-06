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
    static readonly ConcurrentDictionary<string, LevelMap> Cache = new();
    readonly ITestOutputHelper _out;

    public TopDownGenTests(ITestOutputHelper output) => _out = output;

    public static readonly TheoryData<string> Seeds = new() { "KELP7Q2Z", "AAAAAAAA", "REEF2345", "DEEPSEA9", "WAVEDRFT", "CRABSHLL", "TANKHAND", "CRACKRFT" };

    static LevelMap Fresh(string seed, int depth = 1, int reef = 1, string salt = "") =>
        TopDownGenerator.Generate(new RunStreams(SeedCode.Parse(seed)), depth, reef, cosmeticSalt: salt);

    static LevelMap Map(string seed) => Cache.GetOrAdd(seed, s => Fresh(s));

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
    public void DifferentSeedsAndReefsGiveDifferentLevels()
    {
        Assert.NotEqual(Map("KELP7Q2Z").ToBytes(), Map("AAAAAAAA").ToBytes());
        Assert.NotEqual(Map("KELP7Q2Z").ToBytes(), Fresh("KELP7Q2Z", reef: 2).ToBytes());
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
        // A seed that needs several attempts: the attempt that succeeds is always the same one.
        string seed = Seeds.Cast<object[]>().Select(row => (string)row[0]).First(s => Map(s).Attempt > 0);
        var a = Fresh(seed);
        var b = Fresh(seed);
        Assert.True(a.Attempt > 0);
        Assert.Equal(a.Attempt, b.Attempt);
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
        Assert.True(open >= 0.5f, $"only {open:P0} of the level is swimmable");
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
    public void TheBossArenaIsLevelAroundTheDeepestRift(string seed)
    {
        var map = Map(seed);
        var rift = map.Rift;
        Assert.True(Vector2.Distance(LevelValidator.DeepestPoint(map), rift.Position) <= rift.Radius);
        Assert.True(map.HeightAt(rift.Position) < -5f, "the rift cuts deep");
        // A ring round the arena, outside the crack: the level seabed.
        for (int i = 0; i < 32; i++)
        {
            float a = i * MathF.Tau / 32f;
            float h = map.HeightAt(rift.Position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * rift.Radius * 0.8f);
            Assert.InRange(h, -1.05f, -0.95f);
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void SideCanyonsMakeALabyrinth(string seed)
    {
        var map = Map(seed);
        var sides = map.Corridors.Where(c => c.Kind == CorridorKind.Side).ToList();
        Assert.True(sides.Count >= 4, $"{sides.Count} side canyons");
        Assert.All(sides, c => Assert.InRange(c.Width, 4f, 6.5f));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void TheRiftIsAtLeast130MetresAwayBySwimming(string seed)
    {
        var map = Map(seed);
        float geodesic = LevelValidator.DistanceAt(LevelValidator.Distances(map, map.Start.Position), map.Rift.Position);
        _out.WriteLine($"{seed}: rift {geodesic:0} m by swimming, {Vector2.Distance(map.Start.Position, map.Rift.Position):0} m straight");
        Assert.True(geodesic >= 130f, $"{geodesic:0} m");
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
        Assert.All(mains, c => Assert.Equal(map.Rift.Position, c.Points[^1]));
        Assert.InRange(map.Plazas.Count, 2, 4);
        Assert.All(map.Corridors.Where(c => c.Kind == CorridorKind.Spur), c => Assert.True(c.Length <= 20f, $"spur {c.Length:0.0} m"));
        var passes = map.Corridors.Where(c => c.Kind == CorridorKind.Pass).ToList();
        Assert.NotEmpty(passes);
        Assert.All(passes, c => Assert.InRange(c.Width, 3f, 5f));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void MountainsAndTrenchesStayInRange(string seed)
    {
        var map = Map(seed);
        Assert.InRange(LevelValidator.InteriorPeak(map), 3f, 14f);
        Assert.InRange(map.Trenches.Count, 1, 3);
        foreach (var t in map.Trenches)
        {
            Assert.InRange(t.Width, 8f, 14f);
            Assert.InRange(t.Depth, 3f, 7f);
            float deepest = t.Points.Min(p => map.HeightAt(p));
            Assert.True(deepest <= -t.Depth * 0.9f, $"trench bottom {deepest:0.0} m for a {t.Depth:0.0} m cut");
        }
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
        var arches = map.Canopies.Where(c => c.Kind == CanopyKind.Arch).ToList();
        Assert.InRange(arches.Count, 1, 5);
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
            Assert.True(Vector2.Distance(map.Pois[map.Caves[i].Poi].Position, map.Pois[map.Caves[j].Poi].Position) >= 30f);
    }

    // ───────────────────────── POIs, loot and spawns ─────────────────────────

    [Theory]
    [MemberData(nameof(Seeds))]
    public void PoisFollowTheScatterRules(string seed)
    {
        var map = Map(seed);
        var start = map.Start.Position;
        var rift = map.Rift.Position;
        var others = map.Pois.Where(p => p.Kind is not (PoiKind.Start or PoiKind.Rift)).ToList();
        for (int i = 0; i < others.Count; i++)
        {
            Assert.True(Vector2.Distance(others[i].Position, rift) >= 40f);
            for (int j = i + 1; j < others.Count; j++)
                Assert.True(Vector2.Distance(others[i].Position, others[j].Position) >= 25f);
        }
        var early = Assert.Single(others, p => Vector2.Distance(p.Position, start) <= 35f);
        Assert.InRange(Vector2.Distance(early.Position, start), 20f, 35f);
        Assert.True(early.Early);
        Assert.Single(map.Pois, p => p.Kind == PoiKind.Shop);
        Assert.InRange(map.Pois.Count(p => p.Kind == PoiKind.Secret), 1, 2);
        Assert.InRange(map.Pois.Count(p => p.Kind == PoiKind.CurseDen), 0, 1);
        Assert.InRange(map.Pois.Count(p => p.Kind == PoiKind.Ambush), 2, 4);

        // The item cache sits 40–60% along the start→rift axis, off the main corridors.
        var item = map.Pois.Single(p => p.Kind == PoiKind.ItemSpawn);
        Vector2 axis = rift - start;
        float t = Vector2.Dot(item.Position - start, axis) / axis.LengthSquared();
        Assert.InRange(t, 0.35f, 0.65f);
        Assert.False(map.Corridors.Where(c => c.Kind == CorridorKind.Main).Any(c => c.Contains(item.Position)));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void LootAndSpawnsAreLaidOut(string seed)
    {
        var map = Map(seed);
        Assert.InRange(map.Pockets.Count, 4, 6);
        Assert.InRange(map.Coins.Count, 8, 12);
        Assert.All(map.Coins, c => Assert.True(map.HeightAt(c.Position) <= -1f, "coins lie in the seabed under open water"));
        Assert.Contains(map.Spawns, s => s.Role == SpawnRole.Patrol);
        Assert.Contains(map.Spawns, s => s.Role == SpawnRole.Ambush);
        // The shop is safe water: no den in it.
        var shop = map.Pois.Single(p => p.Kind == PoiKind.Shop);
        Assert.DoesNotContain(map.Spawns, s => s.Role == SpawnRole.Den && Vector2.Distance(s.Position, shop.Position) < shop.Radius);
        Assert.NotEmpty(map.Decor);
    }
}
