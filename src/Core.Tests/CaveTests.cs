using System;
using System.Numerics;
using OctoShoots.Core.Terrain;
using Xunit;

namespace OctoShoots.Core.Tests;

public class CaveTests
{
    [Fact]
    public void SpawnsAreInOpenWater()
    {
        var cave = TestWorlds.Cave;
        Assert.True(cave.Sdf.Sample(cave.PlayerSpawn) > 1f);
        foreach (var s in cave.EnemySpawns) Assert.True(cave.Sdf.Sample(s) > 1f, $"enemy spawn {s} is too close to rock");
    }

    [Fact]
    public void EverySpawnIsReachableWithPlayerClearance()
    {
        var cave = TestWorlds.Cave;
        var reached = Reachability.FloodFill(cave.Sdf, cave.PlayerSpawn, 0.4f);
        foreach (var s in cave.EnemySpawns) Assert.True(Reachability.IsReached(cave.Sdf, reached, s), $"{s} unreachable");
    }

    [Fact]
    public void OuterShellIsRock()
    {
        var sdf = TestWorlds.Cave.Sdf;
        for (int x = 0; x < sdf.Nx; x += 7)
        for (int z = 0; z < sdf.Nz; z += 7)
        {
            Assert.True(sdf[x, 0, z] < 0f);
            Assert.True(sdf[x, sdf.Ny - 1, z] < 0f);
        }
    }

    [Fact]
    public void SameSeedBuildsSameCave()
    {
        var a = GreyboxCave.Build(99);
        var b = GreyboxCave.Build(99);
        var p = new Vector3(24.3f, 14.7f, 22.1f);
        Assert.Equal(a.Sdf.Sample(p), b.Sdf.Sample(p));
    }

    [Fact]
    public void MeshSitsOnTheSurfaceAndFacesTheWater()
    {
        var sdf = TestWorlds.Cave.Sdf;
        var mesh = SurfaceNets.BuildChunk(sdf, 32, 0, 32, 64, 64, 64);
        Assert.NotEmpty(mesh.Indices);
        Assert.Equal(0, mesh.Indices.Count % 3);

        foreach (var v in mesh.Positions) Assert.True(MathF.Abs(sdf.Sample(v)) < sdf.Cell, $"vertex {v} is off the surface");

        int facingWater = 0, triangles = mesh.Indices.Count / 3;
        for (int i = 0; i < mesh.Indices.Count; i += 3)
        {
            Vector3 a = mesh.Positions[mesh.Indices[i]], b = mesh.Positions[mesh.Indices[i + 1]], c = mesh.Positions[mesh.Indices[i + 2]];
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, sdf.Gradient((a + b + c) / 3f)) > 0f) facingWater++;
        }
        Assert.True(facingWater > triangles * 0.98, $"{facingWater}/{triangles} triangles face the water");
    }

    [Fact]
    public void ChunksShareBorderVertices()
    {
        var sdf = TestWorlds.Cave.Sdf;
        var left = SurfaceNets.BuildChunk(sdf, 0, 0, 0, 32, 64, 64);
        var right = SurfaceNets.BuildChunk(sdf, 32, 0, 0, 64, 64, 64);
        var leftSet = new System.Collections.Generic.HashSet<Vector3>(left.Positions);
        int shared = 0;
        foreach (var v in right.Positions) if (leftSet.Contains(v)) shared++;
        Assert.True(shared > 0, "neighbouring chunks should produce identical vertices along their border");
    }
}
