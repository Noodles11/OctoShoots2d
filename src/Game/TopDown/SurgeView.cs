using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Firefly Squid's surge (PlaneSurge.cs): a shaft of her light on the swim band. Each ray is a ribbon (a white-hot core
/// in her light's glow, pulses racing outward along it); a cone (more shots) is one filled fan of light, soft at its
/// sides, with its middle ray drawn hotter; Double Helix twists two strands round each ray, brighter on the near side
/// of their turn; Boomerang Shrimp's rays are loops back to her. Where a ray ends without striking anything it comes
/// down to the seabed and burns it: a patch of flame (a cone's whole far edge burns in a line), embers lifting off and
/// a thread of smoke. Hot glows mark her mouth and every impact, and a light at the tip lights what it touches.
/// </summary>
public partial class SurgeView : Node3D
{
    const int MaxGlows = 14;
    /// <summary>A ribbon is this many times its core's width, so her light's glow spills round the core.</summary>
    const float Spill = 3.2f;
    /// <summary>Double Helix: the strands turn once every Twist metres, racing outward.</summary>
    const float Twist = 1.6f, TwistSpeed = 9f;
    /// <summary>The last Dip metres of a ray that strikes nothing bend down to the seabed.</summary>
    const float Dip = 1.6f;
    /// <summary>A ground burn's radius: this plus its ray's width.</summary>
    const float BurnRadius = 1f;

    readonly ImmediateMesh _mesh = new();
    ShaderMaterial _beam = null!, _burn = null!, _scorch = null!, _glow = null!;
    readonly List<MeshInstance3D> _glows = new();
    OmniLight3D _tip = null!;
    FxParticles _embers = null!, _smoke = null!;
    LevelMap? _map;
    float _time;
    int _vertices;

    readonly List<(System.Numerics.Vector2 P, System.Numerics.Vector2 Dir, float S)> _samples = new();
    readonly List<(System.Numerics.Vector2 At, float Radius)> _burns = new();
    readonly List<(System.Numerics.Vector2 A, System.Numerics.Vector2 B)> _front = new();

    public override void _Ready()
    {
        _beam = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/light_surge.gdshader") };
        _burn = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/light_burn.gdshader") };
        _scorch = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/light_scorch.gdshader"), RenderPriority = -1 };
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
        _tip = new OmniLight3D { LightColor = new Color(1f, 0.8f, 0.5f), OmniRange = 4.5f, ShadowEnabled = false, Visible = false };
        AddChild(_tip);
        _embers = new FxParticles(512, additive: true);
        AddChild(_embers);
        _smoke = new FxParticles(256, additive: false);
        AddChild(_smoke);
    }

    /// <summary>The seabed's height under a point (capped at the rock tops).</summary>
    float Floor(System.Numerics.Vector2 p) => _map is null ? LevelMap.SwimBand - 2f : Mathf.Min(_map.HeightAt(p), LevelMap.BlockHeight);

    public void Sync(PlaneWorld world, float dt)
    {
        _time += dt;
        _map = world.Map;
        _mesh.ClearSurfaces();
        if (GetViewport().GetCamera3D() is { } camera)
        {
            _embers.Tick(dt, camera.GlobalBasis);
            _smoke.Tick(dt, camera.GlobalBasis);
        }
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
        _burns.Clear();
        _front.Clear();
        _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, _beam);
        _vertices = 0;

        var main = surge.Rays.FindAll(r => !r.Fork);
        var middle = main.Count > 0 ? main[main.Count / 2] : null;
        bool fan = main.Count > 1 && main.TrueForAll(r => r.Points.Count == 2);
        if (fan)
        {
            Fan(main, bright);
            // Its middle drawn hotter.
            Ray(middle!, bright * 0.55f, surge.Helix, dips: !middle!.Blocked);
        }
        foreach (var ray in surge.Rays)
            if (!fan || ray.Fork) Ray(ray, ray.Fork ? bright * 0.7f : bright, surge.Helix, dips: !ray.Blocked && !ray.Loop);
        if (_vertices == 0) Quad(Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero, 0f, 0f, 0f, 0f, 0f, 0f);
        _mesh.SurfaceEnd();

        // Where it comes down on the seabed it burns: a loop's far end, a ray that struck nothing, a cone's far edge.
        foreach (var ray in surge.Rays)
        {
            if (ray.Blocked || fan && !ray.Fork) continue;
            _burns.Add((ray.Tip, BurnRadius + ray.WidthAt(ray.Length) * 3f));
        }
        if (_burns.Count > 0 || _front.Count > 0)
        {
            // A warm tint on the ground first (the same size, fading to nothing at its edge), then the flame over it.
            _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, _scorch);
            foreach (var (at, radius) in _burns) BurnPatch(at, radius, bright, lift: 0.04f);
            foreach (var (a, b) in _front) BurnStrip(a, b, bright, 0.55f, lift: 0.05f);
            _mesh.SurfaceEnd();
            _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, _burn);
            foreach (var (at, radius) in _burns) BurnPatch(at, radius, bright);
            foreach (var (a, b) in _front) BurnStrip(a, b, bright, 0.55f);
            _mesh.SurfaceEnd();
        }
        Embers(dt, bright);

        // Glows: her mouth and every impact (not on top of each other).
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
        foreach (var ray in surge.Rays)
            if (ray.Blocked) Spot(ray.End, 1.4f);
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
        var tip = (middle ?? surge.Rays[0]).Tip;
        bool grounded = middle is { Blocked: false };
        _tip.Visible = true;
        _tip.Position = new Vector3(tip.X, (grounded ? Floor(tip) : LevelMap.SwimBand) + 0.6f, tip.Y);
        _tip.LightEnergy = 1.6f * bright;
    }

    /// <summary>One ray as a ribbon (or, with Double Helix, a faint core and two strands spiralling round it).</summary>
    void Ray(SurgeRay ray, float bright, bool helix, bool dips)
    {
        Sample(ray.Points);
        if (_samples.Count < 2) return;
        if (!helix)
        {
            Ribbon(ray, bright, 0f, 1f, dips);
            return;
        }
        Ribbon(ray, bright * 0.35f, 0f, 1f, dips);
        Ribbon(ray, bright, 0f, 0.8f, dips, strand: 1f);
        Ribbon(ray, bright, Mathf.Pi, 0.8f, dips, strand: 1f);
    }

    /// <summary>
    /// A ribbon along the sampled ray: Spill × its core's half-width each side. A strand (Double Helix) weaves off the
    /// line by the wave's amplitude, its phase racing outward, brighter on the near half of its turn. One that dips
    /// bends down over its last Dip metres to touch the seabed.
    /// </summary>
    void Ribbon(SurgeRay ray, float bright, float phase, float thin, bool dips, float strand = 0f)
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
            float y = LevelMap.SwimBand;
            if (dips) y = Mathf.Lerp(y, Floor(p) + 0.06f, Mathf.SmoothStep(length - Dip, length, s));
            var c = new Vector3(p.X, y, p.Y);
            var off = new Vector3(n.X, 0f, n.Y) * half;
            left = c - off;
            right = c + off;
            // In over its first half metre; one that strikes something fades out over its last 0.3 m (one that dips
            // runs on into its burn).
            float fade = Mathf.SmoothStep(0f, 0.5f, s + ray.StartAt) * (dips ? 1f : 1f - Mathf.SmoothStep(length - 0.3f, length, s));
            b = bright * k * fade;
            core = w / half;
        }
    }

    /// <summary>
    /// The cone: one fan of light between neighbouring rays, from her mouth to where each ray ends. Where a ray struck
    /// nothing, the fan's far edge comes down to the seabed and burns there.
    /// </summary>
    void Fan(List<SurgeRay> rays, float bright)
    {
        const int Steps = 12;
        var o = rays[0].Points[0];
        for (int i = 0; i + 1 < rays.Count; i++)
        {
            var a = rays[i].End;
            var b = rays[i + 1].End;
            bool downA = !rays[i].Blocked, downB = !rays[i + 1].Blocked;
            if (downA && downB) _front.Add((a, b));
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
                Vertex(a0, s0, u0, f0, t0, downA);
                Vertex(b0, s0, u1, f0, t0, downB);
                Vertex(b1, s1, u1, f1, t1, downB);
                Vertex(a0, s0, u0, f0, t0, downA);
                Vertex(b1, s1, u1, f1, t1, downB);
                Vertex(a1, s1, u0, f1, t1, downA);
            }
        }

        void Vertex(System.Numerics.Vector2 p, float s, float u, float f, float t, bool down)
        {
            float y = LevelMap.SwimBand - 0.02f;
            if (down) y = Mathf.Lerp(y, Floor(p) + 0.06f, Mathf.SmoothStep(0.86f, 1f, t));
            _mesh.SurfaceSetColor(new Color(f, 1f, 1f, 1f));
            _mesh.SurfaceSetUV(new Vector2(s, u));
            _mesh.SurfaceAddVertex(new Vector3(p.X, y, p.Y));
            _vertices++;
        }
    }

    /// <summary>A burning patch on the seabed round a point, laid on the ground's own height.</summary>
    void BurnPatch(System.Numerics.Vector2 at, float radius, float bright, float lift = 0.07f)
    {
        const int Ring = 16;
        float y0 = Floor(at) + lift;
        var centre = new Vector3(at.X, y0, at.Y);
        for (int i = 0; i < Ring; i++)
        {
            float a0 = Mathf.Tau * i / Ring, a1 = Mathf.Tau * (i + 1) / Ring;
            var d0 = new System.Numerics.Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
            var d1 = new System.Numerics.Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
            var p0 = at + d0 * radius;
            var p1 = at + d1 * radius;
            V(centre, Vector2.Zero);
            V(new Vector3(p0.X, Floor(p0) + lift, p0.Y), new Vector2(d0.X, d0.Y));
            V(new Vector3(p1.X, Floor(p1) + lift, p1.Y), new Vector2(d1.X, d1.Y));
        }

        void V(Vector3 p, Vector2 uv)
        {
            _mesh.SurfaceSetColor(new Color(bright, 0f, 0f, 1f));
            _mesh.SurfaceSetUV(uv);
            _mesh.SurfaceAddVertex(p);
        }
    }

    /// <summary>A burning line on the seabed between two of a cone's ray ends.</summary>
    void BurnStrip(System.Numerics.Vector2 a, System.Numerics.Vector2 b, float bright, float half, float lift = 0.08f)
    {
        float len = System.Numerics.Vector2.Distance(a, b);
        if (len < 1e-3f) return;
        var d = (b - a) / len;
        var n = new System.Numerics.Vector2(-d.Y, d.X) * half;
        // Along the strip, measured from the cone's middle so neighbouring pieces' flames run on.
        float s0 = a.X * 0.7f + a.Y * 0.7f, s1 = s0 + len;
        Vector3 P(System.Numerics.Vector2 p) => new(p.X, Floor(p) + lift, p.Y);
        void V(System.Numerics.Vector2 p, float s, float u)
        {
            _mesh.SurfaceSetColor(new Color(bright, 0f, 1f, 1f));
            _mesh.SurfaceSetUV(new Vector2(s, u));
            _mesh.SurfaceAddVertex(P(p));
        }
        V(a - n, s0, -1f);
        V(a + n, s0, 1f);
        V(b + n, s1, 1f);
        V(a - n, s0, -1f);
        V(b + n, s1, 1f);
        V(b - n, s1, -1f);
    }

    /// <summary>Embers lifting off the burns, cooling from gold to red, and a thin smoke rising and spreading.</summary>
    void Embers(float dt, float bright)
    {
        void From(System.Numerics.Vector2 at, float spread, float rate)
        {
            float n = rate * dt * bright;
            int count = (int)n + (_embers.RandRange(0f, 1f) < n - (int)n ? 1 : 0);
            float floor = Floor(at);
            for (int i = 0; i < count; i++)
            {
                var off = new Vector3(_embers.RandRange(-spread, spread), 0f, _embers.RandRange(-spread, spread));
                var p = new Vector3(at.X, floor + 0.1f, at.Y) + off;
                _embers.Emit(p, new Vector3(_embers.RandRange(-0.4f, 0.4f), _embers.RandRange(0.8f, 2f), _embers.RandRange(-0.4f, 0.4f)), _embers.RandRange(0.4f, 0.9f),
                    _embers.RandRange(0.05f, 0.1f), 0.01f, new Color(1f, 0.75f, 0.3f, 1f), new Color(1f, 0.2f, 0.05f, 0f), drag: 1.5f, buoyancy: 0.6f);
                if (_smoke.RandRange(0f, 1f) < 0.35f)
                    _smoke.Emit(p + Vector3.Up * 0.2f, new Vector3(0f, _smoke.RandRange(0.4f, 0.8f), 0f), _smoke.RandRange(0.9f, 1.5f),
                        0.2f, 0.7f, new Color(0.2f, 0.18f, 0.16f, 0.35f), new Color(0.3f, 0.28f, 0.25f, 0f), drag: 1f, buoyancy: 0.3f);
            }
        }
        foreach (var (at, radius) in _burns) From(at, radius * 0.6f, 28f);
        foreach (var (a, b) in _front) From((a + b) * 0.5f, System.Numerics.Vector2.Distance(a, b) * 0.5f, 10f);
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
