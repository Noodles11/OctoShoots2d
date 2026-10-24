using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The light springs (docs/CORRUPTION.md): her emblem in white light on the floor (the gonads and a circle, still),
/// under a dome like a great jellyfish bell lying on the sand — a clear, faint force field, breathing — sparkles drifting
/// up, and an aqua light on the rocks round it. While she bathes it brightens, a burst of holy light (god rays) blazes up
/// from the emblem and fans out round her, and the sparkles stream (the screen trembles and the controller
/// hums while it heals her: TopDownMain). Once spent (it has healed her and she has left it), all of it fades away over
/// SpentFade seconds and it is gone.
/// </summary>
public partial class SpringView : Node3D
{
    sealed class View
    {
        public required LightSpring Spring;
        public required MeshInstance3D Pool, Column, Dome;
        public required OmniLight3D Light;
        public required float Floor;
        public float Bathe, Spark, Fade = 1f;
    }

    const float SpentFade = 2.5f;

    readonly List<View> _views = new();
    ShaderMaterial _pool = null!, _column = null!, _dome = null!;
    ArrayMesh? _domeMesh;
    /// <summary>The dome: how high it stands over the floor (her plane well inside it), and its margin's lappets.</summary>
    const float DomeHeight = 2.6f;
    const int Lappets = 16;
    FxParticles _sparkles = null!;
    LevelMap? _map;

    public override void _Ready()
    {
        _pool = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/light_spring.gdshader"), RenderPriority = 1 };
        _column = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/light_column.gdshader"), RenderPriority = 1 };
        _dome = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/spring_dome.gdshader"), RenderPriority = 2 };
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
            v.Dome.QueueFree();
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
        // The holy light: a burst of rays up from the emblem, fanning out wide as it climbs, well past her plane.
        float height = LevelMap.SwimBand + 5f - floor;
        var column = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = r * 2.3f, BottomRadius = r * 0.45f, Height = height, RadialSegments = 64, Rings = 1, CapTop = false, CapBottom = false },
            MaterialOverride = _column,
            Position = at + Vector3.Up * (floor + height * 0.5f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        var light = new OmniLight3D
        {
            LightColor = new Color(0.55f, 0.95f, 1f),
            LightEnergy = 1.4f,
            OmniRange = r * 2.6f,
            // Its light is shadowed like hers: the rocks and kelp round the pool stand out against it.
            ShadowEnabled = OctoShoots.Game.Settings.ViewOptions.Shadows,
            OmniShadowMode = OmniLight3D.ShadowMode.DualParaboloid,
            ShadowBias = 0.08f,
            ShadowBlur = 1.5f,
            Position = at + Vector3.Up * (floor + 1.2f),
        };
        var dome = new MeshInstance3D
        {
            Mesh = _domeMesh ??= DomeMesh(r),
            MaterialOverride = _dome,
            Position = at + Vector3.Up * floor,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(pool);
        AddChild(column);
        AddChild(dome);
        AddChild(light);
        return new View { Spring = spring, Pool = pool, Column = column, Dome = dome, Light = light, Floor = floor };
    }

    /// <summary>
    /// The dome: a jellyfish bell lying on the sand, as wide as the spring — a full, rounded crown easing down to a margin
    /// of Lappets scallops on the floor. UV x from the apex (0) to the margin (1), y round it.
    /// </summary>
    static ArrayMesh DomeMesh(float radius)
    {
        const int rings = 22, segments = 96;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i <= rings; i++)
            for (int j = 0; j <= segments; j++)
            {
                float u = i / (float)rings, a = j / (float)segments * Mathf.Tau;
                float th = u * Mathf.Pi * 0.5f;
                float scallop = 1f + 0.05f * Mathf.Pow(u, 6f) * Mathf.Cos(a * Lappets);
                float rho = radius * Mathf.Pow(Mathf.Sin(th), 0.8f) * scallop;
                float y = DomeHeight * Mathf.Pow(Mathf.Cos(th), 1.15f);
                var n = new Vector3(Mathf.Cos(a) * Mathf.Sin(th) / radius, Mathf.Cos(th) / DomeHeight, Mathf.Sin(a) * Mathf.Sin(th) / radius).Normalized();
                st.SetNormal(n);
                st.SetUV(new Vector2(u, j / (float)segments));
                st.AddVertex(new Vector3(Mathf.Cos(a) * rho, y, Mathf.Sin(a) * rho));
            }
        for (int i = 0; i < rings; i++)
            for (int j = 0; j < segments; j++)
            {
                int k = i * (segments + 1) + j, k1 = k + 1, n0 = k + segments + 1, n1 = n0 + 1;
                st.AddIndex(k); st.AddIndex(n0); st.AddIndex(k1);
                st.AddIndex(k1); st.AddIndex(n0); st.AddIndex(n1);
            }
        return st.Commit();
    }

    public void Sync(float dt, Camera3D? camera)
    {
        foreach (var v in _views)
        {
            if (v.Spring.Spent) v.Fade = Mathf.MoveToward(v.Fade, 0f, dt / SpentFade);
            bool shown = v.Fade > 0f;
            v.Pool.Visible = v.Column.Visible = v.Dome.Visible = v.Light.Visible = shown;
            if (!shown) continue;
            // Fading, it eases out (slow at first, then gone).
            float k = v.Fade * v.Fade;
            v.Bathe = Mathf.MoveToward(v.Bathe, v.Spring.Bathing ? 1f : 0f, dt * 2.5f);
            v.Pool.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("bathing"), v.Bathe);
            v.Column.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("bathing"), v.Bathe);
            v.Dome.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("bathing"), v.Bathe);
            v.Pool.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("fade"), k);
            v.Column.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("fade"), k);
            v.Dome.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("fade"), k);
            v.Light.LightEnergy = (1.4f + 3.2f * v.Bathe) * k;
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
