using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Terrain;

namespace OctoShoots.Core.Gen;

/// <summary>Which reef of which depth (DESIGN-3D §6.1; three reefs per depth, 2D §31).</summary>
public readonly record struct ReefSpec(int Depth, int Reef)
{
    /// <summary>Basin width and length in metres: grows 32 m per depth; the first two reefs are 32 m smaller.</summary>
    public float Width => 224f + 32f * (Math.Clamp(Depth, 1, 6) - 1) - (Reef < 3 ? 32f : 0f);

    /// <summary>Height of the water column, seabed rock included.</summary>
    public float Height => 64f;

    public override string ToString() => $"Depth {Depth} · Reef {Reef} of 3";
}

public enum ChamberRole { Normal, Start, Boss, Treasure, Shop, Curse, Secret }

/// <summary>A cave inside a reef formation, or the boss arena.</summary>
public sealed class Chamber
{
    public int Index;
    public ChamberRole Role;
    public Vector3 Center;
    public Vector3 Radii;
    public float FloorY;

    /// <summary>Where the entrance tunnel opens into the sea.</summary>
    public Vector3 Mouth;
    public Vector3[] Tunnel = Array.Empty<Vector3>();
    public float TunnelRadius;

    /// <summary>The secret cave's tunnel is plugged with weak rock.</summary>
    public bool Sealed;

    /// <summary>The formation around the cave, for local checks.</summary>
    public Vector3 BoundsMin;
    public Vector3 BoundsMax;

    public List<Vector3> Spawns { get; } = new();

    public Vector3 TunnelMid => Tunnel[Tunnel.Length / 2];

    /// <summary>Points to test reachability with: spawns, the centre, and just above the floor.</summary>
    public IEnumerable<Vector3> Probes()
    {
        foreach (var s in Spawns) yield return s;
        yield return Center;
        yield return new Vector3(Center.X, FloorY + 1.5f, Center.Z);
    }
}

/// <summary>A rock mass rising from the seabed: a few blended ellipsoids.</summary>
public sealed record Formation(Vector3 Center, float Radius, (Vector3 C, Vector3 R)[] Lobes);

public sealed record SealedPocket(Vector3 Center, float Radius);

/// <summary>
/// A generated reef (DESIGN-3D §6): an open-sea basin with a sunlit surface, a rolling seabed,
/// rim cliffs, reef formations holding caves, the boss reef at the deep end, and hidden secrets.
/// </summary>
public sealed class ReefLayout
{
    public required ReefSpec Spec { get; init; }
    public required Vector3 Size { get; init; }
    public required float SurfaceY { get; init; }
    public required Vector3 StartPosition { get; init; }
    public required List<Formation> Formations { get; init; }
    public required List<Chamber> Chambers { get; init; }
    public required List<SealedPocket> Pockets { get; init; }
    public required Vector3 CrackA { get; init; }
    public required Vector3 CrackB { get; init; }
    public required float ArenaRadius { get; init; }
    public required Cave Cave { get; init; }

    /// <summary>Seabed height in metres on a 1 m grid ((Width+1) × (Length+1)).</summary>
    public required float[] Heights { get; init; }

    /// <summary>How many seeded attempts generation needed to pass validation (1 = first try).</summary>
    public int Attempts { get; init; }

    public Chamber Boss => Chambers.First(c => c.Role == ChamberRole.Boss);
    public Chamber? ByRole(ChamberRole role) => Chambers.FirstOrDefault(c => c.Role == role);

    /// <summary>Seabed height under a point (bilinear).</summary>
    public float FloorHeight(float x, float z) => ReefGenerator.SampleHeights(Heights, (int)Size.X, (int)Size.Z, x, z);

    /// <summary>The cave whose volume contains the point, or null in open water.</summary>
    public Chamber? ChamberAt(Vector3 p)
    {
        Chamber? best = null;
        float bestD = 1.15f;
        foreach (var c in Chambers)
        {
            float d = c.Role == ChamberRole.Boss ? (p - c.Center).Length() / ArenaRadius : ((p - c.Center) / c.Radii).Length();
            if (d < bestD)
            {
                bestD = d;
                best = c;
            }
        }
        return best;
    }
}
