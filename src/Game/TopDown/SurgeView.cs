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
/// down to the seabed and the water there boils: bubbles well up and rise to the surface (along a cone's whole far
/// edge), with a fizz of froth at the touch. Hot glows mark her mouth and every impact, and a light at the tip lights what it touches.
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
    /// <summary>Where it touches the seabed the bubbles well up over this radius plus its ray's width.</summary>
    const float TouchRadius = 1f;

    readonly ImmediateMesh _mesh = new(), _crackle = new();
    ShaderMaterial _stroke = null!;
    /// <summary>The lightning crackling round each ray: how many strands, how far out they reach (shares of the ray's glow), how often they re-strike.</summary>
    const int CrackleStrands = 2;
    const float CrackleReach = 1.1f, CrackleStrikes = 12f;
    ShaderMaterial _beam = null!, _glow = null!;
    readonly List<MeshInstance3D> _glows = new();
    OmniLight3D _tip = null!;
    FxParticles _bubbles = null!, _froth = null!;
    LevelMap? _map;
    float _time;
    int _vertices;

    readonly List<(System.Numerics.Vector2 P, System.Numerics.Vector2 Dir, float S)> _samples = new();
    readonly List<(System.Numerics.Vector2 At, float Radius)> _touches = new();
    readonly List<(System.Numerics.Vector2 A, System.Numerics.Vector2 B)> _front = new();

    public override void _Ready()
    {
        _beam = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/light_surge.gdshader") };
        _glow = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/light_shot.gdshader") };
        AddChild(new MeshInstance3D { Mesh = _mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 16384f });
        _stroke = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/light_stroke.gdshader"), RenderPriority = 1 };
        AddChild(new MeshInstance3D { Mesh = _crackle, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 16384f });
        var quad = new QuadMesh { Size = Vector2.One };
        for (int i = 0; i < MaxGlows; i++)
        {
            var g = new MeshInstance3D { Mesh = quad, MaterialOverride = _glow, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
            g.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), i * 0.37f);
            AddChild(g);
            _glows.Add(g);
        }
        _tip = new OmniLight3D { LightColor = new Color(1f, 0.8f, 0.5f), OmniRange = 4.5f, ShadowEnabled = false, Visible = false };
        AddChild(_tip);
        _bubbles = new FxParticles(1200, additive: false, FxParticles.Bubble());
        AddChild(_bubbles);
        _froth = new FxParticles(300, additive: false);
        AddChild(_froth);
    }

    /// <summary>The seabed's height under a point (capped at the rock tops).</summary>
    float Floor(System.Numerics.Vector2 p) => _map is null ? LevelMap.SwimBand - 2f : Mathf.Min(_map.HeightAt(p), LevelMap.BlockHeight);

    public void Sync(PlaneWorld world, float dt)
    {
        _time += dt;
        _map = world.Map;
        _mesh.ClearSurfaces();
        _crackle.ClearSurfaces();
        if (GetViewport().GetCamera3D() is { } camera)
        {
            _bubbles.Tick(dt, camera.GlobalBasis);
            _froth.Tick(dt, camera.GlobalBasis);
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
        _touches.Clear();
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
        // Lightning crackles round every ray (a cone's along its middle and its edges).
        _crackle.SurfaceBegin(Mesh.PrimitiveType.Triangles, _stroke);
        _crackleVerts = 0;
        foreach (var ray in surge.Rays)
            if (!fan || ray.Fork || ray == middle || ray == main[0] || ray == main[^1]) Crackle(ray, bright * (ray.Fork ? 0.6f : 1f));
        if (_crackleVerts == 0) for (int z = 0; z < 3; z++) StrokeVertex(Vector3.Zero, Colors.Black, 0f, 0f, 0f, 0f);
        _crackle.SurfaceEnd();

        // Where it comes down on the seabed it boils the water there: a loop's far end, a ray that struck nothing, a
        // cone's far edge — bubbles well up and rise to the surface.
        foreach (var ray in surge.Rays)
        {
            if (ray.Blocked || fan && !ray.Fork) continue;
            _touches.Add((ray.Tip, TouchRadius + ray.WidthAt(ray.Length) * 3f));
        }
        Bubbles(dt, bright);

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
            g.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("lantern"), Mathf.Min(1f, 0.55f * bright));
            g.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("warmth"), 0.6f);
        }
        var tip = (middle ?? surge.Rays[0]).Tip;
        bool grounded = middle is { Blocked: false };
        _tip.Visible = true;
        _tip.Position = new Vector3(tip.X, (grounded ? Floor(tip) : LevelMap.SwimBand) + 0.6f, tip.Y);
        _tip.LightEnergy = 1.6f * bright;
    }

    int _crackleVerts;

    /// <summary>
    /// Lightning crackling round a ray: CrackleStrands thin jagged bolts running along it, each thrown out to one side
    /// and back as far as the ray's glow and a little past, re-struck CrackleStrikes times a second, painted as her
    /// strikes are (light_stroke: a broken white core on a ragged body; no wash, the beam is its own).
    /// </summary>
    void Crackle(SurgeRay ray, float bright)
    {
        Sample(ray.Points);
        if (_samples.Count < 3) return;
        int strike = (int)(_time * CrackleStrikes);
        var color = HerColor.Light.Lerp(new Color(1f, 0.95f, 0.84f), 0.3f);
        for (int strand = 0; strand < CrackleStrands; strand++)
        {
            int seed = strand * 7919 + strike * 104729 + (int)(ray.Points[0].X * 13f);
            float along = 0f;
            bool started = false;
            Vector3 last = Vector3.Zero, lastSide = Vector3.Zero;
            for (int i = 0; i < _samples.Count; i += 2)
            {
                var (p, d, s) = _samples[i];
                var n = new System.Numerics.Vector2(-d.Y, d.X);
                float reach = ray.WidthAt(s) * Spill * CrackleReach;
                // Wandering out to its side and back (a slow sway every metre or so, smoothly), jagged on top of it.
                float u = s / 1.3f;
                int k0 = (int)Mathf.Floor(u);
                float f = u - k0;
                f = f * f * (3f - 2f * f);
                float sway = Mathf.Lerp(Hash(seed + k0 * 31), Hash(seed + (k0 + 1) * 31), f);
                float off = (0.75f * sway + 0.3f * Hash(seed + i * 57)) * reach * (strand == 0 ? 1f : -1f) * Mathf.SmoothStep(0f, 0.6f, s);
                if (!float.IsFinite(off)) off = 0f;
                var at = p + n * off;
                var c = new Vector3(at.X, LevelMap.SwimBand + 0.02f, at.Y);
                if (!float.IsFinite(c.X) || !float.IsFinite(c.Z)) continue;
                float w = 0.07f * (0.6f + 0.4f * Mathf.Abs(Hash(seed + i * 17)));
                var side = new Vector3(n.X, 0f, n.Y) * w;
                if (started)
                {
                    float seg = c.DistanceTo(last);
                    float fade = bright * (0.55f + 0.45f * Mathf.Abs(Hash(seed + 5))) * Mathf.SmoothStep(0f, 0.5f, s);
                    var col = color with { A = fade };
                    for (int pass = 1; pass <= 2; pass++)
                    {
                        float k = pass == 1 ? 1f : 0.35f;
                        Vector3 l0 = last - lastSide * k, r0 = last + lastSide * k, l1 = c - side * k, r1 = c + side * k;
                        StrokeVertex(l0, col, 0f, along, pass, seed); StrokeVertex(r0, col, 1f, along, pass, seed); StrokeVertex(r1, col, 1f, along + seg, pass, seed);
                        StrokeVertex(l0, col, 0f, along, pass, seed); StrokeVertex(r1, col, 1f, along + seg, pass, seed); StrokeVertex(l1, col, 0f, along + seg, pass, seed);
                    }
                    along += seg;
                }
                started = true;
                last = c;
                lastSide = side;
            }
        }
    }

    void StrokeVertex(Vector3 p, Color c, float u, float s, float pass, float seed)
    {
        _crackle.SurfaceSetColor(c);
        _crackle.SurfaceSetUV(new Vector2(u, s));
        _crackle.SurfaceSetUV2(new Vector2(pass, (Mathf.Abs((int)seed) % 997) * 0.37f));
        _crackle.SurfaceAddVertex(p);
        _crackleVerts++;
    }

    /// <summary>A stable -1..1 for a number.</summary>
    static float Hash(int id)
    {
        uint h = (uint)(id * 374761393);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xFFFF) / 32767.5f - 1f;
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
            // runs on into the seabed).
            float fade = Mathf.SmoothStep(0f, 0.5f, s + ray.StartAt) * (dips ? 1f : 1f - Mathf.SmoothStep(length - 0.3f, length, s));
            b = bright * k * fade;
            core = w / half;
        }
    }

    /// <summary>
    /// The cone: one fan of light between neighbouring rays, from her mouth to where each ray ends. Where a ray struck
    /// nothing, the fan's far edge comes down to the seabed and the water boils there.
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

    /// <summary>
    /// Bubbles welling up where her light meets the seabed (as a geyser's do: GeyserView's BubbleFx), and a fizz of
    /// fine froth at the touch.
    /// </summary>
    void Bubbles(float dt, float bright)
    {
        void From(System.Numerics.Vector2 at, float spread, float rate)
        {
            float n = rate * dt * bright;
            int count = (int)n + (_bubbles.RandRange(0f, 1f) < n - (int)n ? 1 : 0);
            float floor = Floor(at);
            for (int i = 0; i < count; i++)
            {
                var off = new Vector3(_bubbles.RandRange(-spread, spread), 0f, _bubbles.RandRange(-spread, spread));
                var p = new Vector3(at.X, floor + 0.1f, at.Y) + off;
                var v = new Vector3(_bubbles.RandRange(-0.5f, 0.5f), _bubbles.RandRange(1.5f, 3.5f), _bubbles.RandRange(-0.5f, 0.5f));
                BubbleFx.Rise(_bubbles, p, v, _bubbles.RandRange(0.06f, 0.16f), new Color(0.95f, 1f, 1f, 1f));
                if (_froth.RandRange(0f, 1f) < 0.4f)
                    _froth.Emit(p, new Vector3(_froth.RandRange(-0.6f, 0.6f), _froth.RandRange(0.3f, 0.9f), _froth.RandRange(-0.6f, 0.6f)), _froth.RandRange(0.3f, 0.6f),
                        0.12f, 0.3f, new Color(0.95f, 1f, 1f, 0.55f), new Color(0.9f, 0.98f, 1f, 0f), drag: 3f, buoyancy: 0.5f);
            }
        }
        foreach (var (at, radius) in _touches) From(at, radius * 0.5f, 40f);
        foreach (var (a, b) in _front) From((a + b) * 0.5f, System.Numerics.Vector2.Distance(a, b) * 0.5f, 14f);
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
