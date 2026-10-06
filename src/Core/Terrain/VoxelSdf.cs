using System;
using System.Numerics;

namespace OctoShoots.Core.Terrain;

/// <summary>
/// Signed distance field sampled on a regular grid. Positive = open water, negative = rock.
/// Points outside the grid clamp to the border.
/// Stored as 32³ chunks; a chunk whose samples are all equal (open sea, deep rock) keeps one value
/// instead of an array, so a big open level costs memory only where there is a surface.
/// </summary>
public sealed class VoxelSdf
{
    public const int ChunkSize = 32;
    const int Shift = 5;
    const int Mask = ChunkSize - 1;
    const int ChunkVolume = ChunkSize * ChunkSize * ChunkSize;

    readonly float[]?[] _chunks;
    readonly float[] _uniform;

    public VoxelSdf(int nx, int ny, int nz, float cell, Vector3 origin, float fill = 0f)
    {
        Nx = nx;
        Ny = ny;
        Nz = nz;
        Cell = cell;
        Origin = origin;
        ChunksX = (nx + Mask) >> Shift;
        ChunksY = (ny + Mask) >> Shift;
        ChunksZ = (nz + Mask) >> Shift;
        _chunks = new float[]?[ChunksX * ChunksY * ChunksZ];
        _uniform = new float[_chunks.Length];
        Array.Fill(_uniform, fill);
    }

    public int Nx { get; }
    public int Ny { get; }
    public int Nz { get; }
    public float Cell { get; }
    public Vector3 Origin { get; }
    public int ChunksX { get; }
    public int ChunksY { get; }
    public int ChunksZ { get; }
    public Vector3 Size => new Vector3(Nx - 1, Ny - 1, Nz - 1) * Cell;

    /// <summary>How many chunks hold real arrays (the rest are uniform).</summary>
    public int AllocatedChunks
    {
        get
        {
            int n = 0;
            foreach (var c in _chunks) if (c is not null) n++;
            return n;
        }
    }

    public int ChunkIndex(int cx, int cy, int cz) => (cz * ChunksY + cy) * ChunksX + cx;

    public float this[int x, int y, int z]
    {
        get
        {
            int c = ChunkIndex(x >> Shift, y >> Shift, z >> Shift);
            var data = _chunks[c];
            return data is null ? _uniform[c] : data[Local(x, y, z)];
        }
        set
        {
            int c = ChunkIndex(x >> Shift, y >> Shift, z >> Shift);
            var data = _chunks[c];
            if (data is null)
            {
                if (value == _uniform[c]) return;
                data = Allocate(c);
            }
            data[Local(x, y, z)] = value;
        }
    }

    static int Local(int x, int y, int z) => ((z & Mask) << (2 * Shift)) | ((y & Mask) << Shift) | (x & Mask);

    float[] Allocate(int c)
    {
        var data = new float[ChunkVolume];
        Array.Fill(data, _uniform[c]);
        _chunks[c] = data;
        return data;
    }

    /// <summary>A global point key (used for visited sets and vertex caches), not a storage index.</summary>
    public int Index(int x, int y, int z) => (z * Ny + y) * Nx + x;

    public Vector3 PointPosition(int x, int y, int z) => Origin + new Vector3(x, y, z) * Cell;

    /// <summary>True when the chunk holds one value everywhere.</summary>
    public bool IsUniform(int cx, int cy, int cz, out float value)
    {
        int c = ChunkIndex(cx, cy, cz);
        value = _uniform[c];
        return _chunks[c] is null;
    }

    /// <summary>Makes a whole chunk one value (generator fast path).</summary>
    public void SetUniform(int cx, int cy, int cz, float value)
    {
        int c = ChunkIndex(cx, cy, cz);
        _chunks[c] = null;
        _uniform[c] = value;
    }

    /// <summary>Stores a chunk's samples (x fastest, then y, then z); collapses to uniform when all are equal.</summary>
    public void SetChunk(int cx, int cy, int cz, float[] data)
    {
        int c = ChunkIndex(cx, cy, cz);
        float first = data[0];
        bool same = true;
        for (int i = 1; i < data.Length && same; i++) same = data[i] == first;
        if (same)
        {
            _chunks[c] = null;
            _uniform[c] = first;
        }
        else
        {
            _chunks[c] = data;
        }
    }

    /// <summary>Trilinear sample.</summary>
    public float Sample(Vector3 p)
    {
        float fx = Math.Clamp((p.X - Origin.X) / Cell, 0f, Nx - 1.0001f);
        float fy = Math.Clamp((p.Y - Origin.Y) / Cell, 0f, Ny - 1.0001f);
        float fz = Math.Clamp((p.Z - Origin.Z) / Cell, 0f, Nz - 1.0001f);
        int x = (int)fx, y = (int)fy, z = (int)fz;
        float tx = fx - x, ty = fy - y, tz = fz - z;

        float v000, v100, v010, v110, v001, v101, v011, v111;
        if ((x & Mask) < Mask && (y & Mask) < Mask && (z & Mask) < Mask)
        {
            // All eight corners in one chunk: one lookup.
            int c = ChunkIndex(x >> Shift, y >> Shift, z >> Shift);
            var data = _chunks[c];
            if (data is null) return _uniform[c];
            int i = Local(x, y, z);
            const int sy = ChunkSize, sz = ChunkSize * ChunkSize;
            v000 = data[i];
            v100 = data[i + 1];
            v010 = data[i + sy];
            v110 = data[i + sy + 1];
            v001 = data[i + sz];
            v101 = data[i + sz + 1];
            v011 = data[i + sy + sz];
            v111 = data[i + sy + sz + 1];
        }
        else
        {
            v000 = this[x, y, z];
            v100 = this[x + 1, y, z];
            v010 = this[x, y + 1, z];
            v110 = this[x + 1, y + 1, z];
            v001 = this[x, y, z + 1];
            v101 = this[x + 1, y, z + 1];
            v011 = this[x, y + 1, z + 1];
            v111 = this[x + 1, y + 1, z + 1];
        }
        float c00 = MathUtil.Lerp(v000, v100, tx);
        float c10 = MathUtil.Lerp(v010, v110, tx);
        float c01 = MathUtil.Lerp(v001, v101, tx);
        float c11 = MathUtil.Lerp(v011, v111, tx);
        return MathUtil.Lerp(MathUtil.Lerp(c00, c10, ty), MathUtil.Lerp(c01, c11, ty), tz);
    }

    /// <summary>Unit gradient, pointing toward open water.</summary>
    public Vector3 Gradient(Vector3 p)
    {
        float h = Cell * 0.75f;
        var g = new Vector3(
            Sample(p + new Vector3(h, 0, 0)) - Sample(p - new Vector3(h, 0, 0)),
            Sample(p + new Vector3(0, h, 0)) - Sample(p - new Vector3(0, h, 0)),
            Sample(p + new Vector3(0, 0, h)) - Sample(p - new Vector3(0, 0, h)));
        return MathUtil.SafeNormalize(g, Vector3.UnitY);
    }

    /// <summary>Sphere-traces a ray (inflated by radius). Returns true and the distance on a hit.</summary>
    public bool Raycast(Vector3 origin, Vector3 dir, float maxDist, out float hitDist, float radius = 0f)
    {
        float t = 0f;
        for (int i = 0; i < 256 && t < maxDist; i++)
        {
            float d = Sample(origin + dir * t) - radius;
            if (d < 0.02f)
            {
                hitDist = t;
                return true;
            }
            t += MathF.Max(d * 0.7f, 0.03f);
        }
        hitDist = maxDist;
        return false;
    }

    public VoxelSdf Clone()
    {
        var copy = new VoxelSdf(Nx, Ny, Nz, Cell, Origin);
        Array.Copy(_uniform, copy._uniform, _uniform.Length);
        for (int i = 0; i < _chunks.Length; i++)
            if (_chunks[i] is { } data) copy._chunks[i] = (float[])data.Clone();
        return copy;
    }

    /// <summary>Fills a box of grid points (inclusive-exclusive) with one value.</summary>
    public void FillRegion(int x0, int y0, int z0, int x1, int y1, int z1, float value)
    {
        for (int z = z0; z < z1; z++)
        for (int y = y0; y < y1; y++)
        for (int x = x0; x < x1; x++)
            this[x, y, z] = value;
    }

    /// <summary>
    /// Blows a spherical crater: open water wins over rock inside the sphere (§6.3). Grid points within
    /// <paramref name="shellCells"/> of the border never change. Returns the touched point bounds
    /// (inclusive), or null when nothing changed.
    /// </summary>
    public (int X0, int Y0, int Z0, int X1, int Y1, int Z1)? CarveSphere(Vector3 center, float radius, int shellCells)
    {
        Vector3 lo = (center - Origin - new Vector3(radius)) / Cell;
        Vector3 hi = (center - Origin + new Vector3(radius)) / Cell;
        int x0 = Math.Max(shellCells, (int)MathF.Floor(lo.X) - 1), x1 = Math.Min(Nx - 1 - shellCells, (int)MathF.Ceiling(hi.X) + 1);
        int y0 = Math.Max(shellCells, (int)MathF.Floor(lo.Y) - 1), y1 = Math.Min(Ny - 1 - shellCells, (int)MathF.Ceiling(hi.Y) + 1);
        int z0 = Math.Max(shellCells, (int)MathF.Floor(lo.Z) - 1), z1 = Math.Min(Nz - 1 - shellCells, (int)MathF.Ceiling(hi.Z) + 1);
        bool changed = false;
        for (int z = z0; z <= z1; z++)
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            float open = radius - (PointPosition(x, y, z) - center).Length();
            if (open <= this[x, y, z]) continue;
            this[x, y, z] = open;
            changed = true;
        }
        return changed ? (x0, y0, z0, x1, y1, z1) : null;
    }

    /// <summary>Hash of the stored samples, for determinism tests (a uniform chunk hashes differently from an equal array).</summary>
    public ulong ContentHash()
    {
        ulong h = 0xcbf29ce484222325UL;
        for (int c = 0; c < _chunks.Length; c++)
        {
            if (_chunks[c] is { } data)
            {
                foreach (float f in data) Mix(f);
            }
            else
            {
                Mix(_uniform[c]);
                Mix(-1f);
            }
        }
        return h;

        void Mix(float f)
        {
            h ^= (uint)BitConverter.SingleToInt32Bits(f);
            h *= 0x100000001b3UL;
        }
    }

    public bool LineOfSight(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float len = d.Length();
        if (len < 1e-4f) return true;
        return !Raycast(from, d / len, len, out _);
    }
}
