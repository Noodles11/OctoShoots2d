using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The Blightroot as it is drawn (docs/CORRUPTION.md): a captive octopus sitting on the seabed, its mantle raised to her
/// plane (the target her light drains), its eight arms splayed flat on the ground and pinned there by chains of ink
/// that run over them into little ink wells; dark droplets drain off its arms into the wells. On the pulse its mantle
/// swells, the chains pull taut and its arms flinch. Its two gloomvines are chains torn loose that lash at her. Freed,
/// its ink pops off, the chains snap, and it curls its arms in, pushes off the floor and jets up and away, healthy again,
/// trailing bubbles. It has no eyes.
/// </summary>
public partial class BlightView
{
    const int Arms = 8;
    /// <summary>Where along an arm the chains pin it (shares of its length), and how far either side their wells sit.</summary>
    static readonly float[] PinAt = { 0.32f, 0.58f, 0.84f };
    const float PinSpread = 0.5f, LinkSpacing = 0.13f;
    /// <summary>The freed octopus jets up and away over this long.</summary>
    const float FleeSeconds = 2.6f;
    /// <summary>Its colour once freed: a healthy reef octopus.</summary>
    static readonly Color Healthy = new(0.95f, 0.4f, 0.28f);

    sealed class Root
    {
        public Node3D Node = null!;
        public MeshInstance3D Mantle = null!, Ring = null!;
        public Node3D[] Arms = null!;
        public float[] Angle = null!, Length = null!, Phase = null!;
        public float Floor;
        public bool Burst;
        public float Since, Drip;
    }

    MultiMeshInstance3D _chains = null!;
    readonly List<(Transform3D At, Color Look)> _links = new();

    void ReadyChains()
    {
        var link = new TorusMesh { InnerRadius = 0.028f, OuterRadius = 0.07f, Rings = 12, RingSegments = 6 };
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = link };
        _chains = new MultiMeshInstance3D
        {
            Multimesh = mm,
            MaterialOverride = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/ink_chain.gdshader") },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = 200f,
        };
        AddChild(_chains);
    }

    /// <summary>One arm: a tapering tube lying along +X from the mantle's foot, its tip curling round.</summary>
    static ArrayMesh ArmMesh(float length, float curl)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        const int segs = 18, sides = 7;
        var centre = new Vector3[segs + 1];
        for (int k = 0; k <= segs; k++) centre[k] = ArmPoint(k / (float)segs, length, curl);
        Vector3[]? prev = null;
        for (int k = 0; k <= segs; k++)
        {
            float t = k / (float)segs;
            var fwd = (centre[Mathf.Min(k + 1, segs)] - centre[Mathf.Max(k - 1, 0)]).Normalized();
            var side = fwd.Cross(Vector3.Up).Normalized();
            var up = side.Cross(fwd).Normalized();
            float r = ArmRadius(t);
            var ring = new Vector3[sides];
            for (int s = 0; s < sides; s++)
            {
                float a = s * Mathf.Tau / sides;
                // Flattened against the floor, rounder on top.
                ring[s] = centre[k] + side * Mathf.Cos(a) * r + up * Mathf.Sin(a) * r * (Mathf.Sin(a) > 0f ? 0.8f : 0.45f);
            }
            if (prev is not null)
                for (int s = 0; s < sides; s++)
                {
                    int s1 = (s + 1) % sides;
                    st.AddVertex(prev[s]); st.AddVertex(ring[s]); st.AddVertex(ring[s1]);
                    st.AddVertex(prev[s]); st.AddVertex(ring[s1]); st.AddVertex(prev[s1]);
                }
            prev = ring;
        }
        st.GenerateNormals();
        return st.Commit();
    }

    /// <summary>An arm's radius at a share of its length (thick at the mantle, thin at the tip).</summary>
    static float ArmRadius(float t) => Mathf.Lerp(0.38f, 0.05f, Mathf.Pow(t, 0.75f));

    /// <summary>A point along an arm (local: out along +X, a little off the floor), its last third curling.</summary>
    static Vector3 ArmPoint(float t, float length, float curl)
    {
        float x = 0.3f + t * length;
        var p = new Vector3(x, 0.2f * (1f - t) + 0.08f + 0.05f * Mathf.Sin(t * 5f), 0f);
        if (t > 0.65f)
        {
            float u = (t - 0.65f) / 0.35f;
            p.Z += curl * u * u * length * 0.28f;
            p.X -= u * u * length * 0.08f;
        }
        return p;
    }

    Root MakeRoot(Blightroot root)
    {
        float floor = Floor(root.Position);
        var node = new Node3D { Position = new Vector3(root.Position.X, 0f, root.Position.Y) };
        AddChild(node);
        var view = new Root { Node = node, Floor = floor, Arms = new Node3D[Arms], Angle = new float[Arms], Length = new float[Arms], Phase = new float[Arms] };
        float seed = root.Position.X * 0.37f + root.Position.Y * 0.11f;
        for (int i = 0; i < Arms; i++)
        {
            view.Angle[i] = i * Mathf.Tau / Arms + 0.25f * Mathf.Sin(seed + i * 2.3f);
            view.Length[i] = 2.1f + 0.6f * (0.5f + 0.5f * Mathf.Sin(seed * 1.7f + i * 1.9f));
            view.Phase[i] = seed + i * 1.37f;
            var pivot = new Node3D { Position = new Vector3(0f, floor, 0f) };
            node.AddChild(pivot);
            var arm = new MeshInstance3D { Mesh = ArmMesh(view.Length[i], i % 2 == 0 ? 1f : -1f), MaterialOverride = _orb, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            Skin(arm, Kin.Blightroot, 0.7f);
            pivot.AddChild(arm);
            view.Arms[i] = pivot;
        }
        // The mantle: raised from the floor up to her plane, leaning back a little — the target her light drains.
        view.Mantle = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _sacMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        view.Mantle.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), seed);
        Skin(view.Mantle, Kin.Blightroot, 0.6f);
        view.Mantle.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember_tint"), Kin.Tint(new Color(0.55f, 0.8f, 1f)));
        node.AddChild(view.Mantle);
        // The ink wells its chains run into.
        for (int i = 0; i < Arms; i++)
            foreach (float at in PinAt)
                for (int side = -1; side <= 1; side += 2)
                {
                    var p = PinWell(view, i, at, side, 0f);
                    var well = new MeshInstance3D
                    {
                        Mesh = new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.12f, Height = 0.05f, RadialSegments = 8, Rings = 1 },
                        MaterialOverride = _orb,
                        Position = p - node.Position,
                        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    };
                    Skin(well, Kin.Blightroot, 0.3f);
                    node.AddChild(well);
                }
        view.Ring = new MeshInstance3D { Mesh = _quad, MaterialOverride = _ring, Visible = false, Position = new Vector3(0f, LevelMap.SwimBand - 0.35f, 0f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        node.AddChild(view.Ring);
        Place(view, 0f, 0f, 1f);
        return view;
    }

    static void Skin(MeshInstance3D mesh, Color kin, float amount)
    {
        mesh.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember"), 0f);
        mesh.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("wobble"), 0.3f);
        mesh.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("skin"), Kin.Skin(kin, amount));
    }

    /// <summary>An arm's transform now: its angle, a weak twitch, a flinch (lifting it) and how far it is curled in.</summary>
    Basis ArmBasis(Root view, int i, float flinch, float curlIn)
    {
        float twitch = 0.05f * Mathf.Sin(_time * 1.3f + view.Phase[i]) + 0.03f * Mathf.Sin(_time * 3.7f + view.Phase[i] * 2f);
        return new Basis(Vector3.Up, -(view.Angle[i] + twitch)) * new Basis(Vector3.Back, 0.12f * flinch + curlIn);
    }

    /// <summary>The world point a chain's well (side −1/1) stands at, by an arm at a share of its length.</summary>
    Vector3 PinWell(Root view, int i, float at, int side, float flinch)
    {
        var b = new Basis(Vector3.Up, -view.Angle[i]);
        var p = ArmPoint(at, view.Length[i], i % 2 == 0 ? 1f : -1f);
        var across = b * Vector3.Back;
        return view.Node.Position + new Vector3(0f, view.Floor + 0.02f, 0f) + b * new Vector3(p.X, 0f, p.Z) + across * side * PinSpread;
    }

    /// <summary>Lays the octopus out: its arms (flinching, curling in), its mantle (swelling).</summary>
    void Place(Root view, float flinch, float curlIn, float mantle)
    {
        for (int i = 0; i < Arms; i++) view.Arms[i].Basis = ArmBasis(view, i, flinch, curlIn);
        view.Mantle.Position = new Vector3(-0.05f, view.Floor + 0.95f * mantle, 0.08f);
        view.Mantle.Scale = new Vector3(0.72f, 1.02f, 0.72f) * mantle;
        view.Mantle.Rotation = new Vector3(-0.18f, 0f, 0f);
    }

    void SyncRoots(PlaneWorld world, float dt)
    {
        _links.Clear();
        for (int i = 0; i < _roots.Count && i < world.Blightroots.Count; i++)
        {
            var view = _roots[i];
            var root = world.Blightroots[i];
            if (!root.Alive)
            {
                Flee(view, root, dt);
                continue;
            }
            // Breathing; swelling on the telegraph; shrinking as her light drains it.
            float starve = Mathf.Clamp(root.Corruption.Fraction / 0.45f, 0f, 1f);
            float breathe = 1f + 0.05f * Mathf.Sin(_time * 2.1f + i);
            float size = breathe * (0.7f + 0.3f * starve) * (1f + 0.3f * root.Inflate);
            Place(view, root.Inflate, 0f, size);
            view.Mantle.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("swell"), root.Inflate);
            view.Mantle.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember"), 0.2f + 0.8f * root.Inflate);
            view.Mantle.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("wobble"), 0.5f + 0.8f * root.Inflate);
            // The chains over its arms, taut on the pulse.
            for (int a = 0; a < Arms; a++)
                foreach (float at in PinAt)
                    PinChain(view, a, at, root.Inflate);
            // Its ink draining off into the wells: dark droplets lifting off its arms and sinking toward a chain's well.
            view.Drip += dt * 7f;
            while (view.Drip >= 1f)
            {
                view.Drip -= 1f;
                int a = (int)(_droplets.RandRange(0f, Arms - 0.001f));
                float at = PinAt[(int)_droplets.RandRange(0f, PinAt.Length - 0.001f)];
                var from = view.Node.Position + new Vector3(0f, view.Floor, 0f) + view.Arms[a].Basis * ArmPoint(at, view.Length[a], a % 2 == 0 ? 1f : -1f) + Vector3.Up * 0.12f;
                var to = PinWell(view, a, at, _droplets.RandRange(0f, 1f) < 0.5f ? -1 : 1, 0f);
                _droplets.Emit(from, (to - from) * 1.6f + Vector3.Up * 0.5f, 0.6f, 0.05f, 0.015f, new Color(0.03f, 0.02f, 0.07f, 0.9f), new Color(0.15f, 0.3f, 0.6f, 0f), drag: 2f, buoyancy: -0.6f);
            }
            // Its ring of ink running out over its reach.
            view.Ring.Visible = root.PulseRadius >= 0f;
            if (view.Ring.Visible)
            {
                float r = Mathf.Max(root.PulseRadius, 0.1f);
                view.Ring.Scale = Vector3.One * (r / 0.8f);
                float fadeOut = 1f - Mathf.Clamp(r / BlightTuning.Reach, 0f, 1f);
                view.Ring.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("tint"), new Color(Kin.Blightroot.R * 0.5f, Kin.Blightroot.G * 0.5f, Kin.Blightroot.B * 0.6f, 0.85f * (0.35f + 0.65f * fadeOut)));
                view.Ring.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("width"), 0.09f + 0.4f / r);
                view.Ring.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), i * 1.7f);
            }
        }
    }

    /// <summary>A chain over an arm: from one well, arching over the arm, down into the other; lower and tauter on the pulse.</summary>
    void PinChain(Root view, int arm, float at, float taut)
    {
        var a = PinWell(view, arm, at, -1, taut);
        var b = PinWell(view, arm, at, 1, taut);
        // Over the top of the arm there (pulled down onto it on the pulse).
        float lift = ArmPoint(at, view.Length[arm], arm % 2 == 0 ? 1f : -1f).Y + ArmRadius(at) * 0.8f + 0.1f - 0.07f * taut;
        int n = Mathf.Max(3, (int)(Mathf.Pi * PinSpread / LinkSpacing));
        var path = new List<Vector3>(n + 1);
        for (int k = 0; k <= n; k++)
        {
            float u = k / (float)n;
            path.Add(a.Lerp(b, u) + Vector3.Up * Mathf.Sin(u * Mathf.Pi) * lift);
        }
        AddChain(path, 0f, taut, 0.9f);
    }

    /// <summary>Links along a path, every LinkSpacing, each turned a quarter from the last (as a real chain lies).</summary>
    void AddChain(List<Vector3> path, float gone, float taut, float scale)
    {
        float carry = 0f;
        int count = 0;
        for (int k = 1; k < path.Count; k++)
        {
            var from = path[k - 1];
            var seg = path[k] - from;
            float len = seg.Length();
            if (len < 1e-4f) continue;
            var dir = seg / len;
            for (float s = carry; s < len; s += LinkSpacing)
            {
                var at = from + dir * s;
                var side = dir.Cross(Vector3.Up);
                if (side.LengthSquared() < 1e-4f) side = Vector3.Right;
                side = side.Normalized();
                var up = side.Cross(dir).Normalized();
                // The link's long axis along the chain; every other one turned a quarter about it.
                bool turned = count++ % 2 == 1;
                var basis = new Basis(dir * 1.7f * scale, (turned ? side : up) * scale, (turned ? up : side) * scale);
                _links.Add((new Transform3D(basis, at), new Color(gone, taut, 0f, 1f)));
                carry = s + LinkSpacing - len;
            }
            if (carry < 0f) carry = 0f;
        }
    }

    /// <summary>Freed: its ink pops, the chains snap, and it curls its arms in, pushes off and jets up and away, healthy.</summary>
    void Flee(Root view, Blightroot root, float dt)
    {
        if (!view.Burst)
        {
            view.Burst = true;
            view.Since = 0f;
            var mantle = view.Node.Position + view.Mantle.Position;
            Pop(mantle, BlightTuning.SacRadius, Kin.Blightroot);
            // The chains snap: their links fly off.
            for (int a = 0; a < Arms; a++)
                foreach (float at in PinAt)
                {
                    var p = PinWell(view, a, at, 1, 0f).Lerp(PinWell(view, a, at, -1, 0f), 0.5f) + Vector3.Up * 0.3f;
                    _droplets.Burst(p, Vector3.Up, 4, 2.5f, 1f, 0.6f, 0.07f, 0.02f, new Color(0.03f, 0.02f, 0.07f, 0.95f), new Color(0.2f, 0.48f, 1f, 0f), drag: 2.5f);
                }
            view.Ring.Visible = false;
        }
        view.Since += dt;
        float t = view.Since;
        if (t > FleeSeconds)
        {
            view.Node.Visible = false;
            return;
        }
        // Healthy again: the ink's colour gives way to its own.
        float heal = Mathf.Clamp(t / 0.5f, 0f, 1f);
        foreach (var mesh in Meshes(view.Node))
            if (mesh != view.Ring) mesh.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("skin"), new Color(Healthy.R, Healthy.G, Healthy.B, Mathf.Lerp(0.6f, 0.8f, heal)));
        // It gathers itself (arms curling in and up), pushes off, and jets up and away to the north, arms trailing.
        float gather = Mathf.SmoothStep(0f, 1f, t / 0.5f);
        float jet = Mathf.Clamp((t - 0.4f) / (FleeSeconds - 0.4f), 0f, 1f);
        float e = jet * jet;
        // Arms up round its body as it gathers, then trailing down below it as it jets.
        float curl = Mathf.Lerp(0.9f * gather, -1.3f, Mathf.SmoothStep(0f, 1f, jet * 3f));
        Place(view, 0f, curl, 1f - 0.15f * gather + 0.15f * Mathf.Sin(t * 12f) * (1f - jet));
        view.Node.Position = new Vector3(root.Position.X, 14f * e, root.Position.Y - 9f * e);
        view.Node.Scale = Vector3.One * (1f - 0.35f * jet);
        foreach (var mesh in Meshes(view.Node))
            if (mesh.Mesh is CylinderMesh) mesh.Visible = false;
        // A trail of clean bubbles.
        if (jet > 0f && _motes.RandRange(0f, 1f) < 0.7f)
            _motes.Emit(view.Node.Position + new Vector3(0f, view.Floor + 0.3f, 0f) * view.Node.Scale.X, new Vector3(_motes.RandRange(-0.4f, 0.4f), -0.6f, _motes.RandRange(-0.4f, 0.4f)), 1.2f, 0.08f, 0.14f, new Color(0.85f, 0.95f, 1f, 0.7f), new Color(0.85f, 0.95f, 1f, 0f), drag: 1f, buoyancy: 1.2f);
        float fade = 1f - Mathf.Clamp((t - FleeSeconds + 0.6f) / 0.6f, 0f, 1f);
        foreach (var mesh in Meshes(view.Node))
            if (mesh != view.Ring) mesh.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("fade"), fade);
    }

    static IEnumerable<MeshInstance3D> Meshes(Node node)
    {
        if (node is MeshInstance3D m) yield return m;
        foreach (var child in node.GetChildren())
            foreach (var inner in Meshes(child)) yield return inner;
    }

    /// <summary>A Blightroot's gloomvines: chains torn loose from its wells, lashing from the floor up to her plane, coiling round her.</summary>
    void SyncVines(PlaneWorld world)
    {
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
            AddChain(path, gone, vine.Coiling ? 1f : 0.35f, 1.25f);
        }
        // Every chain this frame, in one draw.
        var mm = _chains.Multimesh;
        if (mm.InstanceCount < _links.Count) mm.InstanceCount = Mathf.Max(Mathf.Max(_links.Count, mm.InstanceCount * 2), 256);
        mm.VisibleInstanceCount = _links.Count;
        for (int i = 0; i < _links.Count; i++)
        {
            mm.SetInstanceTransform(i, _links[i].At);
            mm.SetInstanceColor(i, _links[i].Look);
        }
    }
}
