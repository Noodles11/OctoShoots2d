using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Flounders on screen (docs/ADVENTURE.md §4): a flat oval disc lying on its patch (a reef shelf just under her plane),
/// a pale fin fringe all round it and a small tail, sandy and mottled, both its eyes on its top. Shimmying, it wriggles
/// left and right, kicking up a little sand. The Hearthguard is the same disc gone to ink (the sand still showing
/// through as its skin, a gold ember in it): a crown of spines grown out of its fringe, ink drops lifting off it, no
/// eyes. Its needle: the disc draws out narrow and pointed toward her, its head lifting, then a spear of ink stabs from
/// its point up to her plane and draws back. Freed, it is a sandy flounder again, lifting off and swimming away.
/// </summary>
public partial class BlightView
{
    const int FlounderSpines = 12;
    static readonly Color HearthSkin = new(0.85f, 0.66f, 0.32f);

    sealed class FlounderView
    {
        public Node3D Node = null!, Disc = null!;
        public MeshInstance3D Body = null!, Fringe = null!, Tail = null!, Spear = null!;
        public MeshInstance3D[] Eyes = null!, Spots = null!, Spines = null!;
        public bool Corrupted;
        public float Drip;
    }

    readonly Dictionary<PlaneFlounder, FlounderView> _flounders = new();
    readonly CylinderMesh _spearMesh = new() { TopRadius = 0f, BottomRadius = 1f, Height = 1f, RadialSegments = 10, Rings = 1 };
    StandardMaterial3D? _sandSkin, _sandFringe, _sandSpot, _eyeMat;

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
        _sandSkin ??= new StandardMaterial3D { AlbedoColor = new Color(0.62f, 0.52f, 0.36f), Roughness = 0.9f };
        _sandFringe ??= new StandardMaterial3D { AlbedoColor = new Color(0.74f, 0.66f, 0.5f), Roughness = 0.9f };
        _sandSpot ??= new StandardMaterial3D { AlbedoColor = new Color(0.36f, 0.29f, 0.2f), Roughness = 0.9f };
        _eyeMat ??= new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.86f, 0.7f), Roughness = 0.3f };
        var node = new Node3D();
        AddChild(node);
        var disc = new Node3D();
        node.AddChild(disc);
        MeshInstance3D Part(Mesh mesh, Vector3 scale, Vector3 at, bool shadow = true)
        {
            var m = new MeshInstance3D { Mesh = mesh, Scale = scale, Position = at, CastShadow = shadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off };
            disc.AddChild(m);
            return m;
        }
        var view = new FlounderView
        {
            Node = node,
            Disc = disc,
            Fringe = Part(_sphere, new Vector3(0.6f, 0.025f, 0.44f), new Vector3(-0.02f, -0.02f, 0f)),
            Body = Part(_sphere, new Vector3(0.48f, 0.07f, 0.32f), Vector3.Zero),
            Tail = Part(_sphere, new Vector3(0.16f, 0.025f, 0.18f), new Vector3(-0.62f, -0.01f, 0f)),
            Eyes = new[] { Part(_sphere, Vector3.One * 0.045f, new Vector3(0.3f, 0.07f, -0.07f), false), Part(_sphere, Vector3.One * 0.045f, new Vector3(0.26f, 0.065f, 0.06f), false) },
            Spots = new MeshInstance3D[5],
            Spines = new MeshInstance3D[FlounderSpines],
        };
        var rng = new RandomNumberGenerator { Seed = (ulong)(f.Seed * 1000f) };
        for (int i = 0; i < view.Spots.Length; i++)
            view.Spots[i] = Part(_sphere, new Vector3(rng.RandfRange(0.04f, 0.07f), 0.012f, rng.RandfRange(0.04f, 0.07f)),
                new Vector3(rng.RandfRange(-0.3f, 0.2f), 0.06f, rng.RandfRange(-0.18f, 0.18f)), false);
        for (int i = 0; i < FlounderSpines; i++)
        {
            float a = Mathf.Tau * i / FlounderSpines;
            var d = new Vector3(0.6f * Mathf.Cos(a), 0f, 0.44f * Mathf.Sin(a));
            var dir = (d.Normalized() + Vector3.Up * 0.5f).Normalized();
            var x = dir.Cross(Vector3.Up).Normalized();
            var z = x.Cross(dir).Normalized();
            var spine = new MeshInstance3D { Mesh = _spikeMesh, MaterialOverride = _orb, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
            float spineLen = 0.5f + 0.25f * (i % 2);
            spine.Transform = new Transform3D(new Basis(x * 0.7f, dir * spineLen, z * 0.7f), d * 0.92f + dir * SpikeLength * 0.5f * spineLen);
            spine.SetInstanceShaderParameter(SN.Of("ember"), 0f);
            spine.SetInstanceShaderParameter(SN.Of("wobble"), 0.1f);
            spine.SetInstanceShaderParameter(SN.Of("skin"), Kin.Skin(Kin.Inkwarden, 0.6f));
            disc.AddChild(spine);
            view.Spines[i] = spine;
        }
        view.Spear = new MeshInstance3D { Mesh = _spearMesh, MaterialOverride = _orb, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
        view.Spear.SetInstanceShaderParameter(SN.Of("ember"), 0.2f);
        view.Spear.SetInstanceShaderParameter(SN.Of("ember_tint"), Kin.Tint(HearthSkin));
        view.Spear.SetInstanceShaderParameter(SN.Of("wobble"), 0.05f);
        view.Spear.SetInstanceShaderParameter(SN.Of("skin"), Kin.Skin(HearthSkin, 0.4f));
        node.AddChild(view.Spear);
        Dress(view, f);
        return view;
    }

    /// <summary>Healthy, its sandy skin and eyes; corrupted, ink with the sand under it, its spines grown, its eyes gone.</summary>
    void Dress(FlounderView view, PlaneFlounder f)
    {
        view.Corrupted = f.Corrupted;
        foreach (var part in new[] { view.Body, view.Fringe, view.Tail })
        {
            if (!f.Corrupted)
            {
                part.MaterialOverride = part == view.Body ? _sandSkin : _sandFringe;
                continue;
            }
            part.MaterialOverride = _orb;
            part.SetInstanceShaderParameter(SN.Of("ember"), part == view.Body ? 0.35f : 0f);
            part.SetInstanceShaderParameter(SN.Of("ember_tint"), Kin.Tint(HearthSkin));
            part.SetInstanceShaderParameter(SN.Of("wobble"), 0.25f);
            part.SetInstanceShaderParameter(SN.Of("seed"), f.Seed);
            part.SetInstanceShaderParameter(SN.Of("skin"), Kin.Skin(HearthSkin, part == view.Body ? 0.55f : 0.75f));
        }
        foreach (var s in view.Spots)
        {
            s.Visible = !f.Corrupted;
            s.MaterialOverride = _sandSpot;
        }
        foreach (var e in view.Eyes)
        {
            e.Visible = !f.Corrupted;
            e.MaterialOverride = _eyeMat;
        }
        foreach (var s in view.Spines) s.Visible = f.Corrupted;
    }

    void PoseFlounder(PlaneFlounder f, FlounderView view, float dt)
    {
        float bed = FlounderBed(f.Home);
        // Freed, it lifts off its patch toward her plane as it swims away.
        float y = f.Freed ? Mathf.Lerp(bed, LevelMap.SwimBand - 0.3f, Mathf.Clamp(f.Left, 0f, 1f)) : bed;
        view.Node.Position = new Vector3(f.Position.X, y, f.Position.Y);
        float needle = f.Needle;
        // Its heading, swung toward its aim as the needle draws out; the shimmy's wriggle on top.
        float aim = Mathf.Atan2(f.Aim.Y, f.Aim.X);
        float heading = f.Heading + Mathf.Wrap(aim - f.Heading, -Mathf.Pi, Mathf.Pi) * needle;
        float swim = f.Freed ? 0.25f * Mathf.Sin(_time * 7f + f.Seed) : 0f;
        view.Disc.Rotation = new Vector3(0f, -(heading + f.Wriggle + swim), 0f);
        // The needle: drawn out long and narrow, its head lifting toward her.
        view.Disc.Scale = new Vector3(1f + 1.5f * needle, 1f + 0.6f * needle, 1f - 0.8f * needle);
        view.Disc.Position = new Vector3(Mathf.Cos(heading), 0f, Mathf.Sin(heading)) * 0.55f * needle;
        view.Disc.Rotation += new Vector3(0f, 0f, 0.15f * needle);
        // A shimmy kicks up a little sand.
        if (f.Stage == FlounderStage.Shimmy && !f.Freed && GD.Randf() < dt * 14f)
        {
            var at = view.Node.Position + _motes.RandDir() * 0.4f;
            at.Y = bed + 0.05f;
            _droplets.Emit(at, (_motes.RandDir() + Vector3.Up) * _motes.RandRange(0.3f, 0.8f), _motes.RandRange(0.5f, 0.9f), 0.05f, 0.09f, new Color(0.72f, 0.64f, 0.48f, 0.7f), new Color(0.72f, 0.64f, 0.48f, 0f), drag: 3f);
        }
        if (!f.Corrupted)
        {
            view.Spear.Visible = false;
            return;
        }
        // Struck by her light (flashing), worn down (its outline holding).
        float hit = f.Corruption.Since < 0.25f && f.Corruption.LastHit > 0f ? 1f - f.Corruption.Since / 0.25f : 0f;
        float worn = 1f - f.Corruption.Fraction;
        foreach (var part in new[] { view.Body, view.Fringe, view.Tail })
        {
            part.SetInstanceShaderParameter(SN.Of("hit"), hit);
            part.SetInstanceShaderParameter(SN.Of("worn"), worn);
        }
        view.Body.SetInstanceShaderParameter(SN.Of("swell"), f.Stage == FlounderStage.Windup ? 0.3f * needle : 0f);
        // Its ink lifts off it in drops (the third tell).
        view.Drip -= dt;
        if (view.Drip <= 0f)
        {
            view.Drip = _motes.RandRange(0.25f, 0.5f);
            var from = view.Node.Position + new Vector3(_motes.RandRange(-0.35f, 0.35f), 0.08f, _motes.RandRange(-0.25f, 0.25f));
            _droplets.Emit(from, Vector3.Up * _motes.RandRange(0.2f, 0.4f), _motes.RandRange(1f, 1.6f), 0.05f, 0.02f, new Color(0.03f, 0.01f, 0.05f, 0.9f), new Color(0.25f, 0.08f, 0.4f, 0f), drag: 1f, buoyancy: 0.6f);
        }
        // The spear: from the point of its disc up to its tip at her plane.
        view.Spear.Visible = f.Reach > FlounderTuning.Radius + 0.05f;
        if (!view.Spear.Visible) return;
        var dir2 = new Vector3(f.Aim.X, 0f, f.Aim.Y);
        var root = new Vector3(0f, 0.1f, 0f) + dir2 * 1.3f;
        var tip = new Vector3(f.Tip.X, LevelMap.SwimBand, f.Tip.Y) - view.Node.Position;
        var along = tip - root;
        float len = along.Length();
        var up = along / len;
        var side = up.Cross(Vector3.Up).Normalized();
        var fwd = side.Cross(up).Normalized();
        view.Spear.Transform = new Transform3D(new Basis(side * 0.15f, up * len, fwd * 0.15f), root + along * 0.5f);
    }

    void ClearFlounders()
    {
        foreach (var view in _flounders.Values) view.Node.QueueFree();
        _flounders.Clear();
    }
}
