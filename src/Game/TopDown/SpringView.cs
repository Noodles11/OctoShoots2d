using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The light springs (docs/CORRUPTION.md): a pool of clear, luminous water on the floor with her emblem flowing under
/// it, a soft column of light rising to her plane, sparkles drifting up, and an aqua-gold light on the rocks around.
/// While she bathes it brightens, the emblem turns faster and the sparkles stream. Once spent (it has healed her and she
/// has left it), the water, the column, the light and the sparkles fade away over SpentFade seconds and it is gone.
/// </summary>
public partial class SpringView : Node3D
{
    sealed class View
    {
        public required LightSpring Spring;
        public required MeshInstance3D Pool, Column;
        public required OmniLight3D Light;
        public required float Floor;
        public float Bathe, Spark, Fade = 1f;
    }

    const float SpentFade = 2.5f;

    readonly List<View> _views = new();
    ShaderMaterial _pool = null!, _column = null!;
    FxParticles _sparkles = null!;
    LevelMap? _map;

    public override void _Ready()
    {
        _pool = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/light_spring.gdshader"), RenderPriority = 1 };
        _column = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/light_column.gdshader"), RenderPriority = 1 };
        _sparkles = new FxParticles(384, additive: true);
        AddChild(_sparkles);
    }

    /// <summary>A new level: its springs, if any.</summary>
    public void Show(PlaneWorld world)
    {
        _map = world.Map;
        foreach (var v in _views)
        {
            v.Pool.QueueFree();
            v.Column.QueueFree();
            v.Light.QueueFree();
        }
        _views.Clear();
        _sparkles.Clear();
        foreach (var spring in world.Springs) _views.Add(Make(spring));
    }

    View Make(LightSpring spring)
    {
        float floor = Mathf.Min(_map!.HeightAt(spring.Position), LevelMap.BlockHeight);
        float r = SpringTuning.Radius;
        var at = new Vector3(spring.Position.X, 0f, spring.Position.Y);
        var pool = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(r * 2f, r * 2f), Orientation = PlaneMesh.OrientationEnum.Y },
            MaterialOverride = _pool,
            Position = at + Vector3.Up * (floor + 0.06f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        float height = LevelMap.SwimBand + 0.6f - floor;
        var column = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = r * 0.55f, BottomRadius = r * 0.85f, Height = height, RadialSegments = 24, Rings = 1, CapTop = false, CapBottom = false },
            MaterialOverride = _column,
            Position = at + Vector3.Up * (floor + height * 0.5f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        var light = new OmniLight3D
        {
            LightColor = new Color(0.55f, 0.95f, 1f),
            LightEnergy = 1.4f,
            OmniRange = r * 2.6f,
            ShadowEnabled = false,
            Position = at + Vector3.Up * (floor + 1.2f),
        };
        AddChild(pool);
        AddChild(column);
        AddChild(light);
        return new View { Spring = spring, Pool = pool, Column = column, Light = light, Floor = floor };
    }

    public void Sync(float dt, Camera3D? camera)
    {
        foreach (var v in _views)
        {
            if (v.Spring.Spent) v.Fade = Mathf.MoveToward(v.Fade, 0f, dt / SpentFade);
            bool shown = v.Fade > 0f;
            v.Pool.Visible = v.Column.Visible = v.Light.Visible = shown;
            if (!shown) continue;
            // Fading, it eases out (slow at first, then gone).
            float k = v.Fade * v.Fade;
            v.Bathe = Mathf.MoveToward(v.Bathe, v.Spring.Bathing ? 1f : 0f, dt * 2.5f);
            v.Pool.SetInstanceShaderParameter("bathing", v.Bathe);
            v.Column.SetInstanceShaderParameter("bathing", v.Bathe);
            v.Pool.SetInstanceShaderParameter("fade", k);
            v.Column.SetInstanceShaderParameter("fade", k);
            v.Light.LightEnergy = (1.4f + 1.6f * v.Bathe) * k;
            v.Light.LightColor = new Color(0.55f, 0.95f, 1f).Lerp(new Color(1f, 0.85f, 0.55f), 0.4f * v.Bathe);
            // Sparkles rise from the water: a few always, a stream while she bathes.
            v.Spark += dt * (5f + 30f * v.Bathe) * k;
            while (v.Spark >= 1f)
            {
                v.Spark -= 1f;
                float a = _sparkles.RandRange(0f, Mathf.Tau), d = Mathf.Sqrt(_sparkles.RandRange(0f, 1f)) * SpringTuning.Radius * 0.85f;
                var from = new Vector3(v.Spring.Position.X + Mathf.Cos(a) * d, v.Floor + 0.1f, v.Spring.Position.Y + Mathf.Sin(a) * d);
                bool gold = _sparkles.RandRange(0f, 1f) < 0.35f + 0.4f * v.Bathe;
                var c0 = gold ? new Color(1f, 0.85f, 0.5f) : new Color(0.6f, 0.97f, 1f);
                _sparkles.Emit(from, new Vector3(0f, _sparkles.RandRange(0.8f, 2.2f), 0f) + _sparkles.RandDir() * 0.25f,
                    _sparkles.RandRange(1.4f, 2.6f), _sparkles.RandRange(0.06f, 0.14f), 0.01f, c0, c0 with { A = 0f }, drag: 0.4f, buoyancy: 0.5f);
            }
        }
        if (camera is not null) _sparkles.Tick(dt, camera.GlobalBasis);
    }
}
