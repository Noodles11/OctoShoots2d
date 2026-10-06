using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen;
using OctoShoots.Core.Run;
using OctoShoots.Core.Terrain;
using Xunit;
using Xunit.Abstractions;

namespace OctoShoots.Core.Tests;

/// <summary>Open-sea reef generation (DESIGN-3D §6): layout rules, determinism, validation, secrets.</summary>
public class ReefTests
{
    static readonly ConcurrentDictionary<(string, int, int), ReefLayout> Cache = new();
    readonly ITestOutputHelper _out;

    public ReefTests(ITestOutputHelper output) => _out = output;

    public static ReefLayout Reef(string seed, int depth = 1, int reef = 1) =>
        Cache.GetOrAdd((seed, depth, reef), _ => ReefGenerator.Generate(new RunStreams(SeedCode.Parse(seed)), new ReefSpec(depth, reef)));

    public static readonly TheoryData<string, int> Seeds = new()
    {
        { "KELP 7Q2Z", 1 },
        { "AAAA AAAA", 3 },
        { "REEF 2345", 2 },
        { "DEEP SEA9", 1 },
    };

    [Theory]
    [MemberData(nameof(Seeds))]
    public void LayoutsPassValidation(string seed, int reef)
    {
        var layout = Reef(seed, 1, reef);
        Assert.Null(ReefGenerator.Validate(layout));
        _out.WriteLine($"{seed} reef {reef}: {layout.Formations.Count} formations, {layout.Chambers.Count} caves, {layout.Cave.Sdf.AllocatedChunks} surface chunks, {layout.Attempts} attempt(s)");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryCaveRoleIsPresentOnce(string seed, int reef)
    {
        var layout = Reef(seed, 1, reef);
        foreach (var role in new[] { ChamberRole.Boss, ChamberRole.Treasure, ChamberRole.Shop, ChamberRole.Curse, ChamberRole.Secret })
            Assert.Single(layout.Chambers, c => c.Role == role);
        Assert.Single(layout.Chambers, c => c.Sealed);
    }

    [Fact]
    public void TheSeaIsMostlyOpen()
    {
        var layout = Reef("KELP 7Q2Z");
        var sdf = layout.Cave.Sdf;
        var rng = new Rng(5);
        int open = 0, samples = 2000;
        for (int i = 0; i < samples; i++)
        {
            var p = new Vector3(rng.Range(30f, layout.Size.X - 30f), 0f, rng.Range(30f, layout.Size.Z - 30f));
            p.Y = rng.Range(layout.FloorHeight(p.X, p.Z) + 1f, layout.SurfaceY - 1f);
            if (sdf.Sample(p) > 0f) open++;
        }
        Assert.True(open > samples * 0.7f, $"only {open}/{samples} water samples are open");
        int total = sdf.ChunksX * sdf.ChunksY * sdf.ChunksZ;
        Assert.True(sdf.AllocatedChunks < total * 0.7f, $"{sdf.AllocatedChunks}/{total} chunks allocated");
    }

    [Fact]
    public void StartIsShallowAndTheBossReefDeep()
    {
        var layout = Reef("KELP 7Q2Z");
        Assert.InRange(layout.StartPosition.Y - layout.FloorHeight(layout.StartPosition.X, layout.StartPosition.Z), 8f, 12f);
        float startFloor = layout.FloorHeight(layout.StartPosition.X, layout.StartPosition.Z);
        Assert.True(layout.Boss.FloorY - 2f < startFloor, "the seabed should deepen toward the boss");
        Assert.True(Vector2.Distance(new(layout.StartPosition.X, layout.StartPosition.Z), new(layout.Boss.Center.X, layout.Boss.Center.Z)) > layout.Size.X * 0.6f);
    }

    [Fact]
    public void SameSeedSameReef_DifferentReefDifferentTerrain()
    {
        var a = ReefGenerator.Generate(new RunStreams(SeedCode.Parse("KELP 7Q2Z")), new ReefSpec(1, 1));
        var b = Reef("KELP 7Q2Z", 1, 1);
        Assert.Equal(a.Cave.Sdf.ContentHash(), b.Cave.Sdf.ContentHash());
        Assert.NotEqual(b.Cave.Sdf.ContentHash(), Reef("KELP 7Q2Z", 1, 2).Cave.Sdf.ContentHash());
    }

    [Fact]
    public void BasinGrowsDeeperAndFirstReefsAreSmaller()
    {
        Assert.Equal(224f, new ReefSpec(1, 3).Width);
        Assert.Equal(192f, new ReefSpec(1, 1).Width);
        Assert.True(new ReefSpec(6, 3).Width > new ReefSpec(1, 3).Width);
    }

    [Fact]
    public void LootIsPlannedInWater()
    {
        var layout = Reef("KELP 7Q2Z");
        var loot = layout.Cave.Loot;
        Assert.Contains(loot.Shells, s => s.Pool == "treasure");
        Assert.Contains(loot.Shells, s => s.Pool == "curse");
        Assert.Contains(loot.Shells, s => s.Pool == "secret");
        Assert.Equal(3, loot.Shells.Count(s => s.ForSale));
        Assert.True(loot.Chests.Count >= 6);
        Assert.True(loot.Pickups.Count >= 10);
        Assert.InRange(loot.Buried.Count, 8, 12);
        foreach (var p in loot.Shells.Select(s => s.Position).Concat(loot.Chests.Select(c => c.Position)).Concat(loot.Pickups.Select(p => p.Position)))
            Assert.True(layout.Cave.Sdf.Sample(p) >= 0.3f, $"loot at {p} is in rock");
    }

    [Fact]
    public void AnInkBombOpensTheSecretCave()
    {
        var layout = Reef("KELP 7Q2Z");
        var cave = layout.Cave.Clone();
        var secret = layout.ByRole(ChamberRole.Secret)!;
        Assert.NotNull(cave.Sdf.CarveSphere(secret.TunnelMid, 2.5f, cave.ShellCells));
        var reached = Reachability.LocalFlood(cave.Sdf, secret.Mouth, ReefGenerator.Clearance, secret.BoundsMin, secret.BoundsMax);
        Assert.Contains(secret.Probes(), p => reached(p));
    }

    [Fact]
    public void AnInkBombOnTheSeabedOpensAPocket()
    {
        var layout = Reef("KELP 7Q2Z");
        var cave = layout.Cave.Clone();
        var pocket = layout.Pockets[0];
        float floor = layout.FloorHeight(pocket.Center.X, pocket.Center.Z);
        cave.Sdf.CarveSphere(new Vector3(pocket.Center.X, floor, pocket.Center.Z), 2.5f, cave.ShellCells);
        var above = new Vector3(pocket.Center.X, floor + 1.5f, pocket.Center.Z);
        var reached = Reachability.LocalFlood(cave.Sdf, above, ReefGenerator.Clearance, pocket.Center - new Vector3(6f), pocket.Center + new Vector3(6f, 9f, 6f));
        Assert.True(reached(pocket.Center));
    }

    [Fact]
    public void CratersNeverBreachTheShell()
    {
        var cave = Reef("KELP 7Q2Z").Cave.Clone();
        cave.Sdf.CarveSphere(new Vector3(0.5f, 1f, 60f), 3f, cave.ShellCells);
        for (int x = 0; x < cave.ShellCells; x++) Assert.True(cave.Sdf[x, 2, 120] < 0f);
    }

    [Fact]
    public void CloneKeepsTheOriginalPristine()
    {
        var layout = Reef("KELP 7Q2Z");
        ulong before = layout.Cave.Sdf.ContentHash();
        var clone = layout.Cave.Clone();
        var spot = layout.Cave.Loot.Chests[0].Position;
        clone.Sdf.CarveSphere(spot - new Vector3(0f, 0.6f, 0f), 2.5f, clone.ShellCells);
        Assert.Equal(before, layout.Cave.Sdf.ContentHash());
        Assert.NotEqual(before, clone.Sdf.ContentHash());
    }

    [Fact]
    public void ChamberAtFindsCaves()
    {
        var layout = Reef("KELP 7Q2Z");
        foreach (var c in layout.Chambers) Assert.Same(c, layout.ChamberAt(c.Center));
        Assert.Null(layout.ChamberAt(layout.StartPosition));
    }
}

/// <summary>Where the Depth 1 creatures live (docs/DEPTH1-BESTIARY.md §4).</summary>
public class DenTests
{
    [Theory]
    [InlineData("KELP 7Q2Z", 1)]
    [InlineData("REEF 2345", 2)]
    public void EveryCreatureHasDensInTheRightPlaces(string seed, int reef)
    {
        var layout = ReefTests.Reef(seed, 1, reef);
        var dens = layout.Cave.Dens;
        var sdf = layout.Cave.Sdf;
        foreach (var kind in Sim.CreatureCatalog.Kinds)
            Assert.Contains(dens, d => d.Kind == kind);

        Assert.All(dens, d =>
        {
            Assert.True(sdf.Sample(d.Position + d.Up * 0.8f) > 0.2f, $"{d.Kind} den at {d.Position} is buried");
            Assert.True(System.Numerics.Vector2.Distance(new System.Numerics.Vector2(d.Position.X, d.Position.Z), new System.Numerics.Vector2(layout.StartPosition.X, layout.StartPosition.Z)) >= 27f, $"{d.Kind} den too close to the start");
        });

        // The shallow corner: only jellies, urchins and dancers within 60 m of the start.
        foreach (var d in dens.Where(d => System.Numerics.Vector2.Distance(new System.Numerics.Vector2(d.Position.X, d.Position.Z), new System.Numerics.Vector2(layout.StartPosition.X, layout.StartPosition.Z)) < 60f))
            Assert.True(d.Kind is Sim.EnemyKind.MoonJelly or Sim.EnemyKind.SeaUrchin or Sim.EnemyKind.SpanishDancer, $"{d.Kind} near the start");

        Assert.All(dens.Where(d => d.Kind == Sim.EnemyKind.Barracuda), d => Assert.InRange(d.Count, 1, 2));
        Assert.All(dens.Where(d => d.Kind == Sim.EnemyKind.MoonJelly), d => Assert.InRange(d.Count, 6, 9));
        Assert.All(dens.Where(d => d.Bed), d => Assert.InRange(d.Count, 3, 5));
    }

    [Fact]
    public void DensAreDeterministic()
    {
        var a = ReefGenerator.Generate(new RunStreams(SeedCode.Parse("KELP 7Q2Z")), new ReefSpec(1, 1)).Cave.Dens;
        var b = ReefTests.Reef("KELP 7Q2Z", 1, 1).Cave.Dens;
        Assert.Equal(a.Select(d => (d.Kind, d.Position, d.Count)), b.Select(d => (d.Kind, d.Position, d.Count)));
    }
}
