using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Inkwardens on screen (docs/CORRUPTION.md): a corrupted sea anemone, a squat column of glossy ink crowned with a ring
/// of slowly writhing tentacles tipped violet, a faint ring of ink on the floor round it marking how far it guards.
/// When her light strikes a creature it guards, a tentacle's ink reaches out to it: a thin wavering tether that holds
/// while the hits come and fades after, and the struck creature shimmers violet as the light fizzles on it. Freed, a
/// healthy pale-pink anemone is left on the floor.
/// </summary>
public partial class BlightView
{
    const int WardenTentacles = 10;
    const float WardenHang = 1f;

    sealed class WardenView
    {
        public Node3D Node = null!;
        public MeshInstance3D Body = null!, Ring = null!;
        public MeshInstance3D[] Tentacles = null!, Tips = null!;
    }

    sealed class Tether
    {
        public Inkwarden Warden = null!;
        public System.Numerics.Vector2 To;
        public float Age;
    }

    readonly Dictionary<Inkwarden, WardenView> _wardens = new();
    readonly List<Tether> _tethers = new();
    readonly List<MeshInstance3D> _freedAnemones = new();
    readonly CylinderMesh _wardenBody = new() { TopRadius = 0.85f, BottomRadius = 1f, Height = 2f, RadialSegments = 20, Rings = 4 };
    readonly CylinderMesh _tentacleMesh = new() { TopRadius = 0.35f, BottomRadius = 1f, Height = 2f, RadialSegments = 6, Rings = 3 };
    ImmediateMesh? _tetherMesh;
    MeshInstance3D? _tetherNode;
    StandardMaterial3D? _tetherMat, _paleAnemone;

    /// <summary>How long a tether holds after the last blocked hit.</summary>
    const float TetherHold = 0.6f;

    float WardenBase(System.Numerics.Vector2 at) => Mathf.Max(Floor(at), LevelMap.SwimBand - WardenHang);

    void SyncWardens(PlaneWorld world, float dt, Camera3D? camera)
    {
        var live = new HashSet<Inkwarden>(world.Inkwardens);
        var gone = new List<Inkwarden>();
        foreach (var (w, view) in _wardens)
            if (!live.Contains(w))
            {
                view.Node.QueueFree();
                view.Ring.QueueFree();
                gone.Add(w);
            }
        foreach (var w in gone) _wardens.Remove(w);
        foreach (var w in world.Inkwardens)
        {
            if (!_wardens.TryGetValue(w, out var view)) _wardens[w] = view = MakeWarden(w);
            float baseY = WardenBase(w.Position);
            view.Node.Position = new Vector3(w.Position.X, baseY, w.Position.Y);
            // Its tentacles writhe slowly, each out of step; reaching out a little while it guards.
            bool guarding = _tethers.Exists(t => t.Warden == w);
            for (int i = 0; i < WardenTentacles; i++)
            {
                float a = Mathf.Tau * i / WardenTentacles + w.Seed;
                float sway = 0.35f + 0.18f * Mathf.Sin(_time * 1.3f + i * 1.7f + w.Seed) + (guarding ? 0.25f : 0f);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var basis = new Basis(dir.Cross(Vector3.Up).Normalized(), -sway) * Basis.Identity;
                float len = 0.55f + 0.08f * Mathf.Sin(_time * 2.1f + i);
                var root = new Vector3(dir.X * 0.36f, 0.78f, dir.Z * 0.36f);
                view.Tentacles[i].Transform = new Transform3D(basis.Scaled(new Vector3(0.06f, len * 0.5f, 0.06f)), root + basis * new Vector3(0f, len * 0.5f, 0f));
                view.Tips[i].Position = root + basis * new Vector3(0f, len, 0f);
            }
            view.Ring.Position = new Vector3(w.Position.X, baseY + 0.05f, w.Position.Y);
        }

        // The tethers: from the warden's crown to the creature it guards, a thin wavering ribbon of ink.
        for (int i = _tethers.Count - 1; i >= 0; i--)
        {
            _tethers[i].Age += dt;
            if (_tethers[i].Age > TetherHold || !_tethers[i].Warden.Alive) _tethers.RemoveAt(i);
        }
        if (_tetherMesh is null || camera is null) return;
        _tetherMesh.ClearSurfaces();
        if (_tethers.Count == 0) return;
        _tetherMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        var eye = camera.GlobalPosition;
        foreach (var t in _tethers)
        {
            var w = t.Warden;
            var from = new Vector3(w.Position.X, WardenBase(w.Position) + 1.25f, w.Position.Y);
            var to = new Vector3(t.To.X, LevelMap.SwimBand + 0.1f, t.To.Y);
            float fade = 1f - t.Age / TetherHold;
            const int segs = 14;
            Vector3 prevL = default, prevR = default;
            for (int k = 0; k <= segs; k++)
            {
                float u = k / (float)segs;
                var along = to - from;
                var side = along.Cross(Vector3.Up).Normalized();
                // A sag and a wave running along it.
                var p = from + along * u + Vector3.Up * (0.6f * 4f * u * (1f - u)) + side * (0.18f * Mathf.Sin(u * 9f - _time * 7f + w.Seed) * Mathf.Sin(u * Mathf.Pi));
                var view = (eye - p).Normalized();
                var across = along.Cross(view).Normalized() * (0.05f + 0.04f * Mathf.Sin(u * Mathf.Pi)) * fade;
                var l = p - across;
                var r = p + across;
                if (k > 0)
                {
                    var c = new Color(1f, 1f, 1f, fade);
                    foreach (var v in new[] { prevL, prevR, l, prevR, r, l })
                    {
                        _tetherMesh.SurfaceSetColor(c);
                        _tetherMesh.SurfaceAddVertex(v);
                    }
                }
                prevL = l;
                prevR = r;
            }
        }
        _tetherMesh.SurfaceEnd();
    }

    WardenView MakeWarden(Inkwarden w)
    {
        EnsureTethers();
        var node = new Node3D();
        AddChild(node);
        var body = new MeshInstance3D { Mesh = _wardenBody, MaterialOverride = _orb, Scale = new Vector3(0.5f, 0.4f, 0.5f), Position = new Vector3(0f, 0.4f, 0f) };
        body.SetInstanceShaderParameter(SN.Of("ember"), 0.15f);
        body.SetInstanceShaderParameter(SN.Of("wobble"), 0.4f);
        body.SetInstanceShaderParameter(SN.Of("seed"), w.Seed);
        body.SetInstanceShaderParameter(SN.Of("skin"), Kin.Skin(Kin.Inkwarden, 0.7f));
        node.AddChild(body);
        var tentacles = new MeshInstance3D[WardenTentacles];
        var tips = new MeshInstance3D[WardenTentacles];
        for (int i = 0; i < WardenTentacles; i++)
        {
            tentacles[i] = new MeshInstance3D { Mesh = _tentacleMesh, MaterialOverride = _orb, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            tentacles[i].SetInstanceShaderParameter(SN.Of("ember"), 0f);
            tentacles[i].SetInstanceShaderParameter(SN.Of("wobble"), 0.2f);
            tentacles[i].SetInstanceShaderParameter(SN.Of("skin"), Kin.Skin(Kin.Inkwarden, 0.7f));
            tips[i] = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _orb, Scale = Vector3.One * 0.07f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            tips[i].SetInstanceShaderParameter(SN.Of("ember"), 1.2f);
            tips[i].SetInstanceShaderParameter(SN.Of("ember_tint"), new Color(0.7f, 0.35f, 1f, 1f));
            node.AddChild(tentacles[i]);
            node.AddChild(tips[i]);
        }
        // Its reach: a faint ring of ink on the floor.
        var ring = new MeshInstance3D { Mesh = _quad, MaterialOverride = _ring, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Scale = new Vector3(WardenTuning.Reach, 1f, WardenTuning.Reach) };
        ring.SetInstanceShaderParameter(SN.Of("tint"), new Color(0.22f, 0.08f, 0.35f, 0.22f));
        ring.SetInstanceShaderParameter(SN.Of("width"), 0.03f);
        AddChild(ring);
        return new WardenView { Node = node, Body = body, Ring = ring, Tentacles = tentacles, Tips = tips };
    }

    void EnsureTethers()
    {
        if (_tetherMesh is not null) return;
        _tetherMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true,
            AlbedoColor = new Color(0.42f, 0.16f, 0.7f),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            RenderPriority = 2,
        };
        _tetherMesh = new ImmediateMesh();
        _tetherNode = new MeshInstance3D { Mesh = _tetherMesh, MaterialOverride = _tetherMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_tetherNode);
    }

    /// <summary>A hit was turned aside: its warden's tether to the creature (held while the hits come), and a violet shimmer on it.</summary>
    void OnWarded(PlaneWorld world, PlaneEvent e)
    {
        Inkwarden? warden = null;
        foreach (var w in world.Inkwardens)
            if (w.Position == e.Position) warden = w;
        if (warden is null) return;
        var to = e.Position + e.Direction;
        var held = _tethers.Find(t => t.Warden == warden && System.Numerics.Vector2.Distance(t.To, to) < 1.5f);
        if (held is not null)
        {
            held.Age = 0f;
            held.To = to;
        }
        else _tethers.Add(new Tether { Warden = warden, To = to });
        var at = new Vector3(to.X, LevelMap.SwimBand + 0.2f, to.Y);
        for (int i = 0; i < 6; i++)
            _motes.Emit(at + _motes.RandDir() * 0.35f, _motes.RandDir() * _motes.RandRange(0.4f, 1.2f), _motes.RandRange(0.25f, 0.45f), _motes.RandRange(0.06f, 0.12f), 0.01f, new Color(0.65f, 0.35f, 1f, 0.9f), new Color(0.4f, 0.15f, 0.7f, 0f), drag: 3f);
    }

    void ClearWardens()
    {
        foreach (var view in _wardens.Values)
        {
            view.Node.QueueFree();
            view.Ring.QueueFree();
        }
        _wardens.Clear();
        _tethers.Clear();
        _tetherMesh?.ClearSurfaces();
        foreach (var a in _freedAnemones) a.QueueFree();
        _freedAnemones.Clear();
    }

    /// <summary>Freed: a healthy pale-pink anemone left on the floor.</summary>
    void LeaveFreedAnemone(System.Numerics.Vector2 at)
    {
        _paleAnemone ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.72f, 0.8f, 0.7f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.4f,
            EmissionEnabled = true,
            Emission = new Color(1f, 0.6f, 0.7f),
            EmissionEnergyMultiplier = 0.2f,
            RimEnabled = true,
            Rim = 0.5f,
        };
        float floor = Floor(at);
        var anemone = new MeshInstance3D
        {
            Mesh = _wardenBody,
            MaterialOverride = _paleAnemone,
            Position = new Vector3(at.X, floor + 0.25f, at.Y),
            Scale = new Vector3(0.35f, 0.25f, 0.35f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(anemone);
        _freedAnemones.Add(anemone);
    }
}
