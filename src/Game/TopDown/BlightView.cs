using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The corruption's creatures and works on screen (docs/CORRUPTION.md), presentation only: Blightroots (a blackened
/// stump crowned with a pulsing ink sac; its ring of ink; its burst into golden motes and a wave of cleansing),
/// gloomvines (glossy ink tubes rising from the floor to her plane, coiling round her), valves (walls of rolling ink
/// smoke), murklings (wobbling dark orbs with a stolen ember, budding from the floor) and their throws. Everything
/// corrupted shares one look: ink-black, light-drinking, slightly glossy, with a cold violet rim.
/// </summary>
public partial class BlightView : Node3D
{
    static readonly Color Gold = new(1.25f, 0.85f, 0.38f);
    static readonly Color Ink = new(0.02f, 0.012f, 0.035f);
    const float SacHeight = LevelMap.SwimBand + 0.15f;

    sealed class Root
    {
        public Node3D Node = null!;
        public MeshInstance3D Stump = null!, Sac = null!, Ring = null!;
        public bool Burst;
        public float Since;
    }

    sealed class Wall
    {
        public Node3D Node = null!;
        public MeshInstance3D[] Sheets = null!;
        public bool Gone;
        public float Since;
    }

    sealed class Ripple
    {
        public MeshInstance3D Mesh = null!;
        public float Age, Life, From, To;
        public Color Tint;
    }

    readonly List<Root> _roots = new();
    readonly List<Wall> _walls = new();
    readonly List<MeshInstance3D> _murks = new(), _throws = new();
    readonly List<Ripple> _ripples = new();
    ShaderMaterial _orb = null!, _sacMat = null!, _ring = null!, _valve = null!, _vine = null!;
    readonly SphereMesh _sphere = new() { Radius = 1f, Height = 2f, RadialSegments = 24, Rings = 12 };
    readonly QuadMesh _quad = new() { Size = new Vector2(2f, 2f), Orientation = PlaneMesh.OrientationEnum.Y };
    MeshInstance3D _vines = null!;
    ImmediateMesh _vineMesh = null!;
    FxParticles _motes = null!, _droplets = null!;
    LevelMap? _map;
    float _time;

    public override void _Ready()
    {
        _orb = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/corruption_orb.gdshader") };
        _sacMat = new ShaderMaterial { Shader = _orb.Shader };
        _sacMat.SetShaderParameter("ember_color", new Color(0.9f, 0.25f, 0.55f));
        _ring = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/ink_ring.gdshader"), RenderPriority = 1 };
        _valve = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/valve_ink.gdshader"), RenderPriority = 1 };
        _vine = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/gloomvine.gdshader") };
        _vineMesh = new ImmediateMesh();
        _vines = new MeshInstance3D { Mesh = _vineMesh, MaterialOverride = _vine, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_vines);
        _motes = new FxParticles(768, additive: true);
        AddChild(_motes);
        _droplets = new FxParticles(512, additive: false);
        AddChild(_droplets);
    }

    float Floor(System.Numerics.Vector2 p) => _map is null ? -1f : Mathf.Min(_map.HeightAt(p), LevelMap.BlockHeight);

    /// <summary>A new level: its Blightroots and valves.</summary>
    public void Show(PlaneWorld world)
    {
        _map = world.Map;
        foreach (var r in _roots) r.Node.QueueFree();
        foreach (var w in _walls) w.Node.QueueFree();
        foreach (var r in _ripples) r.Mesh.QueueFree();
        _roots.Clear();
        _walls.Clear();
        _ripples.Clear();
        _motes.Clear();
        _droplets.Clear();
        foreach (var root in world.Blightroots) _roots.Add(MakeRoot(root));
        foreach (var valve in world.Valves) _walls.Add(MakeWall(valve));
    }

    Root MakeRoot(Blightroot root)
    {
        float floor = Floor(root.Position);
        var node = new Node3D { Position = new Vector3(root.Position.X, 0f, root.Position.Y) };
        AddChild(node);
        // The stump: from the floor up to just under her plane, blackened, flaring into roots at its foot.
        float height = SacHeight - floor - 0.3f;
        var stump = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.55f, Height = height, RadialSegments = 10, Rings = 3 },
            MaterialOverride = _orb,
            Position = new Vector3(0f, floor + height * 0.5f, 0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        stump.SetInstanceShaderParameter("ember", 0f);
        stump.SetInstanceShaderParameter("wobble", 0f);
        node.AddChild(stump);
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.Tau / 5f + 0.4f;
            var spur = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.03f, Height = 1.4f, RadialSegments = 6, Rings = 1 },
                MaterialOverride = _orb,
                Position = new Vector3(Mathf.Cos(a) * 0.55f, floor + 0.15f, Mathf.Sin(a) * 0.55f),
                Rotation = new Vector3(0f, -a, 1.2f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            spur.SetInstanceShaderParameter("ember", 0f);
            spur.SetInstanceShaderParameter("wobble", 0f);
            stump.AddChild(spur);
            spur.Position -= stump.Position;
        }
        // The sac: a glossy membrane of ink round a stolen light.
        var sac = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _sacMat, Position = new Vector3(0f, SacHeight, 0f), Scale = Vector3.One * 0.75f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        sac.SetInstanceShaderParameter("seed", root.Position.X * 0.37f);
        node.AddChild(sac);
        var ring = new MeshInstance3D { Mesh = _quad, MaterialOverride = _ring, Visible = false, Position = new Vector3(0f, LevelMap.SwimBand - 0.35f, 0f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        node.AddChild(ring);
        return new Root { Node = node, Stump = stump, Sac = sac, Ring = ring };
    }

    Wall MakeWall(Valve valve)
    {
        float floor = Floor(valve.Center);
        float top = LevelMap.SwimBand + 1.8f, height = top - floor;
        var node = new Node3D
        {
            Position = new Vector3(valve.Center.X, floor + height * 0.5f, valve.Center.Y),
            // The sheets face along the corridor (their plane runs across it).
            Basis = new Basis(Vector3.Up, Mathf.Atan2(valve.Normal.X, valve.Normal.Y)),
        };
        AddChild(node);
        var sheets = new MeshInstance3D[3];
        for (int i = 0; i < sheets.Length; i++)
        {
            sheets[i] = new MeshInstance3D
            {
                Mesh = new QuadMesh { Size = new Vector2(valve.HalfWidth * 2f, height) },
                MaterialOverride = _valve,
                Position = new Vector3(0f, 0f, (i - 1) * valve.HalfThickness * 0.8f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            sheets[i].SetInstanceShaderParameter("seed", i * 3.7f + valve.Center.X * 0.1f);
            node.AddChild(sheets[i]);
        }
        return new Wall { Node = node, Sheets = sheets };
    }

    public void Sync(PlaneWorld world, float dt, Camera3D? camera)
    {
        _time += dt;
        SyncRoots(world, dt);
        SyncWalls(world, dt);
        SyncMurklings(world);
        SyncVines(world);
        StepRipples(dt);
        if (camera is not null)
        {
            _motes.Tick(dt, camera.GlobalBasis);
            _droplets.Tick(dt, camera.GlobalBasis);
        }
    }

    void SyncRoots(PlaneWorld world, float dt)
    {
        for (int i = 0; i < _roots.Count && i < world.Blightroots.Count; i++)
        {
            var view = _roots[i];
            var root = world.Blightroots[i];
            if (!root.Alive)
            {
                if (!view.Burst)
                {
                    view.Burst = true;
                    view.Since = 0f;
                    Burst(root.Position);
                }
                // The stump shrivels into the floor, its sac gone in specks.
                view.Since += dt;
                float k = Mathf.Clamp(view.Since / 1.4f, 0f, 1f);
                view.Sac.SetInstanceShaderParameter("fade", 1f - Mathf.Clamp(view.Since / 0.35f, 0f, 1f));
                view.Stump.SetInstanceShaderParameter("fade", 1f - k);
                view.Stump.Scale = new Vector3(1f - 0.5f * k, 1f - 0.6f * k, 1f - 0.5f * k);
                view.Ring.Visible = false;
                if (k >= 1f) view.Node.Visible = false;
                continue;
            }
            // Breathing; inflating on the telegraph; shrinking as it starves (its ring cleansed).
            float starve = Mathf.Clamp(root.RingDensity / 0.45f, 0f, 1f);
            float breathe = 1f + 0.06f * Mathf.Sin(_time * 2.1f + i);
            float size = 0.75f * breathe * (0.6f + 0.4f * starve) * (1f + 0.45f * root.Inflate);
            view.Sac.Scale = Vector3.One * size;
            view.Sac.SetInstanceShaderParameter("swell", root.Inflate);
            view.Sac.SetInstanceShaderParameter("ember", 0.25f + 0.9f * root.Inflate);
            view.Sac.SetInstanceShaderParameter("wobble", 0.6f + 0.8f * root.Inflate);
            // Its ring of ink running out over its reach.
            view.Ring.Visible = root.PulseRadius >= 0f;
            if (view.Ring.Visible)
            {
                float r = Mathf.Max(root.PulseRadius, 0.1f);
                view.Ring.Scale = Vector3.One * (r / 0.8f);
                float fadeOut = 1f - Mathf.Clamp(r / BlightTuning.Reach, 0f, 1f);
                view.Ring.SetInstanceShaderParameter("tint", new Color(Ink.R, Ink.G, Ink.B, 0.85f * (0.35f + 0.65f * fadeOut)));
                view.Ring.SetInstanceShaderParameter("width", 0.09f + 0.4f / r);
                view.Ring.SetInstanceShaderParameter("seed", i * 1.7f);
            }
        }
    }

    /// <summary>A Blightroot bursts: golden motes, ink droplets and a ring of cleansing light running out over its reach.</summary>
    void Burst(System.Numerics.Vector2 at)
    {
        var p = new Vector3(at.X, SacHeight, at.Y);
        for (int i = 0; i < 90; i++)
        {
            var dir = _motes.RandDir();
            dir.Y = Mathf.Abs(dir.Y) * 0.8f + 0.2f;
            _motes.Emit(p + dir * 0.4f, dir * _motes.RandRange(1.5f, 5.5f), _motes.RandRange(1.0f, 2.2f), _motes.RandRange(0.12f, 0.3f), 0.02f, Gold, new Color(1f, 0.6f, 0.2f, 0f), drag: 1.6f, buoyancy: 0.9f);
        }
        _droplets.Burst(p, Vector3.Up, 26, 3.5f, 1f, 0.7f, 0.22f, 0.05f, new Color(0.02f, 0.01f, 0.04f, 0.9f), new Color(0.05f, 0.02f, 0.08f, 0f), drag: 2.5f);
        AddRipple(at, Floor(at) + 0.08f, 0.3f, BlightTuning.Reach, BlightTuning.BurstSeconds + 0.3f, new Color(Gold.R * 1.3f, Gold.G * 1.3f, Gold.B, 0.9f));
    }

    void SyncWalls(PlaneWorld world, float dt)
    {
        for (int i = 0; i < _walls.Count && i < world.Valves.Count; i++)
        {
            var view = _walls[i];
            var valve = world.Valves[i];
            if (valve.Cleared && !view.Gone)
            {
                view.Gone = true;
                view.Since = 0f;
                var at = new Vector3(valve.Center.X, LevelMap.SwimBand, valve.Center.Y);
                var across = new Vector3(valve.Across.X, 0f, valve.Across.Y);
                for (int k = 0; k < 50; k++)
                {
                    var from = at + across * _motes.RandRange(-valve.HalfWidth, valve.HalfWidth) + Vector3.Up * _motes.RandRange(-0.6f, 1.2f);
                    _motes.Emit(from, new Vector3(0f, _motes.RandRange(0.6f, 2f), 0f) + _motes.RandDir() * 0.6f, _motes.RandRange(0.8f, 1.6f), _motes.RandRange(0.1f, 0.22f), 0.02f, Gold, new Color(1f, 0.6f, 0.2f, 0f), drag: 1.2f, buoyancy: 0.6f);
                }
            }
            float fade = view.Gone ? 1f - Mathf.Clamp((view.Since += dt) / 0.8f, 0f, 1f) : 1f;
            view.Node.Visible = fade > 0f;
            foreach (var sheet in view.Sheets)
            {
                sheet.SetInstanceShaderParameter("integrity", Mathf.Max(valve.Integrity, 0f));
                sheet.SetInstanceShaderParameter("fade", fade);
            }
        }
    }

    void SyncMurklings(PlaneWorld world)
    {
        while (_murks.Count < world.Murklings.Count)
        {
            var m = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _orb, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(m);
            _murks.Add(m);
        }
        for (int i = 0; i < _murks.Count; i++)
        {
            var view = _murks[i];
            view.Visible = i < world.Murklings.Count;
            if (!view.Visible) continue;
            var m = world.Murklings[i];
            float y = LevelMap.SwimBand + 0.08f * Mathf.Sin(_time * 2.3f + m.Seed);
            float scale = BlightTuning.MurkRadius;
            float ember = 0.45f;
            if (m.Budding)
            {
                // Budding: a dark bead swelling up out of the floor.
                float k = 1f - m.Bud / BlightTuning.BudSeconds;
                float e = k * k * (3f - 2f * k);
                y = Mathf.Lerp(Floor(m.Position) + 0.1f, y, e);
                scale *= 0.25f + 0.75f * e;
                ember = 0.1f + 0.35f * e;
            }
            else if (m.Clinging)
            {
                // Clinging: it drinks her light, its stolen ember brightening.
                scale *= 0.75f;
                ember = 0.9f + 0.3f * Mathf.Sin(_time * 9f + m.Seed);
            }
            view.Position = new Vector3(m.Position.X, y, m.Position.Y);
            view.Scale = Vector3.One * scale;
            view.SetInstanceShaderParameter("seed", m.Seed);
            view.SetInstanceShaderParameter("ember", ember);
            view.SetInstanceShaderParameter("wobble", 1.2f);
            view.SetInstanceShaderParameter("fade", 1f);
        }

        while (_throws.Count < world.MurkShots.Count)
        {
            var t = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _orb, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            t.SetInstanceShaderParameter("ember", 0f);
            t.SetInstanceShaderParameter("wobble", 0.5f);
            AddChild(t);
            _throws.Add(t);
        }
        for (int i = 0; i < _throws.Count; i++)
        {
            var view = _throws[i];
            view.Visible = i < world.MurkShots.Count;
            if (!view.Visible) continue;
            var s = world.MurkShots[i];
            view.Position = new Vector3(s.Position.X, LevelMap.SwimBand, s.Position.Y);
            var along = new Vector3(s.Velocity.X, 0f, s.Velocity.Y);
            // Stretched along its flight, a smear of dark.
            view.Basis = along.LengthSquared() > 1e-4f ? Basis.LookingAt(along.Normalized(), Vector3.Up).Scaled(new Vector3(1f, 1f, 1.8f)) : Basis.Identity;
            view.Scale = new Vector3(BlightTuning.ThrowRadius * 1.2f, BlightTuning.ThrowRadius * 1.2f, BlightTuning.ThrowRadius * 2.2f);
            if (((int)(_time * 60f) + i) % 3 == 0)
                _droplets.Emit(view.Position, Vector3.Up * 0.2f, 0.35f, 0.16f, 0.04f, new Color(0.03f, 0.01f, 0.06f, 0.7f), new Color(0.05f, 0.02f, 0.1f, 0f), drag: 1f);
        }
    }

    /// <summary>The vines: tubes from the floor at their root rising to her plane, tapering to the tip; a coil wraps her.</summary>
    void SyncVines(PlaneWorld world)
    {
        _vineMesh.ClearSurfaces();
        bool any = false;
        foreach (var vine in world.Vines) any |= vine.Points.Count >= 2;
        if (!any) return;
        _vineMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        var her = new Vector3(world.Player.Position.X, LevelMap.SwimBand, world.Player.Position.Y);
        foreach (var vine in world.Vines)
        {
            if (vine.Points.Count < 2) continue;
            float gone = vine.Dissolving >= 0f ? vine.Dissolving / BlightTuning.DissolveSeconds : 0f;
            float rootFloor = Floor(vine.Root.Position) + 0.1f;
            var path = new List<Vector3>(vine.Points.Count + 24);
            for (int k = 0; k < vine.Points.Count; k++)
            {
                var q = vine.Points[k];
                float along = k * BlightTuning.Segment;
                float rise = Mathf.SmoothStep(0f, 1f, along / 1.8f);
                float sway = Mathf.Sin(_time * 1.4f + k * 0.5f + vine.Curl * 3f) * 0.06f * rise;
                path.Add(new Vector3(q.X + sway, Mathf.Lerp(rootFloor, LevelMap.SwimBand - 0.15f, rise), q.Y - sway));
            }
            if (vine.Coiling)
            {
                // The coil: a helix round her, closing as it holds.
                var tip = path[^1];
                float a0 = Mathf.Atan2(tip.Z - her.Z, tip.X - her.X);
                for (int k = 1; k <= 20; k++)
                {
                    float a = a0 + k * 0.42f;
                    float r = Mathf.Lerp(0.75f, 0.5f, k / 20f);
                    path.Add(her + new Vector3(Mathf.Cos(a) * r, -0.25f + k * 0.03f, Mathf.Sin(a) * r));
                }
            }
            Tube(path, gone, vine.Coiling ? 1f : 0f);
        }
        _vineMesh.SurfaceEnd();
    }

    void Tube(List<Vector3> path, float gone, float coil)
    {
        const int sides = 6;
        int n = path.Count;
        Vector3[] prevRing = new Vector3[sides], prevNormal = new Vector3[sides];
        for (int k = 0; k < n; k++)
        {
            var fwd = (path[Mathf.Min(k + 1, n - 1)] - path[Mathf.Max(k - 1, 0)]).Normalized();
            var side = fwd.Cross(Vector3.Up);
            if (side.LengthSquared() < 1e-4f) side = Vector3.Right;
            side = side.Normalized();
            var up = side.Cross(fwd).Normalized();
            float t = (float)k / (n - 1);
            float radius = Mathf.Lerp(0.15f, 0.035f, t) * (1f - 0.5f * gone);
            var ring = new Vector3[sides];
            var normal = new Vector3[sides];
            for (int s = 0; s < sides; s++)
            {
                float a = s * Mathf.Tau / sides;
                normal[s] = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                ring[s] = path[k] + normal[s] * radius;
            }
            if (k > 0)
            {
                var c0 = new Color((float)(k - 1) / (n - 1), gone, coil);
                var c1 = new Color(t, gone, coil);
                for (int s = 0; s < sides; s++)
                {
                    int s1 = (s + 1) % sides;
                    Vert(prevRing[s], prevNormal[s], c0, s, k - 1);
                    Vert(ring[s], normal[s], c1, s, k);
                    Vert(ring[s1], normal[s1], c1, s + 1, k);
                    Vert(prevRing[s], prevNormal[s], c0, s, k - 1);
                    Vert(ring[s1], normal[s1], c1, s + 1, k);
                    Vert(prevRing[s1], prevNormal[s1], c0, s + 1, k - 1);
                }
            }
            prevRing = ring;
            prevNormal = normal;
        }
    }

    void Vert(Vector3 at, Vector3 normal, Color color, int s, int k)
    {
        _vineMesh.SurfaceSetNormal(normal);
        _vineMesh.SurfaceSetColor(color);
        _vineMesh.SurfaceSetUV(new Vector2(s / 6f, k / 40f));
        _vineMesh.SurfaceAddVertex(at);
    }

    // ───────────────────────── events ─────────────────────────

    /// <summary>The war's moments: a bud's ripple, a murkling bursting (and its ember blooming), a sac drinking a bubble.</summary>
    public void OnEvent(in PlaneEvent e)
    {
        var at = new Vector3(e.Position.X, LevelMap.SwimBand, e.Position.Y);
        switch (e.Type)
        {
            case PlaneEventType.MurklingBud:
                AddRipple(e.Position, Floor(e.Position) + 0.06f, 0.2f, 1.6f, BlightTuning.BudSeconds, new Color(Ink.R, Ink.G, Ink.B, 0.9f));
                break;
            case PlaneEventType.MurklingBurst:
                _droplets.Burst(at, Vector3.Up, 18, 3f, 1f, 0.55f, 0.16f, 0.04f, new Color(0.02f, 0.01f, 0.04f, 0.95f), new Color(0.06f, 0.02f, 0.1f, 0f), drag: 3f);
                if (e.Size > 0f)
                {
                    // Its stolen ember falls and blooms a patch of colour.
                    AddRipple(e.Position, Floor(e.Position) + 0.08f, 0.2f, e.Size, 0.9f, new Color(Gold.R * 1.4f, Gold.G * 1.3f, Gold.B, 0.85f));
                    for (int i = 0; i < 24; i++)
                        _motes.Emit(at, _motes.RandDir() * _motes.RandRange(0.6f, 2.4f) + Vector3.Down * 1.2f, _motes.RandRange(0.6f, 1.2f), _motes.RandRange(0.08f, 0.18f), 0.02f, Gold, new Color(1f, 0.6f, 0.2f, 0f), drag: 2f, buoyancy: 0.4f);
                }
                break;
            case PlaneEventType.BlightrootDrank:
                // A bubble's light drawn into the sac: a brief inward spiral of motes.
                for (int i = 0; i < 12; i++)
                {
                    var from = at + _motes.RandDir() * 1.2f;
                    _motes.Emit(from, (at - from) * 2.4f, 0.4f, 0.12f, 0.02f, new Color(1f, 0.8f, 0.4f), new Color(0.6f, 0.2f, 0.8f, 0f), drag: 0.5f);
                }
                break;
        }
    }

    void AddRipple(System.Numerics.Vector2 at, float y, float from, float to, float life, Color tint)
    {
        var mesh = new MeshInstance3D { Mesh = _quad, MaterialOverride = _ring, Position = new Vector3(at.X, y, at.Y), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(mesh);
        _ripples.Add(new Ripple { Mesh = mesh, Life = life, From = from, To = to, Tint = tint });
    }

    void StepRipples(float dt)
    {
        for (int i = _ripples.Count - 1; i >= 0; i--)
        {
            var r = _ripples[i];
            r.Age += dt;
            float k = r.Age / r.Life;
            if (k >= 1f)
            {
                r.Mesh.QueueFree();
                _ripples.RemoveAt(i);
                continue;
            }
            float e = 1f - (1f - k) * (1f - k);
            float radius = Mathf.Lerp(r.From, r.To, e);
            r.Mesh.Scale = Vector3.One * (radius / 0.8f);
            r.Mesh.SetInstanceShaderParameter("tint", new Color(r.Tint.R, r.Tint.G, r.Tint.B, r.Tint.A * (1f - k)));
            r.Mesh.SetInstanceShaderParameter("width", 0.1f + 0.25f / Mathf.Max(radius, 0.2f));
        }
    }
}
