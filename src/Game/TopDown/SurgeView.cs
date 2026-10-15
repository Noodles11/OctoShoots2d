using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Firefly Squid's surge (PlaneSurge.cs): a shaft of her light on the swim band. Each ray is a ribbon (a white-hot core
/// in her light's glow, pulses racing outward along it); a cone (more shots) is one filled fan of light, soft at its
/// sides, with its middle ray drawn hotter; Double Helix twists two strands round each ray, brighter on the near side
/// of their turn. Hot glows mark her mouth, its tip and every impact, and a light at the tip lights what it touches.
/// </summary>
public partial class SurgeView : Node3D
{
    const int MaxGlows = 14;
    /// <summary>A ribbon is this many times its core's width, so her light's glow spills round the core.</summary>
    const float Spill = 3.2f;
    /// <summary>Double Helix: the strands turn once every Twist metres, racing outward.</summary>
    const float Twist = 1.6f, TwistSpeed = 9f;

    readonly ImmediateMesh _mesh = new();
    ShaderMaterial _beam = null!, _glow = null!;
    readonly List<MeshInstance3D> _glows = new();
    OmniLight3D _tip = null!;
    float _time;
    int _vertices;

    readonly List<(System.Numerics.Vector2 P, System.Numerics.Vector2 Dir, float S)> _samples = new();

    public override void _Ready()
    {
        _beam = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/light_surge.gdshader") };
        _glow = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/light_shot.gdshader") };
        AddChild(new MeshInstance3D { Mesh = _mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 16384f });
        var quad = new QuadMesh { Size = Vector2.One };
        for (int i = 0; i < MaxGlows; i++)
        {
            var g = new MeshInstance3D { Mesh = quad, MaterialOverride = _glow, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
            g.SetInstanceShaderParameter("seed", i * 0.37f);
            AddChild(g);
            _glows.Add(g);
        }
        _tip = new OmniLight3D { LightColor = new Color(1f, 0.9f, 0.6f), OmniRange = 4.5f, ShadowEnabled = false, Visible = false };
        AddChild(_tip);
    }

    public void Sync(PlaneWorld world, float dt)
    {
        _time += dt;
        _mesh.ClearSurfaces();
        var surge = world.LightSurge;
        if (surge is null || surge.Rays.Count == 0)
        {
            foreach (var g in _glows) g.Visible = false;
            _tip.Visible = false;
            return;
        }
        // Light comes on in a flash rather than at once.
        float on = Mathf.Clamp(surge.Age / 0.08f, 0.3f, 1f);
        float bright = on * Mathf.Clamp(0.75f + 0.25f * surge.Power, 0.75f, 1.6f);
        _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, _beam);
        _vertices = 0;

        var main = surge.Rays.FindAll(r => !r.Fork);
        bool fan = main.Count > 1 && main.TrueForAll(r => r.Points.Count == 2);
        if (fan)
        {
            Fan(main, bright);
            // Its middle drawn hotter.
            Ray(main[main.Count / 2], bright * 0.55f, surge.Helix);
        }
        foreach (var ray in surge.Rays)
            if (!fan || ray.Fork) Ray(ray, ray.Fork ? bright * 0.7f : bright, surge.Helix);
        if (_vertices == 0) Quad(Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero, 0f, 0f, 0f, 0f, 0f, 0f);
        _mesh.SurfaceEnd();

        // Glows: her mouth, the tips and the impacts (not on top of each other).
        var spots = new List<(System.Numerics.Vector2 At, float Size)>();
        void Spot(System.Numerics.Vector2 at, float size)
        {
            if (spots.Count >= MaxGlows) return;
            foreach (var (q, _) in spots)
                if (System.Numerics.Vector2.DistanceSquared(q, at) < 0.36f) return;
            spots.Add((at, size));
        }
        Spot(surge.Rays[0].Points[0], 0.9f);
        foreach (var at in surge.Impacts) Spot(at, 1.5f);
        // A cone's open edge is one soft front, not a row of sparks: only its middle ray's tip glows (and any that struck).
        var middle = main.Count > 0 ? main[main.Count / 2] : null;
        foreach (var ray in surge.Rays)
            if (ray.Blocked || ray.Fork || !fan || ray == middle) Spot(ray.End, ray.Blocked ? 1.4f : 1f);
        for (int i = 0; i < _glows.Count; i++)
        {
            var g = _glows[i];
            g.Visible = i < spots.Count;
            if (!g.Visible) continue;
            var (at, size) = spots[i];
            float flicker = 0.85f + 0.15f * Mathf.Sin(_time * 40f + i * 2.1f);
            g.Position = new Vector3(at.X, LevelMap.SwimBand, at.Y);
            g.Scale = Vector3.One * size * (0.8f + 0.2f * surge.Power) * flicker;
            g.SetInstanceShaderParameter("lantern", Mathf.Min(1f, 0.55f * bright));
            g.SetInstanceShaderParameter("warmth", 0.6f);
        }
        var end = surge.Rays[main.Count > 0 ? surge.Rays.IndexOf(main[main.Count / 2]) : 0].End;
        _tip.Visible = true;
        _tip.Position = new Vector3(end.X, LevelMap.SwimBand + 0.6f, end.Y);
        _tip.LightEnergy = 1.6f * bright;
    }

    /// <summary>One ray as a ribbon (or, with Double Helix, a faint core and two strands spiralling round it).</summary>
    void Ray(SurgeRay ray, float bright, bool helix)
    {
        Sample(ray.Points);
        if (_samples.Count < 2) return;
        if (!helix)
        {
            Ribbon(ray, bright, 0f, 1f);
            return;
        }
        Ribbon(ray, bright * 0.35f, 0f, 1f);
        Ribbon(ray, bright, 0f, 0.8f, strand: 1f);
        Ribbon(ray, bright, Mathf.Pi, 0.8f, strand: 1f);
    }

    /// <summary>
    /// A ribbon along the sampled ray: Spill × its core's half-width each side. A strand (Double Helix) weaves off the
    /// line by the wave's amplitude, its phase racing outward, brighter on the near half of its turn.
    /// </summary>
    void Ribbon(SurgeRay ray, float bright, float phase, float thin, float strand = 0f)
    {
        float length = _samples[^1].S;
        for (int i = 1; i < _samples.Count; i++)
        {
            var (p0, d0, s0) = _samples[i - 1];
            var (p1, d1, s1) = _samples[i];
            Edge(p0, d0, s0, out var l0, out var r0, out float b0, out float g0);
            Edge(p1, d1, s1, out var l1, out var r1, out float b1, out float g1);
            Quad(l0, r0, r1, l1, s0, s1, b0, b1, g0, g1);
        }

        void Edge(System.Numerics.Vector2 p, System.Numerics.Vector2 d, float s, out Vector3 left, out Vector3 right, out float b, out float core)
        {
            var n = new System.Numerics.Vector2(-d.Y, d.X);
            float w = ray.WidthAt(s) * thin;
            float k = 1f;
            if (strand > 0f)
            {
                float turn = s / Twist * Mathf.Tau - _time * TwistSpeed + phase;
                float amp = PlaneCombatTuning.WaveAmplitude * Mathf.SmoothStep(0f, 0.8f, s);
                p += n * (amp * Mathf.Sin(turn));
                k = 0.55f + 0.45f * Mathf.Cos(turn);
            }
            float half = w * Spill + 0.06f;
            var c = new Vector3(p.X, LevelMap.SwimBand, p.Y);
            var off = new Vector3(n.X, 0f, n.Y) * half;
            left = c - off;
            right = c + off;
            // In over its first half metre, out over its last 0.3 m.
            float fade = Mathf.SmoothStep(0f, 0.5f, s + ray.StartAt) * (1f - Mathf.SmoothStep(length - 0.3f, length, s));
            b = bright * k * fade;
            core = w / half;
        }
    }

    /// <summary>The cone: one fan of light between neighbouring rays, from her mouth to where each ray ends.</summary>
    void Fan(List<SurgeRay> rays, float bright)
    {
        const int Steps = 12;
        var o = rays[0].Points[0];
        for (int i = 0; i + 1 < rays.Count; i++)
        {
            var a = rays[i].End;
            var b = rays[i + 1].End;
            float u0 = -1f + 2f * i / (rays.Count - 1), u1 = -1f + 2f * (i + 1) / (rays.Count - 1);
            for (int t = 0; t < Steps; t++)
            {
                float t0 = t / (float)Steps, t1 = (t + 1) / (float)Steps;
                var a0 = o + (a - o) * t0;
                var a1 = o + (a - o) * t1;
                var b0 = o + (b - o) * t0;
                var b1 = o + (b - o) * t1;
                float s0 = System.Numerics.Vector2.Distance(o, a0), s1 = System.Numerics.Vector2.Distance(o, a1);
                float f0 = bright * Mathf.SmoothStep(0f, 0.15f, t0) * (1f - 0.45f * t0), f1 = bright * Mathf.SmoothStep(0f, 0.15f, t1) * (1f - 0.45f * t1);
                _mesh.SurfaceSetColor(new Color(f0, 1f, 1f, 1f));
                Vertex(a0, s0, u0);
                _mesh.SurfaceSetColor(new Color(f0, 1f, 1f, 1f));
                Vertex(b0, s0, u1);
                _mesh.SurfaceSetColor(new Color(f1, 1f, 1f, 1f));
                Vertex(b1, s1, u1);
                _mesh.SurfaceSetColor(new Color(f0, 1f, 1f, 1f));
                Vertex(a0, s0, u0);
                _mesh.SurfaceSetColor(new Color(f1, 1f, 1f, 1f));
                Vertex(b1, s1, u1);
                _mesh.SurfaceSetColor(new Color(f1, 1f, 1f, 1f));
                Vertex(a1, s1, u0);
            }
        }

        void Vertex(System.Numerics.Vector2 p, float s, float u)
        {
            _mesh.SurfaceSetUV(new Vector2(s, u));
            _mesh.SurfaceAddVertex(new Vector3(p.X, LevelMap.SwimBand - 0.02f, p.Y));
            _vertices++;
        }
    }

    /// <summary>Two triangles: left/right at the near end (along s0, brightness b0, core share g0), then the far end.</summary>
    void Quad(Vector3 l0, Vector3 r0, Vector3 r1, Vector3 l1, float s0, float s1, float b0, float b1, float g0, float g1)
    {
        void V(Vector3 p, float s, float u, float b, float g)
        {
            _mesh.SurfaceSetColor(new Color(b, g, 0f, 1f));
            _mesh.SurfaceSetUV(new Vector2(s, u));
            _mesh.SurfaceAddVertex(p);
            _vertices++;
        }
        V(l0, s0, -1f, b0, g0);
        V(r0, s0, 1f, b0, g0);
        V(r1, s1, 1f, b1, g1);
        V(l0, s0, -1f, b0, g0);
        V(r1, s1, 1f, b1, g1);
        V(l1, s1, -1f, b1, g1);
    }

    /// <summary>The ray's polyline resampled every 0.2 m (each sample with its heading and distance along).</summary>
    void Sample(List<System.Numerics.Vector2> points)
    {
        _samples.Clear();
        float s = 0f;
        for (int i = 1; i < points.Count; i++)
        {
            var a = points[i - 1];
            var b = points[i];
            float len = System.Numerics.Vector2.Distance(a, b);
            if (len < 1e-4f) continue;
            var d = (b - a) / len;
            int n = Mathf.Max(1, Mathf.CeilToInt(len / 0.2f));
            for (int k = _samples.Count == 0 ? 0 : 1; k <= n; k++)
                _samples.Add((a + d * (len * k / n), d, s + len * k / n));
            s += len;
        }
    }
}
