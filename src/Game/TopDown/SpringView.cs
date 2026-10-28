using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The light springs (docs/CORRUPTION.md): a god ray — one cone of sunlight falling slantwise from the surface onto the
/// seabed (god_ray.gdshader), a spotlight lighting the floor it falls on — and her emblem in white light on the floor
/// there (the gonads and a circle, still), motes drifting down the shaft. While it heals her it glows brighter and
/// warmer, sparkles stream up round it and twirl round her bell (the screen trembles and the controller hums:
/// TopDownMain); the ray dims a little as its store of healing is used. Once spent, the ray and the emblem fade quickly away (SpentFade).
/// </summary>
public partial class SpringView : Node3D
{
    sealed class View
    {
        public required LightSpring Spring;
        public required MeshInstance3D Pool, Ray;
        public required SpotLight3D Light;
        public required float Floor;
        public required Vector3 Down;
        public float Glow, Spark, Mote, Halo, Fade = 1f;
    }

    const float SpentFade = 0.8f;
    /// <summary>The ray: where the surface is above the floor, how far from upright it falls, and from which side (north-west, so it rises up the screen).</summary>
    const float RayHeight = 17f, RaySlant = 32f;
    static readonly Vector2 RayFrom = new Vector2(-0.45f, -0.89f).Normalized();
    /// <summary>The ray's width at the surface, and on the floor (shares of the spring's radius).</summary>
    const float RayTop = 0.5f, RayFoot = 1.05f;

    readonly List<View> _views = new();
    ShaderMaterial _pool = null!, _ray = null!;
    FxParticles _sparkles = null!;
    LevelMap? _map;

    public override void _Ready()
    {
        _pool = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/light_spring.gdshader"), RenderPriority = 1 };
        _ray = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/god_ray.gdshader"), RenderPriority = 2 };
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
            v.Ray.QueueFree();
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
        var at = new Vector3(spring.Position.X, floor, spring.Position.Y);
        var pool = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(r * 2f, r * 2f), Orientation = PlaneMesh.OrientationEnum.Y },
            MaterialOverride = _pool,
            Position = at + Vector3.Up * 0.06f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        // The ray: from the surface, up and off to the north-west, down onto the spring.
        float reach = RayHeight * Mathf.Tan(Mathf.DegToRad(RaySlant));
        var top = new Vector3(RayFrom.X * reach, RayHeight, RayFrom.Y * reach);
        var ray = new MeshInstance3D
        {
            Mesh = RayMesh(top, r * RayTop, r * RayFoot),
            MaterialOverride = _ray,
            Position = at,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        ray.SetInstanceShaderParameter(SN.Of("seed"), spring.Position.X * 0.37f + spring.Position.Y * 0.11f);
        // Its light on the floor: a spot from high up the ray, its cone just covering the spring. Shadowed like hers,
        // so the rocks and kelp round it cast long slanting shadows.
        var down = -top.Normalized();
        var from = at + top * 0.7f;
        float dist = from.DistanceTo(at);
        var light = new SpotLight3D
        {
            LightColor = new Color(0.8f, 0.94f, 1f),
            LightEnergy = 3f,
            SpotRange = dist * 1.4f,
            SpotAngle = Mathf.RadToDeg(Mathf.Atan(r * 1.25f / dist)),
            SpotAngleAttenuation = 0.6f,
            ShadowEnabled = OctoShoots.Game.Settings.ViewOptions.Shadows,
            ShadowBias = 0.08f,
            ShadowBlur = 1.5f,
        };
        AddChild(pool);
        AddChild(ray);
        AddChild(light);
        light.LookAtFromPosition(from, at, Mathf.Abs(down.Y) > 0.99f ? Vector3.Forward : Vector3.Up);
        return new View { Spring = spring, Pool = pool, Ray = ray, Light = light, Floor = floor, Down = down };
    }

    /// <summary>
    /// The ray: an oblique cone, open at both ends — a circle of footRadius on the floor (at the origin) up to one of
    /// topRadius round top. UV x round it, y from the top (0) to the floor (1).
    /// </summary>
    static ArrayMesh RayMesh(Vector3 top, float topRadius, float footRadius)
    {
        const int rings = 16, segments = 48;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var axis = top.Normalized();
        for (int i = 0; i <= rings; i++)
            for (int j = 0; j <= segments; j++)
            {
                float v = i / (float)rings, a = j / (float)segments * Mathf.Tau;
                var round = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float radius = Mathf.Lerp(topRadius, footRadius, v);
                var centre = top * (1f - v);
                // Its normal: outward from the axis.
                var n = (round - axis * round.Dot(axis)).Normalized();
                st.SetNormal(n);
                st.SetUV(new Vector2(j / (float)segments, v));
                st.AddVertex(centre + round * radius);
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

    /// <summary>A frame: each spring's light, and its sparkles (round her bell, at her, while it heals her).</summary>
    public void Sync(float dt, Camera3D? camera, Vector3 her)
    {
        foreach (var v in _views)
        {
            if (v.Spring.Spent) v.Fade = Mathf.MoveToward(v.Fade, 0f, dt / SpentFade);
            bool shown = v.Fade > 0f;
            v.Pool.Visible = v.Ray.Visible = v.Light.Visible = shown;
            if (!shown) continue;
            // Fading, it eases out (slow at first, then gone).
            float k = v.Fade * v.Fade;
            float store = v.Spring.PoolMax > 0f ? v.Spring.Pool / v.Spring.PoolMax : 0f;
            v.Glow = Mathf.MoveToward(v.Glow, v.Spring.Healing ? 1f : 0f, dt * 2.5f);
            v.Pool.SetInstanceShaderParameter(SN.Of("glow"), v.Glow);
            v.Pool.SetInstanceShaderParameter(SN.Of("fade"), k);
            v.Ray.SetInstanceShaderParameter(SN.Of("glow"), v.Glow);
            v.Ray.SetInstanceShaderParameter(SN.Of("store"), store);
            v.Ray.SetInstanceShaderParameter(SN.Of("fade"), k);
            v.Light.LightEnergy = (3f + 3.5f * v.Glow) * (0.6f + 0.4f * store) * k;
            v.Light.LightColor = new Color(0.8f, 0.94f, 1f).Lerp(new Color(1f, 0.86f, 0.6f), 0.45f * v.Glow);
            var at = new Vector3(v.Spring.Position.X, v.Floor, v.Spring.Position.Y);
            // Motes drifting slowly down the shaft, catching the light.
            v.Mote += dt * 6f * k;
            while (v.Mote >= 1f)
            {
                v.Mote -= 1f;
                float h = _sparkles.RandRange(0.5f, 7f);
                var round = _sparkles.RandDir() * SpringTuning.Radius * 0.7f;
                var from = at - v.Down * (h / Mathf.Max(-v.Down.Y, 0.1f)) + round with { Y = 0f };
                var c = new Color(0.85f, 0.95f, 1f, 0.7f);
                _sparkles.Emit(from, v.Down * _sparkles.RandRange(0.3f, 0.6f), _sparkles.RandRange(2.5f, 4f), _sparkles.RandRange(0.03f, 0.06f), 0.01f, c, c with { A = 0f }, drag: 0f, buoyancy: 0f);
            }
            // While it heals her, gold sparkles twirl up round her bell.
            v.Halo += dt * 40f * v.Glow * k;
            while (v.Halo >= 1f)
            {
                v.Halo -= 1f;
                float a = _sparkles.RandRange(0f, Mathf.Tau), d = _sparkles.RandRange(0.45f, 0.8f);
                var round = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var c0 = _sparkles.RandRange(0f, 1f) < 0.6f ? new Color(1f, 0.88f, 0.55f) : new Color(0.85f, 1f, 1f);
                _sparkles.Emit(her + round * d + Vector3.Up * _sparkles.RandRange(-0.2f, 0.3f), new Vector3(-round.Z, 0.9f, round.X) * _sparkles.RandRange(0.5f, 1f),
                    _sparkles.RandRange(0.7f, 1.2f), _sparkles.RandRange(0.09f, 0.17f), 0.01f, c0, c0 with { A = 0f }, drag: 1f, buoyancy: 0.3f);
            }
            // Sparkles rise from the floor: a few always, a stream while it heals her.
            v.Spark += dt * (4f + 30f * v.Glow) * k;
            while (v.Spark >= 1f)
            {
                v.Spark -= 1f;
                float a = _sparkles.RandRange(0f, Mathf.Tau), d = Mathf.Sqrt(_sparkles.RandRange(0f, 1f)) * SpringTuning.Radius * 0.85f;
                var from = at + new Vector3(Mathf.Cos(a) * d, 0.1f, Mathf.Sin(a) * d);
                bool gold = _sparkles.RandRange(0f, 1f) < 0.35f + 0.4f * v.Glow;
                var c0 = gold ? new Color(1f, 0.85f, 0.5f) : new Color(0.6f, 0.97f, 1f);
                _sparkles.Emit(from, new Vector3(0f, _sparkles.RandRange(0.8f, 2.2f), 0f) + _sparkles.RandDir() * 0.25f,
                    _sparkles.RandRange(1.4f, 2.6f), _sparkles.RandRange(0.06f, 0.14f), 0.01f, c0, c0 with { A = 0f }, drag: 0.4f, buoyancy: 0.5f);
            }
        }
        if (camera is not null) _sparkles.Tick(dt, camera.GlobalBasis);
    }
}
