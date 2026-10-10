using System;
using System.Threading.Tasks;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The corruption field as a texture (docs/CORRUPTION.md): R its ink density, G the gold of corruption dying, one texel
/// per half-metre cell. Every view of the corruption reads it: the floor's ink (reef_surface), the blades, the maps.
/// The gold is worked out here, from how fast each cell's density falls: cleansing always reads as light spreading.
/// It uploads at most 30 times a second, and only while something changes.
/// </summary>
public sealed class CorruptionTexture
{
    const int N = CorruptionField.N;
    const float Interval = 1f / 30f, BurnFade = 0.85f, BurnGain = 4f;

    readonly Image _image;
    readonly byte[] _bytes = new byte[N * N * 2];
    readonly float[] _prev = new float[N * N];
    readonly float[] _burn = new float[N * N];
    int _version = -1;
    float _clock;
    bool _burning;

    public ImageTexture Texture { get; }

    public CorruptionTexture(CorruptionField field)
    {
        Array.Copy(field.Density, _prev, _prev.Length);
        Fill(field, 0f);
        _image = Image.CreateFromData(N, N, false, Image.Format.Rg8, _bytes);
        Texture = ImageTexture.CreateFromImage(_image);
        _version = field.Version;
    }

    public void Update(CorruptionField field, float dt)
    {
        _clock += dt;
        if (_clock < Interval || (field.Version == _version && !_burning)) return;
        Fill(field, _clock);
        _clock = 0f;
        _version = field.Version;
        _image.SetData(N, N, false, Image.Format.Rg8, _bytes);
        Texture.Update(_image);
    }

    void Fill(CorruptionField field, float dt)
    {
        var d = field.Density;
        bool burning = false;
        float fade = BurnFade * dt;
        for (int i = 0; i < d.Length; i++)
        {
            float v = d[i];
            float drop = _prev[i] - v;
            float b = _burn[i];
            b = drop > 0f ? MathF.Min(1f, b + drop * BurnGain) : MathF.Max(0f, b - fade);
            _burn[i] = b;
            _prev[i] = v;
            if (b > 0f) burning = true;
            _bytes[i * 2] = (byte)(MathF.Min(v, 1f) * 255f);
            _bytes[i * 2 + 1] = (byte)(b * 255f);
        }
        _burning = burning;
    }
}

/// <summary>
/// The corruption's outgrowth: swaying ink blades over the floor (corruption_blades.gdshader), one MultiMesh per chunk
/// of the level so only what the camera sees is drawn. Every blade is placed once, off the main thread, from the
/// field's starting density; the shader grows, shrivels, burns and dissolves each one from the live texture.
/// </summary>
public partial class CorruptionView : Node3D
{
    const int Chunks = 8;
    const float ChunkSize = LevelMap.Size / (float)Chunks;
    /// <summary>One MultiMesh instance: a 3×4 transform and a custom colour (seed, u, v, 0).</summary>
    const int Stride = 16;

    /// <summary>Blades laid out for a level, chunk by chunk: the instance buffer and its count.</summary>
    public sealed record Layout(float[][] Buffers, int[] Counts);

    static ArrayMesh? _blade;
    ShaderMaterial _material = null!;

    public override void _Ready()
    {
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/corruption_blades.gdshader") };
    }

    /// <summary>Lays out a level's blades (pure work on data: run it off the main thread).</summary>
    public static Task<Layout> Plan(LevelMap map, CorruptionField field) => Task.Run(() => Build(map, field));

    /// <summary>Shows a level's blades reading this texture (null clears them).</summary>
    public void Show(Layout? layout, Texture2D? texture)
    {
        foreach (var child in GetChildren()) child.QueueFree();
        if (layout is null || texture is null) return;
        _material.SetShaderParameter("corruption_map", texture);
        var blade = _blade ??= BladeMesh();
        for (int c = 0; c < layout.Buffers.Length; c++)
        {
            if (layout.Counts[c] == 0) continue;
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseCustomData = true,
                Mesh = blade,
                InstanceCount = layout.Counts[c],
            };
            mm.Buffer = layout.Buffers[c];
            AddChild(new MultiMeshInstance3D
            {
                Multimesh = mm,
                MaterialOverride = _material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                // Room for the sway and the recoil.
                ExtraCullMargin = 1.5f,
            });
        }
    }

    /// <summary>Her bell, for the blades' recoil from her light.</summary>
    public void SetHer(Vector3 at) => _material.SetShaderParameter("her_pos", at);

    public static Layout Build(LevelMap map, CorruptionField field)
    {
        var lists = new System.Collections.Generic.List<float>[Chunks * Chunks];
        for (int i = 0; i < lists.Length; i++) lists[i] = new System.Collections.Generic.List<float>(4096);
        uint state = (uint)(map.Seed ^ (map.Seed >> 32)) | 1u;
        float Rand()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0xFFFFFF) / 16777216f;
        }
        const int n = CorruptionField.N;
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float start = field.Initial[CorruptionField.Index(x, y)];
            if (start <= 0.05f) continue;
            // Thicker ground, more blades (one or two a cell: a dense sward at half-metre spacing).
            int blades = 1 + (Rand() < start * 0.75f ? 1 : 0);
            for (int b = 0; b < blades; b++)
            {
                var p = new System.Numerics.Vector2((x + Rand()) * CorruptionTuning.Cell, (y + Rand()) * CorruptionTuning.Cell);
                float floor = map.HeightAt(p);
                if (floor > LevelMap.BlockHeight) continue;
                float yaw = Rand() * MathF.Tau, c = MathF.Cos(yaw), s = MathF.Sin(yaw);
                float height = (0.32f + 0.6f * Rand()) * (0.6f + 0.55f * start);
                float width = 0.8f + 0.6f * Rand();
                int chunk = Math.Clamp((int)(p.Y / ChunkSize), 0, Chunks - 1) * Chunks + Math.Clamp((int)(p.X / ChunkSize), 0, Chunks - 1);
                var list = lists[chunk];
                // Rows of the 3×4 transform: a yaw, the blade's width (x, z) and height (y), at the floor.
                list.Add(c * width); list.Add(0f); list.Add(s * width); list.Add(p.X);
                list.Add(0f); list.Add(height); list.Add(0f); list.Add(floor);
                list.Add(-s * width); list.Add(0f); list.Add(c * width); list.Add(p.Y);
                list.Add(Rand()); list.Add(p.X / LevelMap.Size); list.Add(p.Y / LevelMap.Size); list.Add(0f);
            }
        }
        var buffers = new float[lists.Length][];
        var counts = new int[lists.Length];
        for (int i = 0; i < lists.Length; i++)
        {
            buffers[i] = lists[i].ToArray();
            counts[i] = buffers[i].Length / Stride;
        }
        return new Layout(buffers, counts);
    }

    /// <summary>A blade: a tapering strip in four segments, 7 cm wide at the root, UV.y its height (0 root → 1 tip).</summary>
    static ArrayMesh BladeMesh()
    {
        float[] ts = { 0f, 0.3f, 0.56f, 0.8f, 1f };
        float[] half = { 0.035f, 0.03f, 0.022f, 0.012f, 0f };
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < ts.Length - 1; i++)
        {
            Vector3 a0 = new(-half[i], ts[i], 0f), a1 = new(half[i], ts[i], 0f), b0 = new(-half[i + 1], ts[i + 1], 0f), b1 = new(half[i + 1], ts[i + 1], 0f);
            void V(Vector3 v, float u, float t)
            {
                st.SetNormal(new Vector3(0f, 0.25f, 1f).Normalized());
                st.SetUV(new Vector2(u, t));
                st.AddVertex(v);
            }
            V(a0, 0f, ts[i]); V(a1, 1f, ts[i]); V(b1, 1f, ts[i + 1]);
            V(a0, 0f, ts[i]); V(b1, 1f, ts[i + 1]); V(b0, 0f, ts[i + 1]);
        }
        return st.Commit();
    }
}
