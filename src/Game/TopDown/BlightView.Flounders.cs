using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Flounders on screen (docs/ADVENTURE.md §4). Healthy, a flounder (FlounderMesh, flounder.gdshader) lies on its patch
/// (a reef shelf just under her plane), its fringe rippling; shimmying, it curls left and right and throws sand off its
/// flanks at every swing. Freed, it lifts off and swims away as a flatfish does, waves running down its body. The
/// Hearthguard is a plain flat oval of ink (the sand still showing through as its skin, a gold ember in it), no head and
/// no fins, ink drops lifting off it; its sting is its own back drawn up into one continuous cone that leans at her and
/// stabs out to her plane while the oval stays where it lies.
/// </summary>
public partial class BlightView
{
    static readonly Color HearthSkin = new(0.85f, 0.66f, 0.32f);
    /// <summary>The Hearthguard's oval (half-length, half-thickness, half-width) and how far in its sting's base sits.</summary>
    static readonly Vector3 HearthOval = new(0.56f, 0.085f, 0.4f);
    const float StingBase = 0.88f;
    const int StingRings = 18, StingAround = 16;

    sealed class FlounderView
    {
        public Node3D Node = null!;
        public MeshInstance3D Fish = null!, Oval = null!, Rim = null!, Sting = null!;
        public ImmediateMesh StingMesh = null!;
        public bool Corrupted;
        public float Drip, Phase;
        /// <summary>The shimmy's swings it has thrown sand at.</summary>
        public int Swings;
    }

    readonly Dictionary<PlaneFlounder, FlounderView> _flounders = new();
    ShaderMaterial? _flounderMat;

    /// <summary>Where it lies: on its patch's floor, never deeper than a shelf under her plane.</summary>
    float FlounderBed(System.Numerics.Vector2 at) => Mathf.Max(Floor(at), LevelMap.SwimBand + AbyssReef.ShelfDepth - 0.4f) + 0.06f;

    void SyncFlounders(PlaneWorld world, float dt)
    {
        var live = new HashSet<PlaneFlounder>(world.Flounders);
        var gone = new List<PlaneFlounder>();
        foreach (var (f, view) in _flounders)
            if (!live.Contains(f))
            {
                view.Node.QueueFree();
                gone.Add(f);
            }
        foreach (var f in gone) _flounders.Remove(f);
        foreach (var f in world.Flounders)
        {
            if (!_flounders.TryGetValue(f, out var view)) _flounders[f] = view = MakeFlounder(f);
            if (view.Corrupted != f.Corrupted) Dress(view, f);
            PoseFlounder(f, view, dt);
        }
    }

    FlounderView MakeFlounder(PlaneFlounder f)
    {
        _flounderMat ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/flounder.gdshader") };
        var node = new Node3D();
        AddChild(node);
        var view = new FlounderView
        {
            Node = node,
            Fish = new MeshInstance3D { Mesh = FlounderMesh.Mesh, MaterialOverride = _flounderMat },
            Oval = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _orb, Scale = HearthOval, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off },
            // A thin skirt round the oval where it presses into the sand.
            Rim = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _orb, Scale = new Vector3(HearthOval.X * 1.1f, 0.03f, HearthOval.Z * 1.12f), Position = new Vector3(0f, -0.03f, 0f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off },
            StingMesh = new ImmediateMesh(),
        };
        view.Sting = new MeshInstance3D { Mesh = view.StingMesh, MaterialOverride = _orb, Visible = false };
        view.Fish.SetInstanceShaderParameter(SN.Of("seed"), f.Seed % 10f);
        node.AddChild(view.Fish);
        node.AddChild(view.Oval);
        node.AddChild(view.Rim);
        node.AddChild(view.Sting);
        foreach (var part in new[] { view.Oval, view.Rim, view.Sting })
        {
            part.SetInstanceShaderParameter(SN.Of("ember"), part == view.Rim ? 0f : 0.35f);
            part.SetInstanceShaderParameter(SN.Of("ember_tint"), Kin.Tint(HearthSkin));
            part.SetInstanceShaderParameter(SN.Of("wobble"), part == view.Sting ? 0.12f : 0.25f);
            part.SetInstanceShaderParameter(SN.Of("seed"), f.Seed);
            part.SetInstanceShaderParameter(SN.Of("skin"), Kin.Skin(HearthSkin, part == view.Rim ? 0.6f : 0.4f));
        }
        Dress(view, f);
        return view;
    }

    /// <summary>Healthy (or freed), the flounder; corrupted, the plain ink oval.</summary>
    void Dress(FlounderView view, PlaneFlounder f)
    {
        view.Corrupted = f.Corrupted;
        view.Fish.Visible = !f.Corrupted;
        view.Oval.Visible = view.Rim.Visible = f.Corrupted;
        view.Sting.Visible = false;
    }

    void PoseFlounder(PlaneFlounder f, FlounderView view, float dt)
    {
        float bed = FlounderBed(f.Home);
        // Freed, it lifts off its patch toward her plane as it swims away.
        float lift = f.Freed ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp(f.Left, 0f, 1f)) : 0f;
        float y = Mathf.Lerp(bed, LevelMap.SwimBand - 0.3f, lift);
        view.Node.Position = new Vector3(f.Position.X, y, f.Position.Y);
        float heading = f.Heading + f.Wriggle;
        var turn = new Basis(Vector3.Up, -heading);
        ShimmySand(f, view, bed, dt);

        if (!f.Corrupted)
        {
            // Swimming off, waves run down it and it banks a little into its wander; lying still, only its fringe ripples.
            float swim = f.Freed ? Mathf.Clamp(f.Left * 2f, 0f, 1f) : 0f;
            view.Phase += dt * (2.2f + 8f * swim);
            float bank = f.Freed ? 0.12f * Mathf.Sin(f.Left * 1.3f + f.Seed + 1.2f) : 0f;
            view.Fish.Basis = turn * new Basis(Vector3.Right, bank);
            // Lying pressed to the sand it casts no shadow of its own (only a sliver would show, striped); lifted off, it does.
            view.Fish.CastShadow = f.Freed ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off;
            view.Fish.SetInstanceShaderParameter(SN.Of("phase"), view.Phase);
            view.Fish.SetInstanceShaderParameter(SN.Of("swim"), swim);
            view.Fish.SetInstanceShaderParameter(SN.Of("bend"), f.Wriggle * 0.9f);
            view.Fish.SetInstanceShaderParameter(SN.Of("ink"), f.Turning > 0f ? 1f - f.Turning / MurkFishTuning.TurnSeconds : 0f);
            return;
        }

        view.Oval.Basis = turn.Scaled(HearthOval);
        view.Rim.Basis = turn.Scaled(view.Rim.Scale);
        // Struck by her light (flashing), worn down (its outline holding).
        float hit = f.Corruption.Since < 0.25f && f.Corruption.LastHit > 0f ? 1f - f.Corruption.Since / 0.25f : 0f;
        float worn = 1f - f.Corruption.Fraction;
        foreach (var part in new[] { view.Oval, view.Rim, view.Sting })
        {
            part.SetInstanceShaderParameter(SN.Of("hit"), hit);
            part.SetInstanceShaderParameter(SN.Of("worn"), worn);
        }
        // Its ink lifts off it in drops.
        view.Drip -= dt;
        if (view.Drip <= 0f)
        {
            view.Drip = _motes.RandRange(0.25f, 0.5f);
            var from = view.Node.Position + new Vector3(_motes.RandRange(-0.35f, 0.35f), 0.08f, _motes.RandRange(-0.25f, 0.25f));
            _droplets.Emit(from, Vector3.Up * _motes.RandRange(0.2f, 0.4f), _motes.RandRange(1f, 1.6f), 0.05f, 0.02f, new Color(0.03f, 0.01f, 0.05f, 0.9f), new Color(0.25f, 0.08f, 0.4f, 0f), drag: 1f, buoyancy: 0.6f);
        }
        PoseSting(f, view, heading);
    }

    /// <summary>
    /// Shimmying, it throws sand off the flank it swings to at each swing (a spray of grains falling back and a slow puff
    /// of dust), and a few grains off its edges between.
    /// </summary>
    void ShimmySand(PlaneFlounder f, FlounderView view, float bed, float dt)
    {
        if (f.Stage != FlounderStage.Shimmy || f.Freed)
        {
            view.Swings = 0;
            return;
        }
        var sand = new Color(0.86f, 0.77f, 0.56f, 0.95f);
        var sandDark = new Color(0.62f, 0.52f, 0.34f, 0.95f);
        while (view.Swings < FlounderTuning.Swings && f.Timer >= FlounderTuning.SwingAt(view.Swings))
        {
            float side = FlounderTuning.SwingSide(view.Swings++);
            float flank = f.Heading + side * Mathf.Pi / 2f;
            var dir = new Vector3(Mathf.Cos(flank), 0f, Mathf.Sin(flank));
            var along = new Vector3(Mathf.Cos(f.Heading), 0f, Mathf.Sin(f.Heading));
            for (int i = 0; i < 18; i++)
            {
                var at = view.Node.Position + dir * 0.4f + along * _motes.RandRange(-0.4f, 0.4f);
                at.Y = bed + 0.04f;
                var v = dir * _motes.RandRange(0.5f, 1.6f) + along * _motes.RandRange(-0.5f, 0.5f) + Vector3.Up * _motes.RandRange(0.6f, 1.4f);
                var c = sand.Lerp(sandDark, _motes.RandRange(0f, 1f));
                _droplets.Emit(at, v, _motes.RandRange(0.6f, 1f), _motes.RandRange(0.05f, 0.08f), 0.03f, c, c with { A = 0f }, drag: 1.2f, buoyancy: -2.6f);
            }
            for (int i = 0; i < 3; i++)
            {
                var at = view.Node.Position + dir * 0.5f + along * _motes.RandRange(-0.3f, 0.3f);
                at.Y = bed + 0.08f;
                _droplets.Emit(at, dir * _motes.RandRange(0.3f, 0.7f) + Vector3.Up * 0.2f, _motes.RandRange(1.2f, 1.8f), 0.22f, 0.6f, sand with { A = 0.5f }, sand with { A = 0f }, drag: 2f);
            }
        }
        if (GD.Randf() < dt * 20f)
        {
            float a = _motes.RandRange(0f, Mathf.Tau);
            var rim = new Vector3(Mathf.Cos(a) * 0.55f, 0f, Mathf.Sin(a) * 0.4f);
            var at = view.Node.Position + new Basis(Vector3.Up, -f.Heading) * rim;
            at.Y = bed + 0.04f;
            _droplets.Emit(at, rim.Normalized() * _motes.RandRange(0.2f, 0.5f) + Vector3.Up * _motes.RandRange(0.3f, 0.7f), _motes.RandRange(0.5f, 0.8f), 0.035f, 0.02f, sand, sand with { A = 0f }, drag: 1.5f, buoyancy: -2f);
        }
    }

    /// <summary>
    /// The sting: its back drawn up into one cone, its base the oval's own back, rising off it and bending toward her,
    /// its tip leaning at her as it draws (the telegraph), then out to the stab's tip at her plane, and back.
    /// </summary>
    void PoseSting(PlaneFlounder f, FlounderView view, float heading)
    {
        float drawn = f.Needle;
        view.Sting.Visible = drawn > 0.02f || f.Reach > 0.05f;
        if (!view.Sting.Visible) return;
        var aim = new Vector3(f.Aim.X, 0f, f.Aim.Y);
        float rise = LevelMap.SwimBand - view.Node.Position.Y;
        // Drawn, its tip stands a little up and leaning at her; stabbing, out along the stab to her plane.
        float reach = Mathf.Max(0.75f * drawn, f.Reach);
        float up = 0.6f * drawn + Mathf.Clamp((f.Reach - 0.75f) / 1f, 0f, 1f) * (rise - 0.6f * drawn);
        var root = new Vector3(0f, 0.02f, 0f);
        var tip = root + aim * reach + Vector3.Up * up;
        float span = (tip - root).Length();
        // Off its back straight up, then over toward her (a quadratic curve).
        var bendAt = root + Vector3.Up * Mathf.Min(0.5f * span, 0.5f);
        var fwd = new Vector3(Mathf.Cos(heading), 0f, Mathf.Sin(heading));
        var side = new Vector3(-Mathf.Sin(heading), 0f, Mathf.Cos(heading));
        float a = HearthOval.X * StingBase, b = HearthOval.Z * StingBase;

        var m = view.StingMesh;
        m.ClearSurfaces();
        m.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        var ring = new Vector3[StingRings + 1, StingAround];
        var norm = new Vector3[StingRings + 1, StingAround];
        for (int i = 0; i <= StingRings; i++)
        {
            float u = i / (float)StingRings;
            var c = (1f - u) * (1f - u) * root + 2f * u * (1f - u) * bendAt + u * u * tip;
            var t = (2f * (1f - u) * (bendAt - root) + 2f * u * (tip - bendAt)).Normalized();
            // The cross-section's axes: the oval's own at the base, kept square to the curve.
            var x = fwd - t * fwd.Dot(t);
            if (x.LengthSquared() < 1e-4f) x = side.Cross(t);
            x = x.Normalized();
            var z = x.Cross(t).Normalized();
            // The oval's back at the base, narrowing to a point; rounder as it goes.
            float k = Mathf.Pow(1f - u, 2.4f);
            float ra = a * k, rb = Mathf.Lerp(b, a, Mathf.SmoothStep(0f, 0.5f, u)) * k;
            for (int j = 0; j < StingAround; j++)
            {
                float th = Mathf.Tau * j / StingAround;
                float cs = Mathf.Cos(th), sn = Mathf.Sin(th);
                ring[i, j] = c + x * (cs * ra) + z * (sn * rb);
                norm[i, j] = (x * (cs * rb) + z * (sn * ra) + t * 0.4f * Mathf.Max(ra, rb)).Normalized();
            }
        }
        void V(int i, int j)
        {
            m.SurfaceSetNormal(norm[i, j]);
            m.SurfaceAddVertex(ring[i, j]);
        }
        for (int i = 0; i < StingRings; i++)
        for (int j = 0; j < StingAround; j++)
        {
            int jn = (j + 1) % StingAround;
            V(i, j); V(i, jn); V(i + 1, j);
            V(i, jn); V(i + 1, jn); V(i + 1, j);
        }
        m.SurfaceEnd();
        // Drawing, its ember wakes in it (the telegraph).
        float tell = f.Stage == FlounderStage.Windup ? drawn : 0f;
        view.Sting.SetInstanceShaderParameter(SN.Of("swell"), 0.15f * tell);
        view.Sting.SetInstanceShaderParameter(SN.Of("ember"), 0.35f + 0.9f * tell);
        view.Oval.SetInstanceShaderParameter(SN.Of("ember"), 0.35f + 0.9f * tell);
    }

    void ClearFlounders()
    {
        foreach (var view in _flounders.Values) view.Node.QueueFree();
        _flounders.Clear();
    }
}
