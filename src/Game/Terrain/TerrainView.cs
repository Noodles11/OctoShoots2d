using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;
using OctoShoots.Core.Terrain;

namespace OctoShoots.Game.Terrain;

/// <summary>
/// Shows the reef as one mesh per 32³ chunk. Chunks are meshed on worker threads; after a crater
/// only the chunks it touches are remeshed (§6.3).
/// </summary>
public partial class TerrainView : Node3D
{
    const int Size = SurfaceNets.ChunkSize;

    ShaderMaterial _material = null!;
    VoxelSdf? _sdf;
    MeshInstance3D?[] _chunks = Array.Empty<MeshInstance3D?>();
    int _cx, _cy, _cz;

    public override void _Ready()
    {
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/reef_terrain.gdshader") };
    }

    /// <summary>
    /// Tells the rock shader where the seabed is (sand settles near it) and where the surface is (caustics fade with depth).
    /// Without a heightfield (the grey-box cave) any flat floor counts as seabed.
    /// </summary>
    public void SetSeabed(float[]? heights, int width, int length, float surfaceY)
    {
        if (heights is null)
        {
            _material.SetShaderParameter("has_seabed", 0f);
            _material.SetShaderParameter("surface_y", float.IsFinite(surfaceY) ? surfaceY : 30f);
            return;
        }
        var data = new byte[heights.Length * 4];
        Buffer.BlockCopy(heights, 0, data, 0, data.Length);
        var image = Image.CreateFromData(width + 1, length + 1, false, Image.Format.Rf, data);
        _material.SetShaderParameter("seabed", ImageTexture.CreateFromImage(image));
        _material.SetShaderParameter("reef_size", new Vector2(width, length));
        _material.SetShaderParameter("has_seabed", 1f);
        _material.SetShaderParameter("surface_y", surfaceY);
    }

    /// <summary>Meshes every chunk of an SDF. Safe to call from a worker thread (pure Core).</summary>
    public static MeshData[] MeshAll(VoxelSdf sdf)
    {
        int cx = (sdf.Nx + Size - 1) / Size, cy = (sdf.Ny + Size - 1) / Size, cz = (sdf.Nz + Size - 1) / Size;
        var chunks = new MeshData[cx * cy * cz];
        Parallel.For(0, chunks.Length, i =>
        {
            int x = i % cx, y = i / cx % cy, z = i / (cx * cy);
            chunks[i] = SurfaceNets.BuildChunk(sdf, x * Size, y * Size, z * Size, (x + 1) * Size, (y + 1) * Size, (z + 1) * Size);
        });
        return chunks;
    }

    /// <summary>Replaces the whole terrain with prebuilt chunk meshes.</summary>
    public void Show(VoxelSdf sdf, MeshData[] chunks)
    {
        foreach (var child in GetChildren()) child.QueueFree();
        _sdf = sdf;
        _cx = (sdf.Nx + Size - 1) / Size;
        _cy = (sdf.Ny + Size - 1) / Size;
        _cz = (sdf.Nz + Size - 1) / Size;
        _chunks = new MeshInstance3D?[chunks.Length];
        int triangles = 0;
        for (int i = 0; i < chunks.Length; i++)
        {
            triangles += chunks[i].Indices.Count / 3;
            SetChunk(i, chunks[i]);
        }
        GD.Print($"Terrain: {triangles} triangles in {chunks.Length} chunks");
    }

    /// <summary>Remeshes the chunks a crater can have changed.</summary>
    public void Remesh(System.Numerics.Vector3 center, float radius)
    {
        if (_sdf is null) return;
        var watch = Stopwatch.StartNew();
        // A changed grid point alters quads up to two points away (edge signs and cell vertices).
        int Lo(float v, float origin) => (int)MathF.Floor((v - radius - origin) / _sdf.Cell) - 2;
        int Hi(float v, float origin) => (int)MathF.Ceiling((v + radius - origin) / _sdf.Cell) + 2;
        int x0 = Math.Max(0, Lo(center.X, _sdf.Origin.X) / Size), x1 = Math.Min(_cx - 1, Hi(center.X, _sdf.Origin.X) / Size);
        int y0 = Math.Max(0, Lo(center.Y, _sdf.Origin.Y) / Size), y1 = Math.Min(_cy - 1, Hi(center.Y, _sdf.Origin.Y) / Size);
        int z0 = Math.Max(0, Lo(center.Z, _sdf.Origin.Z) / Size), z1 = Math.Min(_cz - 1, Hi(center.Z, _sdf.Origin.Z) / Size);

        var dirty = new List<int>();
        for (int z = z0; z <= z1; z++)
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            dirty.Add((z * _cy + y) * _cx + x);

        var meshes = new MeshData[dirty.Count];
        var sdf = _sdf;
        Parallel.For(0, dirty.Count, k =>
        {
            int i = dirty[k];
            int x = i % _cx, y = i / _cx % _cy, z = i / (_cx * _cy);
            meshes[k] = SurfaceNets.BuildChunk(sdf, x * Size, y * Size, z * Size, (x + 1) * Size, (y + 1) * Size, (z + 1) * Size);
        });
        for (int k = 0; k < dirty.Count; k++) SetChunk(dirty[k], meshes[k]);
        if (watch.ElapsedMilliseconds > 8) GD.Print($"Remeshed {dirty.Count} chunks in {watch.ElapsedMilliseconds} ms");
    }

    void SetChunk(int index, MeshData data)
    {
        var existing = _chunks[index];
        if (data.Indices.Count == 0)
        {
            existing?.QueueFree();
            _chunks[index] = null;
            return;
        }
        var mesh = ToMesh(data);
        if (existing is null)
        {
            existing = new MeshInstance3D { MaterialOverride = _material };
            AddChild(existing);
            _chunks[index] = existing;
        }
        existing.Mesh = mesh;
    }

    static ArrayMesh ToMesh(MeshData data)
    {
        var verts = new Vector3[data.Positions.Count];
        var normals = new Vector3[data.Normals.Count];
        for (int i = 0; i < verts.Length; i++)
        {
            var p = data.Positions[i];
            var n = data.Normals[i];
            verts[i] = new Vector3(p.X, p.Y, p.Z);
            normals[i] = new Vector3(n.X, n.Y, n.Z);
        }

        // Core winds counter-clockwise seen from the water; Godot's front faces are clockwise.
        var indices = new int[data.Indices.Count];
        for (int i = 0; i < indices.Length; i += 3)
        {
            indices[i] = data.Indices[i];
            indices[i + 1] = data.Indices[i + 2];
            indices[i + 2] = data.Indices[i + 1];
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}
