using System;
using System.Collections.Generic;
using System.Numerics;

namespace OctoShoots.Core.Terrain;

public sealed class MeshData
{
    public List<Vector3> Positions { get; } = new();
    public List<Vector3> Normals { get; } = new();

    /// <summary>Triangles wound counter-clockwise seen from the water side (right-handed).</summary>
    public List<int> Indices { get; } = new();
}

/// <summary>
/// Naive surface nets over a <see cref="VoxelSdf"/>: one vertex per surface cell, one quad per
/// sign-changing grid edge. Chunks share vertex positions exactly, so they stitch without seams.
/// </summary>
public static class SurfaceNets
{
    public const int ChunkSize = 32;

    static readonly int[,] EdgeCorners =
    {
        { 0, 1 }, { 2, 3 }, { 4, 5 }, { 6, 7 }, // x edges
        { 0, 2 }, { 1, 3 }, { 4, 6 }, { 5, 7 }, // y edges
        { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 }, // z edges
    };

    /// <summary>Meshes the grid edges whose base point lies in [x0,x1)×[y0,y1)×[z0,z1).</summary>
    public static MeshData BuildChunk(VoxelSdf sdf, int x0, int y0, int z0, int x1, int y1, int z1)
    {
        var mesh = new MeshData();
        if (NoSurfaceNear(sdf, x0, y0, z0, x1, y1, z1)) return mesh;
        var cellVertex = new Dictionary<int, int>();
        int nx = sdf.Nx, ny = sdf.Ny, nz = sdf.Nz;

        for (int z = z0; z < z1 && z < nz; z++)
        for (int y = y0; y < y1 && y < ny; y++)
        for (int x = x0; x < x1 && x < nx; x++)
        {
            float v = sdf[x, y, z];
            bool rock = v < 0f;

            if (x + 1 < nx && y >= 1 && z >= 1 && y <= ny - 2 && z <= nz - 2 && rock != (sdf[x + 1, y, z] < 0f))
                EmitQuad(sdf, mesh, cellVertex,
                    (x, y - 1, z - 1), (x, y, z - 1), (x, y, z), (x, y - 1, z),
                    rock ? Vector3.UnitX : -Vector3.UnitX);

            if (y + 1 < ny && x >= 1 && z >= 1 && x <= nx - 2 && z <= nz - 2 && rock != (sdf[x, y + 1, z] < 0f))
                EmitQuad(sdf, mesh, cellVertex,
                    (x - 1, y, z - 1), (x, y, z - 1), (x, y, z), (x - 1, y, z),
                    rock ? Vector3.UnitY : -Vector3.UnitY);

            if (z + 1 < nz && x >= 1 && y >= 1 && x <= nx - 2 && y <= ny - 2 && rock != (sdf[x, y, z + 1] < 0f))
                EmitQuad(sdf, mesh, cellVertex,
                    (x - 1, y - 1, z), (x, y - 1, z), (x, y, z), (x - 1, y, z),
                    rock ? Vector3.UnitZ : -Vector3.UnitZ);
        }

        return mesh;
    }

    /// <summary>True when every storage chunk the range (plus its one-point border) touches is uniform with one sign.</summary>
    static bool NoSurfaceNear(VoxelSdf sdf, int x0, int y0, int z0, int x1, int y1, int z1)
    {
        int Lo(int v) => Math.Max(0, v - 1) / VoxelSdf.ChunkSize;
        int cx1 = Math.Min(sdf.ChunksX - 1, (x1 + 1) / VoxelSdf.ChunkSize);
        int cy1 = Math.Min(sdf.ChunksY - 1, (y1 + 1) / VoxelSdf.ChunkSize);
        int cz1 = Math.Min(sdf.ChunksZ - 1, (z1 + 1) / VoxelSdf.ChunkSize);
        bool? rock = null;
        for (int cz = Lo(z0); cz <= cz1; cz++)
        for (int cy = Lo(y0); cy <= cy1; cy++)
        for (int cx = Lo(x0); cx <= cx1; cx++)
        {
            if (!sdf.IsUniform(cx, cy, cz, out float v)) return false;
            bool r = v < 0f;
            if (rock is null) rock = r;
            else if (rock != r) return false;
        }
        return true;
    }

    static void EmitQuad(VoxelSdf sdf, MeshData mesh, Dictionary<int, int> cellVertex,
        (int, int, int) c0, (int, int, int) c1, (int, int, int) c2, (int, int, int) c3, Vector3 waterDir)
    {
        int a = CellVertex(sdf, mesh, cellVertex, c0);
        int b = CellVertex(sdf, mesh, cellVertex, c1);
        int c = CellVertex(sdf, mesh, cellVertex, c2);
        int d = CellVertex(sdf, mesh, cellVertex, c3);

        Vector3 pa = mesh.Positions[a], pb = mesh.Positions[b], pc = mesh.Positions[c];
        bool flip = Vector3.Dot(Vector3.Cross(pb - pa, pc - pa), waterDir) < 0f;
        if (flip)
        {
            mesh.Indices.AddRange(new[] { a, c, b, a, d, c });
        }
        else
        {
            mesh.Indices.AddRange(new[] { a, b, c, a, c, d });
        }
    }

    static int CellVertex(VoxelSdf sdf, MeshData mesh, Dictionary<int, int> cellVertex, (int X, int Y, int Z) cell)
    {
        int key = sdf.Index(cell.X, cell.Y, cell.Z);
        if (cellVertex.TryGetValue(key, out int existing)) return existing;

        Span<float> corner = stackalloc float[8];
        for (int i = 0; i < 8; i++)
            corner[i] = sdf[cell.X + (i & 1), cell.Y + ((i >> 1) & 1), cell.Z + ((i >> 2) & 1)];

        Vector3 sum = Vector3.Zero;
        int count = 0;
        for (int e = 0; e < 12; e++)
        {
            int i0 = EdgeCorners[e, 0], i1 = EdgeCorners[e, 1];
            float v0 = corner[i0], v1 = corner[i1];
            if ((v0 < 0f) == (v1 < 0f)) continue;
            float t = v0 / (v0 - v1);
            sum += Vector3.Lerp(CornerOffset(i0), CornerOffset(i1), t);
            count++;
        }

        Vector3 local = count > 0 ? sum / count : new Vector3(0.5f);
        Vector3 world = sdf.Origin + (new Vector3(cell.X, cell.Y, cell.Z) + local) * sdf.Cell;
        int index = mesh.Positions.Count;
        mesh.Positions.Add(world);
        mesh.Normals.Add(sdf.Gradient(world));
        cellVertex[key] = index;
        return index;
    }

    static Vector3 CornerOffset(int i) => new(i & 1, (i >> 1) & 1, (i >> 2) & 1);
}
