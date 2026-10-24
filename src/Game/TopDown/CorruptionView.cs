using System;
using System.Threading.Tasks;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The corruption field as a texture (docs/CORRUPTION.md): R its ink density, G the gold of corruption dying, one texel
/// per half-metre cell. Every view of the corruption reads it: the floor's gold where it dies (reef_surface), the shards, the maps.
/// The gold is worked out here, from how fast each cell's density falls: cleansing always reads as light spreading. Ink a
/// creature drinks (CorruptionField.Drunk) is not cleansed: it just goes, leaving the ground dark and empty.
/// It uploads at most 30 times a second, and only while something changes.
/// </summary>
public sealed class CorruptionTexture
{
    const int N = CorruptionField.N;
    /// <summary>How often it uploads; how fast a cleansed cell's gold (and the last drops lit with it) fades, and how fast it lights.</summary>
    const float Interval = 1f / 30f, BurnFade = 2.55f, BurnGain = 4f;

    readonly Image _image;
    readonly byte[] _bytes = new byte[N * N * 2];
    readonly float[] _prev = new float[N * N];
    readonly float[] _burn = new float[N * N];
    readonly float[] _drunk = new float[N * N];
    int _version = -1;
    float _clock;
    bool _burning;

    public ImageTexture Texture { get; }

    /// <summary>The cells cleansed since the last upload, and by how much (InkVapour lifts ink off them).</summary>
    public readonly System.Collections.Generic.List<(int Cell, float Drop)> Drops = new();

    public CorruptionTexture(CorruptionField field)
    {
        Array.Copy(field.Density, _prev, _prev.Length);
        Array.Copy(field.Drunk, _drunk, _drunk.Length);
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
        Drops.Clear();
        float fade = BurnFade * dt;
        for (int i = 0; i < d.Length; i++)
        {
            float v = d[i];
            // What a creature drank is gone without light.
            float drank = field.Drunk[i] - _drunk[i];
            _drunk[i] = field.Drunk[i];
            float drop = _prev[i] - v - drank;
            float b = _burn[i];
            if (drop > 0.02f && dt > 0f) Drops.Add((i, drop));
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
/// The corruption's mark on the reef: ink dripping upward out of the ground, as fine as plankton
/// (corruption_shards.gdshader). From its own spot on corrupted ground each drop wells up, stretches up on a neck, snaps
/// free and rises away, then another wells up, on its own loop; one MultiMesh per chunk of the level so only what the
/// camera sees is drawn. Every spot is placed once, off the main thread, from the field's starting density, never on a
/// dive's gateway (the shaft); the shader stops a spot's drops (the last lit gold) as its ground is cleansed. A level's drift
/// fades in when it is shown and fades out with the dive (<see cref="Fade"/>), never switched in one frame.
/// </summary>
public partial class CorruptionView : Node3D
{
    const int Chunks = 8;
    const float ChunkSize = LevelMap.Size / (float)Chunks;
    /// <summary>One MultiMesh instance: a 3×4 transform and a custom colour (seed, u, v, 0).</summary>
    const int Stride = 16;
    /// <summary>A drop's radius (metres): plankton-fine, a speck at play distance.</summary>
    const float RadiusMin = 0.022f, RadiusMax = 0.04f;
    /// <summary>How high it rises before it is gone.</summary>
    const float RiseMin = 0.6f, RiseMax = 1.5f;
    /// <summary>Drops per half-metre cell: one on thin corruption, up to this many more where it is thick.</summary>
    const int MorePerCell = 4;
    /// <summary>The drift keeps this far (metres) off a dive's gateway, and fades in over AppearSeconds when shown.</summary>
    const float GatewayClear = 2.5f, AppearSeconds = 1.2f;

    /// <summary>Shards laid out for a level, chunk by chunk: the instance buffer and its count.</summary>
    public sealed record Layout(float[][] Buffers, int[] Counts);

    static Mesh? _spore;
    ShaderMaterial _material = null!;
    float _fade = 1f, _appear = 1f;

    /// <summary>The dive: 1 → 0 as the level is let go (its drift fades with the rest of its life).</summary>
    public void Fade(float k)
    {
        _fade = Mathf.Clamp(k, 0f, 1f);
        ApplyFade();
    }

    void ApplyFade() => _material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("fade"), _fade * Mathf.SmoothStep(0f, 1f, _appear));

    public override void _Process(double delta)
    {
        if (_appear >= 1f) return;
        _appear = Mathf.Min(1f, _appear + (float)delta / AppearSeconds);
        ApplyFade();
    }

    public override void _Ready()
    {
        // After the water pass (RenderPriority −100), so the sea does not tint them.
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/corruption_shards.gdshader"), RenderPriority = 0 };
    }

    /// <summary>Lays out a level's shards (pure work on data: run it off the main thread).</summary>
    public static Task<Layout> Plan(LevelMap map, CorruptionField field) => Task.Run(() => Build(map, field));

    /// <summary>Shows a level's shards reading this texture (null clears them).</summary>
    public void Show(Layout? layout, Texture2D? texture)
    {
        foreach (var child in GetChildren()) child.QueueFree();
        _fade = 1f;
        _appear = 0f;
        ApplyFade();
        if (layout is null || texture is null) return;
        _material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("corruption_map"), texture);
        // A quad: the shader turns it to the camera and shapes the drop on it.
        var shard = _spore ??= new QuadMesh { Size = Vector2.One };
        for (int c = 0; c < layout.Buffers.Length; c++)
        {
            if (layout.Counts[c] == 0) continue;
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseCustomData = true,
                Mesh = shard,
                InstanceCount = layout.Counts[c],
            };
            mm.Buffer = layout.Buffers[c];
            AddChild(new MultiMeshInstance3D
            {
                Multimesh = mm,
                MaterialOverride = _material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                // Room for the rise and the sway.
                ExtraCullMargin = RiseMax + 0.6f,
            });
        }
    }

    public static Layout Build(LevelMap map, CorruptionField field)
    {
        var lists = new System.Collections.Generic.List<float>[Chunks * Chunks];
        for (int i = 0; i < lists.Length; i++) lists[i] = new System.Collections.Generic.List<float>(2048);
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
            // Shards hang wherever corruption is or may spread (a Blotter's trail brings them where there were none);
            // the shader hides them while their ground is clean.
            float start = field.Initial[CorruptionField.Index(x, y)];
            float guard = field.Guard[CorruptionField.Index(x, y)];
            if (start <= 0.05f && guard <= 0.05f) continue;
            if (start <= 0.05f) start = 0.6f * guard;
            // Thicker ground, more drops.
            int drops = 1 + (int)(start * MorePerCell + Rand());
            for (int d = 0; d < drops; d++)
            {
                var p = new System.Numerics.Vector2((x + Rand()) * CorruptionTuning.Cell, (y + Rand()) * CorruptionTuning.Cell);
                float floor = map.HeightAt(p);
                if (floor > LevelMap.BlockHeight || map.Shaft.Contains(p, GatewayClear)) continue;
                float radius = RadiusMin + (RadiusMax - RadiusMin) * Rand();
                float rise = RiseMin + (RiseMax - RiseMin) * Rand();
                // Its scale is the drop's radius (the shader reads it back).
                var basis = Basis.Identity.Scaled(Vector3.One * radius);
                int chunk = Math.Clamp((int)(p.Y / ChunkSize), 0, Chunks - 1) * Chunks + Math.Clamp((int)(p.X / ChunkSize), 0, Chunks - 1);
                var list = lists[chunk];
                // Rows of the 3×4 transform (at the floor, where it emerges), then the custom colour (seed, u, v, rise).
                list.Add(basis.Row0.X); list.Add(basis.Row0.Y); list.Add(basis.Row0.Z); list.Add(p.X);
                list.Add(basis.Row1.X); list.Add(basis.Row1.Y); list.Add(basis.Row1.Z); list.Add(floor);
                list.Add(basis.Row2.X); list.Add(basis.Row2.Y); list.Add(basis.Row2.Z); list.Add(p.Y);
                list.Add(Rand()); list.Add(p.X / LevelMap.Size); list.Add(p.Y / LevelMap.Size); list.Add(rise);
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
}
