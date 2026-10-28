using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Inkwardens on screen (docs/CORRUPTION.md): a corrupted sea anemone, a squat column of glossy ink crowned with a ring
/// of slowly writhing tentacles tipped violet. A faint thin tether of ink wavers out of it to every creature it guards,
/// always; when her light strikes one, that tether swells bold and dark while the hits come and settles back after, and
/// the struck creature shimmers violet as the light fizzles on it. Freed, a healthy pale-pink anemone is left on the
/// floor.
/// <para>
/// The adventure's Inkwarden (a turned pufferfish, Inkwarden.Puffer) is a ring instead: an inflated ring of ink at her
/// plane, its core glowing the pufferfish's yellow, its spikes folded in toward its hole. Roused, the spikes roll over
/// its top to point out, the ring spins up and they go one by one (each fired is gone from it); settling, it slows and
/// they grow back, pointing in.
/// </para>
/// </summary>
public partial class BlightView
{
    const int WardenTentacles = 10;
    const float WardenHang = 1f;

    sealed class WardenView
    {
        public Node3D Node = null!;
        public MeshInstance3D Body = null!;
        public MeshInstance3D[] Tentacles = null!, Tips = null!;
        /// <summary>The spiky ring's spikes (the adventure's Inkwarden).</summary>
        public MeshInstance3D[] Spikes = System.Array.Empty<MeshInstance3D>();
    }

    /// <summary>The spiky ring: its ring's middle line and tube (radius), and a spike's length and base.</summary>
    const float RingMid = 0.5f, RingTube = 0.24f, SpikeLength = 0.5f;
    readonly TorusMesh _ringBody = new() { InnerRadius = RingMid - RingTube, OuterRadius = RingMid + RingTube, Rings = 32, RingSegments = 14 };
    readonly CylinderMesh _spikeMesh = new() { TopRadius = 0f, BottomRadius = 0.08f, Height = SpikeLength, RadialSegments = 8, Rings = 1 };
    static readonly Color PufferCore = new(1f, 0.8f, 0.25f, 1f);

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

    /// <summary>How long a tether holds bold after the last blocked hit; how strong it is at rest (0..1).</summary>
    const float TetherHold = 0.6f, LinkRest = 0.35f;

    /// <summary>This frame's tethers: the warden, the creature's place, how strong (0..1).</summary>
    readonly List<(Inkwarden Warden, System.Numerics.Vector2 To, float Strength)> _links = new();

    float WardenBase(System.Numerics.Vector2 at) => Mathf.Max(Floor(at), LevelMap.SwimBand - WardenHang);

    void SyncWardens(PlaneWorld world, float dt, Camera3D? camera)
    {
        var live = new HashSet<Inkwarden>(world.Inkwardens);
        var gone = new List<Inkwarden>();
        foreach (var (w, view) in _wardens)
            if (!live.Contains(w))
            {
                view.Node.QueueFree();
                gone.Add(w);
            }
        foreach (var w in gone) _wardens.Remove(w);
        foreach (var w in world.Inkwardens)
        {
            if (!_wardens.TryGetValue(w, out var view)) _wardens[w] = view = w.Puffer ? MakeSpiky(w) : MakeWarden(w);
            if (w.Puffer)
            {
                PoseSpiky(w, view);
                continue;
            }
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
        }

        // The tethers: from the warden's crown to every creature it guards, a thin wavering ribbon of ink — faint at rest,
        // bold while her hits are turned aside on it (held, then settling back).
        for (int i = _tethers.Count - 1; i >= 0; i--)
        {
            _tethers[i].Age += dt;
            if (_tethers[i].Age > TetherHold || !_tethers[i].Warden.Alive) _tethers.RemoveAt(i);
        }
        if (_tetherMesh is null || camera is null) return;
        _tetherMesh.ClearSurfaces();
        _links.Clear();
        foreach (var w in world.Inkwardens)
            foreach (var at in world.GuardedBy(w))
            {
                var hit = _tethers.Find(t => t.Warden == w && System.Numerics.Vector2.Distance(t.To, at) < 1.5f);
                _links.Add((w, at, LinkRest + (1f - LinkRest) * (hit is null ? 0f : 1f - hit.Age / TetherHold)));
            }
        // A struck creature no longer guarded (gone, or out of reach): its tether fades out where it was.
        foreach (var t in _tethers)
            if (!_links.Exists(l => l.Warden == t.Warden && System.Numerics.Vector2.Distance(l.To, t.To) < 1.5f))
                _links.Add((t.Warden, t.To, 1f - t.Age / TetherHold));
        if (_links.Count == 0) return;
        _tetherMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        var eye = camera.GlobalPosition;
        foreach (var (w, at, fade) in _links)
        {
            var from = w.Puffer ? new Vector3(w.Position.X, LevelMap.SwimBand + 0.2f, w.Position.Y) : new Vector3(w.Position.X, WardenBase(w.Position) + 1.25f, w.Position.Y);
            var to = new Vector3(at.X, LevelMap.SwimBand + 0.1f, at.Y);
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
                var across = along.Cross(view).Normalized() * (0.05f + 0.04f * Mathf.Sin(u * Mathf.Pi)) * (0.5f + 0.5f * fade);
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
            tentacles[i] = new MeshInstance3D { Mesh = _tentacleMesh, MaterialOverride = _orb, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
            tentacles[i].SetInstanceShaderParameter(SN.Of("ember"), 0f);
            tentacles[i].SetInstanceShaderParameter(SN.Of("wobble"), 0.2f);
            tentacles[i].SetInstanceShaderParameter(SN.Of("skin"), Kin.Skin(Kin.Inkwarden, 0.7f));
            tips[i] = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _orb, Scale = Vector3.One * 0.07f, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
            tips[i].SetInstanceShaderParameter(SN.Of("ember"), 1.2f);
            tips[i].SetInstanceShaderParameter(SN.Of("ember_tint"), new Color(0.7f, 0.35f, 1f, 1f));
            node.AddChild(tentacles[i]);
            node.AddChild(tips[i]);
        }
        return new WardenView { Node = node, Body = body, Tentacles = tentacles, Tips = tips };
    }

    /// <summary>The spiky ring: its inflated body and its spikes.</summary>
    WardenView MakeSpiky(Inkwarden w)
    {
        EnsureTethers();
        var node = new Node3D();
        AddChild(node);
        var body = new MeshInstance3D { Mesh = _ringBody, MaterialOverride = _orb, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
        body.SetInstanceShaderParameter(SN.Of("ember"), 0.5f);
        body.SetInstanceShaderParameter(SN.Of("ember_tint"), PufferCore);
        body.SetInstanceShaderParameter(SN.Of("wobble"), 0.5f);
        body.SetInstanceShaderParameter(SN.Of("seed"), w.Seed);
        body.SetInstanceShaderParameter(SN.Of("skin"), Kin.Skin(Kin.Inkwarden, 0.5f));
        node.AddChild(body);
        var spikes = new MeshInstance3D[WardenTuning.Spikes];
        for (int i = 0; i < spikes.Length; i++)
        {
            spikes[i] = new MeshInstance3D { Mesh = _spikeMesh, MaterialOverride = _orb, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
            spikes[i].SetInstanceShaderParameter(SN.Of("ember"), 0f);
            spikes[i].SetInstanceShaderParameter(SN.Of("wobble"), 0.1f);
            spikes[i].SetInstanceShaderParameter(SN.Of("skin"), Kin.Skin(Kin.Inkwarden, 0.8f));
            node.AddChild(spikes[i]);
        }
        return new WardenView { Node = node, Body = body, Tentacles = System.Array.Empty<MeshInstance3D>(), Tips = System.Array.Empty<MeshInstance3D>(), Spikes = spikes };
    }

    /// <summary>
    /// The spiky ring this frame: at her plane, bobbing; spinning as the sim spins it; its spikes rolled from pointing in
    /// (folded into its hole) over its top to pointing out; the fired ones gone; settling, the new ones growing in.
    /// </summary>
    void PoseSpiky(Inkwarden w, WardenView view)
    {
        float bob = 0.06f * Mathf.Sin(_time * 1.4f + w.Seed);
        view.Node.Position = new Vector3(w.Position.X, LevelMap.SwimBand + bob, w.Position.Y);
        view.Node.Rotation = new Vector3(0f, -w.Spin, 0f);
        // Roused, it puffs up a little.
        float puff = w.Stage is WardenStage.Rolling or WardenStage.SpinUp or WardenStage.Firing ? 1.12f : 1f;
        view.Body.Scale = new Vector3(puff, puff * (1f + 0.04f * Mathf.Sin(_time * 3f + w.Seed)), puff);
        float roll = w.Out;
        float tilt = Mathf.Pi * (roll * roll * (3f - 2f * roll));
        float grown = w.Grown;
        int fired = w.Stage == WardenStage.Firing ? WardenTuning.Spikes - w.SpikesLeft : 0;
        for (int i = 0; i < view.Spikes.Length; i++)
        {
            var spike = view.Spikes[i];
            spike.Visible = i >= fired && grown > 0.02f;
            if (!spike.Visible) continue;
            float a = Mathf.Tau * i / view.Spikes.Length;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var t = new Vector3(-d.Z, 0f, d.X);
            // From pointing in (−d) over the top (up) to pointing out (+d).
            var dir = (-d * Mathf.Cos(tilt) + Vector3.Up * Mathf.Sin(tilt)).Normalized();
            float len = grown;
            var root = d * RingMid + dir * RingTube * 0.9f;
            var y = dir;
            var x = t;
            var z = x.Cross(y).Normalized();
            spike.Transform = new Transform3D(new Basis(x, y, z).Scaled(new Vector3(1f, len, 1f)), root + dir * SpikeLength * 0.5f * len);
        }
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
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
        };
        AddChild(anemone);
        _freedAnemones.Add(anemone);
    }
}
