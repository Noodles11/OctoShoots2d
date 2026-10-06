using System;
using System.Collections.Generic;
using System.Numerics;

namespace OctoShoots.Core.Terrain;

/// <summary>Flood fill over grid points with enough clearance for a sphere of a given radius.</summary>
public static class Reachability
{
    /// <summary>
    /// Every grid point reachable from <paramref name="start"/> through points with at least
    /// <paramref name="clearance"/> of open water. An optional sphere can be treated as solid.
    /// </summary>
    public static bool[] FloodFill(VoxelSdf sdf, Vector3 start, float clearance, Vector3? blockCenter = null, float blockRadius = 0f)
    {
        int nx = sdf.Nx, ny = sdf.Ny, nz = sdf.Nz;
        int strideY = nx, strideZ = nx * ny;
        var visited = new bool[nx * ny * nz];
        var (sx, sy, sz) = Nearest(sdf, start);
        if (sdf[sx, sy, sz] < clearance) return visited;

        // Grid-space blocker, so the inner loop never builds positions.
        Vector3 bc = blockCenter.HasValue ? (blockCenter.Value - sdf.Origin) / sdf.Cell : Vector3.Zero;
        float br2 = blockCenter.HasValue ? blockRadius * blockRadius / (sdf.Cell * sdf.Cell) : -1f;

        var queue = new Queue<int>();
        int first = sdf.Index(sx, sy, sz);
        queue.Enqueue(first);
        visited[first] = true;

        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            int x = i % nx, y = i / strideY % ny, z = i / strideZ;
            if (x > 0) Visit(i - 1, x - 1, y, z);
            if (x < nx - 1) Visit(i + 1, x + 1, y, z);
            if (y > 0) Visit(i - strideY, x, y - 1, z);
            if (y < ny - 1) Visit(i + strideY, x, y + 1, z);
            if (z > 0) Visit(i - strideZ, x, y, z - 1);
            if (z < nz - 1) Visit(i + strideZ, x, y, z + 1);
        }
        return visited;

        void Visit(int j, int x, int y, int z)
        {
            if (visited[j] || sdf[x, y, z] < clearance) return;
            if (br2 > 0f)
            {
                float dx = x - bc.X, dy = y - bc.Y, dz = z - bc.Z;
                if (dx * dx + dy * dy + dz * dz < br2) return;
            }
            visited[j] = true;
            queue.Enqueue(j);
        }
    }

    /// <summary>
    /// Flood fill limited to a box of the grid: cheap enough to check one cave of a huge open level.
    /// Returns a predicate telling whether a point (inside the box) was reached.
    /// </summary>
    public static Func<Vector3, bool> LocalFlood(VoxelSdf sdf, Vector3 start, float clearance, Vector3 boxMin, Vector3 boxMax, Vector3? blockCenter = null, float blockRadius = 0f)
    {
        var (x0, y0, z0) = Nearest(sdf, boxMin);
        var (x1, y1, z1) = Nearest(sdf, boxMax);
        int w = x1 - x0 + 1, h = y1 - y0 + 1, d = z1 - z0 + 1;
        var visited = new bool[w * h * d];
        int Local(int x, int y, int z) => ((z - z0) * h + (y - y0)) * w + (x - x0);
        bool Inside(int x, int y, int z) => x >= x0 && x <= x1 && y >= y0 && y <= y1 && z >= z0 && z <= z1;
        float br2 = blockCenter.HasValue ? blockRadius * blockRadius : -1f;
        Vector3 bc = blockCenter ?? Vector3.Zero;

        bool Reached(Vector3 p)
        {
            var (x, y, z) = Nearest(sdf, p);
            return Inside(x, y, z) && visited[Local(x, y, z)];
        }

        var (sx, sy, sz) = Nearest(sdf, start);
        if (!Inside(sx, sy, sz) || sdf[sx, sy, sz] < clearance) return Reached;
        var queue = new Queue<(int, int, int)>();
        visited[Local(sx, sy, sz)] = true;
        queue.Enqueue((sx, sy, sz));
        Span<(int, int, int)> steps = stackalloc (int, int, int)[6] { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) };
        while (queue.Count > 0)
        {
            var (x, y, z) = queue.Dequeue();
            foreach (var (dx, dy, dz) in steps)
            {
                int nx = x + dx, ny = y + dy, nz = z + dz;
                if (!Inside(nx, ny, nz)) continue;
                int i = Local(nx, ny, nz);
                if (visited[i] || sdf[nx, ny, nz] < clearance) continue;
                if (br2 > 0f && Vector3.DistanceSquared(sdf.PointPosition(nx, ny, nz), bc) < br2) continue;
                visited[i] = true;
                queue.Enqueue((nx, ny, nz));
            }
        }
        return Reached;
    }

    public static bool IsReached(VoxelSdf sdf, bool[] visited, Vector3 p)
    {
        var (x, y, z) = Nearest(sdf, p);
        return visited[sdf.Index(x, y, z)];
    }

    static (int, int, int) Nearest(VoxelSdf sdf, Vector3 p)
    {
        Vector3 f = (p - sdf.Origin) / sdf.Cell;
        return (
            Math.Clamp((int)MathF.Round(f.X), 0, sdf.Nx - 1),
            Math.Clamp((int)MathF.Round(f.Y), 0, sdf.Ny - 1),
            Math.Clamp((int)MathF.Round(f.Z), 0, sdf.Nz - 1));
    }
}
