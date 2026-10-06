using System;
using System.Collections.Generic;
using System.Numerics;

namespace OctoShoots.Core.Terrain;

/// <summary>
/// A breadth-first "distance to the target" field on a coarse 3D lattice, so swimmers can find their way
/// around reef formations (DESIGN-3D §7.1). A cell is open when there is room for a creature at its centre.
/// Which cells are open is worked out lazily and remembered; a crater only needs <see cref="Invalidate"/>.
/// </summary>
public sealed class FlowField
{
    readonly VoxelSdf _sdf;
    readonly float _cell;
    readonly float _clearance;
    readonly int _nx, _ny, _nz;
    readonly byte[] _open;      // 0 unknown, 1 open, 2 blocked
    readonly ushort[] _distance;
    readonly int[] _stamp;
    int _version;

    static readonly (int X, int Y, int Z)[] Steps = BuildSteps();

    public FlowField(VoxelSdf sdf, float cell = 2.5f, float clearance = 1.1f)
    {
        _sdf = sdf;
        _cell = cell;
        _clearance = clearance;
        Vector3 size = sdf.Size;
        _nx = (int)(size.X / cell) + 2;
        _ny = (int)(size.Y / cell) + 2;
        _nz = (int)(size.Z / cell) + 2;
        _open = new byte[_nx * _ny * _nz];
        _distance = new ushort[_open.Length];
        _stamp = new int[_open.Length];
    }

    public bool Built { get; private set; }

    /// <summary>The point the field leads to.</summary>
    public Vector3 Target { get; private set; }

    static (int, int, int)[] BuildSteps()
    {
        var list = new List<(int, int, int)>();
        for (int z = -1; z <= 1; z++)
        for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
            if (x != 0 || y != 0 || z != 0) list.Add((x, y, z));
        return list.ToArray();
    }

    int Index(int x, int y, int z) => (z * _ny + y) * _nx + x;

    bool InRange(int x, int y, int z) => x >= 0 && y >= 0 && z >= 0 && x < _nx && y < _ny && z < _nz;

    (int, int, int) CellOf(Vector3 p) => (
        Math.Clamp((int)MathF.Floor((p.X - _sdf.Origin.X) / _cell), 0, _nx - 1),
        Math.Clamp((int)MathF.Floor((p.Y - _sdf.Origin.Y) / _cell), 0, _ny - 1),
        Math.Clamp((int)MathF.Floor((p.Z - _sdf.Origin.Z) / _cell), 0, _nz - 1));

    Vector3 CenterOf(int x, int y, int z) => _sdf.Origin + new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * _cell;

    bool IsOpen(int x, int y, int z)
    {
        int i = Index(x, y, z);
        if (_open[i] == 0) _open[i] = _sdf.Sample(CenterOf(x, y, z)) >= _clearance ? (byte)1 : (byte)2;
        return _open[i] == 1;
    }

    /// <summary>Forget what is known about cells near a crater.</summary>
    public void Invalidate(Vector3 center, float radius)
    {
        var (x0, y0, z0) = CellOf(center - new Vector3(radius + _cell));
        var (x1, y1, z1) = CellOf(center + new Vector3(radius + _cell));
        for (int z = z0; z <= z1; z++)
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            _open[Index(x, y, z)] = 0;
        Built = false;
    }

    /// <summary>Floods outward from the target, up to <paramref name="radius"/> metres away.</summary>
    public void Build(Vector3 target, float radius)
    {
        _version++;
        Target = target;
        var (tx, ty, tz) = CellOf(target);
        int r = (int)MathF.Ceiling(radius / _cell);
        var queue = new Queue<int>();

        void Seed(int x, int y, int z)
        {
            int i = Index(x, y, z);
            if (_stamp[i] == _version) return;
            _stamp[i] = _version;
            _distance[i] = 0;
            queue.Enqueue(i);
        }

        // The target itself may sit in a cell that is mostly rock: start from whatever open cells touch it.
        if (IsOpen(tx, ty, tz)) Seed(tx, ty, tz);
        foreach (var (dx, dy, dz) in Steps)
        {
            int x = tx + dx, y = ty + dy, z = tz + dz;
            if (InRange(x, y, z) && IsOpen(x, y, z)) Seed(x, y, z);
        }

        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            int x = i % _nx, y = i / _nx % _ny, z = i / (_nx * _ny);
            ushort next = (ushort)(_distance[i] + 1);
            foreach (var (dx, dy, dz) in Steps)
            {
                int nx = x + dx, ny = y + dy, nz = z + dz;
                if (!InRange(nx, ny, nz) || Math.Abs(nx - tx) > r || Math.Abs(ny - ty) > r || Math.Abs(nz - tz) > r) continue;
                int j = Index(nx, ny, nz);
                if (_stamp[j] == _version || !IsOpen(nx, ny, nz)) continue;
                _stamp[j] = _version;
                _distance[j] = next;
                queue.Enqueue(j);
            }
        }
        Built = true;
    }

    /// <summary>True when the position's cell, or a neighbour, was reached by the flood.</summary>
    public bool Reaches(Vector3 position) => BestNeighbour(position, out _, out _);

    /// <summary>Unit vector toward the target along the field, or zero when the position is outside it (or already there).</summary>
    public Vector3 Direction(Vector3 position)
    {
        if (!Built || !BestNeighbour(position, out var best, out ushort bestDistance)) return Vector3.Zero;
        var (cx, cy, cz) = CellOf(position);
        int here = Index(cx, cy, cz);
        // Already in the closest cell: nothing to steer around.
        if (_stamp[here] == _version && _distance[here] <= bestDistance) return Vector3.Zero;
        Vector3 to = best - position;
        float len = to.Length();
        return len > 1e-4f ? to / len : Vector3.Zero;
    }

    bool BestNeighbour(Vector3 position, out Vector3 center, out ushort distance)
    {
        center = default;
        distance = ushort.MaxValue;
        if (!Built) return false;
        var (cx, cy, cz) = CellOf(position);
        bool found = false;
        foreach (var (dx, dy, dz) in Steps)
        {
            int x = cx + dx, y = cy + dy, z = cz + dz;
            if (!InRange(x, y, z)) continue;
            int i = Index(x, y, z);
            if (_stamp[i] != _version || _distance[i] >= distance) continue;
            distance = _distance[i];
            center = CenterOf(x, y, z);
            found = true;
        }
        int own = Index(cx, cy, cz);
        if (_stamp[own] == _version && _distance[own] <= distance)
        {
            distance = _distance[own];
            center = CenterOf(cx, cy, cz);
            found = true;
        }
        return found;
    }
}
