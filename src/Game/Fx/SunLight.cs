using System;
using System.Threading.Tasks;
using Godot;
using OctoShoots.Core.Terrain;

namespace OctoShoots.Game.Fx;

/// <summary>
/// The sun as seen from under the water: one direction for the light, its shadows, the caustics on the ground and the
/// god rays in the water (sunlight.gdshaderinc). Owns the sun's directional light and a "sun shadow map": for every
/// point of the sea surface, the height at which its ray of light first hits rock. Shaders use it to keep caustics and
/// shafts out of the shade of overhangs, arches and caves. It is rebuilt off the main thread after craters.
/// </summary>
public partial class SunLight : Node3D
{
    /// <summary>Direction the light travels: steep, a little slanted, so shafts read as diagonal beams.</summary>
    public static readonly Vector3 Direction = new Vector3(0.42f, -0.86f, 0.28f).Normalized();

    const float NoHit = -1000f;

    /// <summary>Rock this close to the reef's sides is the boundary rim.</summary>
    const float RimMargin = 28f;

    DirectionalLight3D _light = null!;
    VoxelSdf? _sdf;
    float _surfaceY;
    int _width, _length;
    Task<float[]>? _rebuild;
    bool _dirty;

    public override void _Ready()
    {
        _light = new DirectionalLight3D
        {
            LightColor = new Color(0.7f, 0.93f, 1f),
            LightEnergy = 0.45f,
            ShadowEnabled = true,
            ShadowBlur = 2.5f,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel2Splits,
            DirectionalShadowMaxDistance = 45f,
            LightAngularDistance = 2.5f,
        };
        _light.Transform = new Transform3D(Basis.LookingAt(Direction, Vector3.Forward), Vector3.Zero);
        AddChild(_light);
        RenderingServer.GlobalShaderParameterSet("sun_dir", Direction);
    }

    /// <summary>A reef with a sea surface: build the shadow map now (on the caller's thread) and use it.</summary>
    public void Show(VoxelSdf sdf, float surfaceY, float[]? map)
    {
        _sdf = sdf;
        _surfaceY = surfaceY;
        _width = (int)MathF.Ceiling(sdf.Size.X);
        _length = (int)MathF.Ceiling(sdf.Size.Z);
        RenderingServer.GlobalShaderParameterSet("sea_surface_y", surfaceY);
        RenderingServer.GlobalShaderParameterSet("reef_extent", new Vector2(_width, _length));
        Apply(map ?? Build(sdf, surfaceY));
    }

    /// <summary>A closed cave (the grey-box): caustics everywhere, no shadow map, a notional surface above it.</summary>
    public void ShowCave(float surfaceY)
    {
        _sdf = null;
        RenderingServer.GlobalShaderParameterSet("sea_surface_y", surfaceY);
        RenderingServer.GlobalShaderParameterSet("sun_has_shadow", 0f);
    }

    public void SetStrength(float strength) => RenderingServer.GlobalShaderParameterSet("light_strength", strength);

    /// <summary>A crater may let new light in: rebuild the map in the background.</summary>
    public void MarkDirty() => _dirty = _sdf is not null;

    public override void _Process(double delta)
    {
        if (_rebuild is { IsCompleted: true } task)
        {
            _rebuild = null;
            // A crater carved while the map was being read can upset it: just try again.
            if (task.IsFaulted) _dirty = true;
            else Apply(task.Result);
        }
        if (_dirty && _rebuild is null && _sdf is not null)
        {
            _dirty = false;
            var sdf = _sdf;
            float surface = _surfaceY;
            _rebuild = Task.Run(() => Build(sdf, surface));
        }
    }

    /// <summary>For each 1 m cell of the surface, cast its sunbeam down until it meets rock. Thread-safe (pure Core).</summary>
    public static float[] Build(VoxelSdf sdf, float surfaceY)
    {
        int w = (int)MathF.Ceiling(sdf.Size.X), d = (int)MathF.Ceiling(sdf.Size.Z);
        var map = new float[w * d];
        var dir = new System.Numerics.Vector3(Direction.X, Direction.Y, Direction.Z);
        Parallel.For(0, d, z =>
        {
            for (int x = 0; x < w; x++)
            {
                var entry = new System.Numerics.Vector3(x + 0.5f, surfaceY, z + 0.5f);
                if (!sdf.Raycast(entry, dir, 400f, out float hit))
                {
                    map[z * w + x] = NoHit;
                    continue;
                }
                // The reef's outer rim is the edge of the world, not scenery: it casts no shadow.
                var at = entry + dir * hit;
                float edge = MathF.Min(MathF.Min(at.X, w - at.X), MathF.Min(at.Z, d - at.Z));
                map[z * w + x] = edge < RimMargin ? NoHit : at.Y;
            }
        });
        return map;
    }

    void Apply(float[] map)
    {
        var data = new byte[map.Length * 4];
        Buffer.BlockCopy(map, 0, data, 0, data.Length);
        var image = Image.CreateFromData(_width, _length, false, Image.Format.Rf, data);
        RenderingServer.GlobalShaderParameterSet("sun_shadow", ImageTexture.CreateFromImage(image));
        RenderingServer.GlobalShaderParameterSet("sun_has_shadow", 1f);
    }
}
