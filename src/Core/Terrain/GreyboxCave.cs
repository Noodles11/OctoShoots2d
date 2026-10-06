using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using OctoShoots.Core.Loot;
using OctoShoots.Core.Sim;
using static OctoShoots.Core.Terrain.SdfMath;

namespace OctoShoots.Core.Terrain;

/// <summary>
/// A sea anemone rooted on the seabed: reef decoration, and sometimes a nest where a school of
/// clownfish (with a hidden ninja among them) lives.
/// </summary>
public sealed record AnemoneSpot(Vector3 Position, Vector3 Up, float Radius, bool Nest, int SchoolSize, int Hue)
{
    /// <summary>Tentacle height above the base.</summary>
    public float Height => Radius * 1.25f;

    /// <summary>Where a creature coming out of the anemone appears.</summary>
    public Vector3 Mouth => Position + Up * (Height * 0.55f);
}

/// <summary>A playable piece of terrain: the SDF plus where Clementine and the creatures start.</summary>
public sealed class Cave
{
    public required VoxelSdf Sdf { get; init; }
    public required Vector3 PlayerSpawn { get; init; }
    public required Vector3[] EnemySpawns { get; init; }

    /// <summary>Grid points this close to the border are the indestructible outer shell (§6.3).</summary>
    public int ShellCells { get; init; } = 4;

    /// <summary>Height of the sea surface: nothing swims above it. Infinite in closed caves.</summary>
    public float SurfaceY { get; init; } = float.PositiveInfinity;

    public LootPlan Loot { get; init; } = new();

    public IReadOnlyList<AnemoneSpot> Anemones { get; init; } = Array.Empty<AnemoneSpot>();

    /// <summary>Where the creatures live.</summary>
    public IReadOnlyList<DenSpot> Dens { get; init; } = Array.Empty<DenSpot>();

    /// <summary>A copy with its own SDF, so craters don't touch the original.</summary>
    public Cave Clone(IReadOnlyList<DenSpot>? dens = null) => new()
    {
        Sdf = Sdf.Clone(),
        PlayerSpawn = PlayerSpawn,
        EnemySpawns = EnemySpawns,
        ShellCells = ShellCells,
        SurfaceY = SurfaceY,
        Loot = Loot,
        Anemones = Anemones,
        Dens = dens ?? Dens,
    };
}

/// <summary>
/// The M0 feel-prototype cave: one ~24×16×24 m chamber with a flat floor, a pillar, an arch,
/// an overhang, a tunnel to a side pocket and a chimney up to a small upper pocket.
/// Hand-placed shapes with seeded noise, baked into a 0.5 m voxel SDF.
/// </summary>
public static class GreyboxCave
{
    public const float Cell = 0.5f;
    public const float Shell = 2f;
    public const float FloorY = 8.3f;
    static readonly Vector3 Extent = new(60f, 34f, 40f);

    public static Cave Build(ulong seed)
    {
        uint noiseSeed = (uint)(new Rng(seed).Stream("cave").NextU64() >> 32);
        int nx = (int)(Extent.X / Cell) + 1;
        int ny = (int)(Extent.Y / Cell) + 1;
        int nz = (int)(Extent.Z / Cell) + 1;
        var sdf = new VoxelSdf(nx, ny, nz, Cell, Vector3.Zero);

        Parallel.For(0, nz, z =>
        {
            for (int y = 0; y < ny; y++)
            for (int x = 0; x < nx; x++)
                sdf[x, y, z] = Field(sdf.PointPosition(x, y, z), noiseSeed);
        });

        return new Cave
        {
            Sdf = sdf,
            ShellCells = (int)(Shell / Cell),
            PlayerSpawn = new Vector3(17f, 14f, 15f),
            EnemySpawns = new[]
            {
                new Vector3(30f, 14f, 28f),
                new Vector3(28f, 18f, 13f),
                new Vector3(46f, 12f, 26f),
                new Vector3(22f, 12f, 31f),
                new Vector3(12f, 26.5f, 9f),
                new Vector3(33f, 11f, 20f),
                new Vector3(49f, 13f, 22f),
                new Vector3(15f, 18f, 24f),
            },
            Anemones = new[]
            {
                new AnemoneSpot(new Vector3(24.5f, 8.35f, 17f), Vector3.UnitY, 1.8f, true, 4, 0),
                new AnemoneSpot(new Vector3(21f, 8.35f, 19f), Vector3.UnitY, 1f, false, 0, 3),
                new AnemoneSpot(new Vector3(26.5f, 8.35f, 24f), Vector3.UnitY, 1.2f, false, 0, 5),
            },
            // A little of everything on the chamber floor (y 8.3) for testing.
            Loot = new LootPlan
            {
                Shells = { new ShellSpot(new Vector3(27f, 8.8f, 18f), "treasure", false), new ShellSpot(new Vector3(18.5f, 8.8f, 20f), "shop", true) },
                Chests = { new ChestSpot(new Vector3(29f, 8.9f, 21f)) },
                Pickups =
                {
                    new PickupSpot(new Vector3(21f, 8.7f, 17f), PickupKind.Coin),
                    new PickupSpot(new Vector3(21.6f, 8.7f, 17.4f), PickupKind.Coin),
                    new PickupSpot(new Vector3(26f, 8.7f, 15f), PickupKind.HalfHeart),
                    new PickupSpot(new Vector3(18f, 8.7f, 26f), PickupKind.Bomb),
                },
                Buried = { new BuriedCoins(new Vector3(24f, 7.9f, 26f), 3) },
            },
        };
    }

    /// <summary>Analytic field: positive in water.</summary>
    static float Field(Vector3 p, uint seed)
    {
        // Water volumes, smoothly merged.
        float open = -Ellipsoid(p, new Vector3(24f, 15f, 22f), new Vector3(12f, 8f, 12f));
        open = SMax(open, -Ellipsoid(p, new Vector3(46f, 12f, 26f), new Vector3(7f, 5f, 7f)), 1.5f);
        open = SMax(open, -Capsule(p, new Vector3(34f, 14f, 24f), new Vector3(38f, 14.5f, 27f), 2.1f), 1.5f);
        open = SMax(open, -Capsule(p, new Vector3(38f, 14.5f, 27f), new Vector3(42f, 12.5f, 25.5f), 2.1f), 1.5f);
        open = SMax(open, -Capsule(p, new Vector3(20f, 20f, 16f), new Vector3(14f, 25f, 11f), 2.4f), 1.5f);
        open = SMax(open, -Ellipsoid(p, new Vector3(12f, 27f, 9f), new Vector3(4.5f, 3f, 4.5f)), 1.5f);

        open += 0.65f * Noise(p * 0.22f, seed) + 0.3f * Noise(p * 0.55f, seed + 17u);

        // Flat floor with a little relief.
        open = SMin(open, p.Y - FloorY + 0.2f * Noise(p * 0.4f, seed + 31u), 0.8f);

        // Rock features.
        open = SMin(open, Capsule(p, new Vector3(20f, 4f, 26f), new Vector3(20f, 26f, 26f), 1.4f + 0.25f * Noise(p * 0.7f, seed + 5u)), 1.2f);
        open = SMin(open, Capsule(p, new Vector3(30f, 4f, 16f), new Vector3(29f, 26f, 17f), 1.1f), 1.0f);
        open = SMin(open, TorusZ(p, new Vector3(27f, FloorY, 26f), 3.2f, 0.75f), 0.6f);
        open = SMin(open, Ellipsoid(p, new Vector3(13f, 15f, 30f), new Vector3(4f, 1f, 3f)), 0.8f);

        // Indestructible outer shell.
        float box = MathF.Min(MathF.Min(MathF.Min(p.X, Extent.X - p.X), MathF.Min(p.Y, Extent.Y - p.Y)), MathF.Min(p.Z, Extent.Z - p.Z)) - Shell;
        return MathF.Min(open, box);
    }
}
