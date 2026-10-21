using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Lashjaws on screen (docs/CORRUPTION.md): a corrupted moray's head poking out of a dark hole in the reef wall at her
/// height, glossy ink with one ember eye. Its jaws gape and its eye flares before a lash; the tongue is a thick wavering
/// ribbon of ink with a bulb at its tip that runs out to what it catches and reels it in; it chews, and stunned its jaw
/// hangs slack and its eye dims. Freed, the healthy moray draws back into its hole.
/// </summary>
public partial class BlightView
{
    sealed class JawView
    {
        public Node3D Node = null!;
        public Node3D Head = null!, Jaw = null!;
        public MeshInstance3D Eye = null!, Tip = null!, Hole = null!;
        public float Open, Lunge;
    }

    readonly Dictionary<Lashjaw, JawView> _jaws = new();
    ImmediateMesh? _tongueMesh;
    StandardMaterial3D? _tongueMat;

    float JawHeight(System.Numerics.Vector2 at) => Mathf.Max(LevelMap.SwimBand + 0.15f, Floor(at) + 0.4f);

    void SyncLashjaws(PlaneWorld world, float dt, Camera3D? camera)
    {
        var live = new HashSet<Lashjaw>(world.Lashjaws);
        var gone = new List<Lashjaw>();
        foreach (var (l, view) in _jaws)
            if (!live.Contains(l))
            {
                // Freed: the healthy moray draws back into its hole, and the hole closes up after it.
                var node = view.Node;
                view.Tip.QueueFree();
                var tween = CreateTween();
                tween.TweenProperty(view.Head, "position", view.Head.Position - new Vector3(0f, 0f, 1.2f), 0.9f).SetEase(Tween.EaseType.In);
                tween.TweenInterval(0.6f);
                tween.TweenCallback(Callable.From(node.QueueFree));
                gone.Add(l);
            }
        foreach (var l in gone) _jaws.Remove(l);

        foreach (var l in world.Lashjaws)
        {
            if (!_jaws.TryGetValue(l, out var view)) _jaws[l] = view = MakeJaw(l);
            float y = JawHeight(l.Position);
            var facing = new Vector3(l.Facing.X, 0f, l.Facing.Y).Normalized();
            var hole = WallFace(l, y);
            view.Node.Transform = new Transform3D(Basis.LookingAt(-facing, Vector3.Up), new Vector3(hole.X, y, hole.Y));
            // How wide its jaws gape and how far its head juts out, by what it is doing.
            float open = l.State switch
            {
                LashState.Watch => 0.08f + 0.05f * Mathf.Sin(_time * 1.4f + l.Seed),
                LashState.Windup => Mathf.Lerp(0.1f, 0.95f, Mathf.Clamp(l.Timer / LashTuning.Windup, 0f, 1f)),
                LashState.Lash or LashState.Hold or LashState.Reel or LashState.Retract => 0.7f,
                LashState.Chew => 0.25f + 0.25f * Mathf.Abs(Mathf.Sin(_time * 7f)),
                LashState.Stunned => 0.5f + 0.06f * Mathf.Sin(_time * 3f),
                _ => 0.1f,
            };
            float lunge = l.State is LashState.Lash or LashState.Hold ? 0.3f : l.State == LashState.Windup ? -0.15f * Mathf.Clamp(l.Timer / LashTuning.Windup, 0f, 1f) : 0f;
            view.Open = Mathf.Lerp(view.Open, open, 1f - Mathf.Exp(-14f * dt));
            view.Lunge = Mathf.Lerp(view.Lunge, lunge, 1f - Mathf.Exp(-12f * dt));
            view.Head.Position = new Vector3(0f, 0f, view.Lunge);
            view.Jaw.Rotation = new Vector3(view.Open, 0f, 0f);
            // Stunned it lolls; winding up its eye flares.
            view.Head.Rotation = l.State == LashState.Stunned ? new Vector3(0f, 0f, 0.25f * Mathf.Sin(_time * 2.2f)) : Vector3.Zero;
            float eye = l.State switch { LashState.Windup => 2.2f, LashState.Stunned => 0.15f, LashState.Hold => 1.6f, _ => 1f };
            view.Eye.SetInstanceShaderParameter(SN.Of("ember"), eye);
            view.Tip.Visible = l.TongueOut;
            if (l.TongueOut)
            {
                view.Tip.Position = new Vector3(l.Tip.X, LevelMap.SwimBand + 0.15f, l.Tip.Y);
                view.Tip.SetInstanceShaderParameter(SN.Of("ember_tint"), l.Fish is { } f ? FishEmber(f.Kind) : Kin.Tint(Kin.Lashjaw));
            }
        }

        // The tongues: thick wavering ribbons of ink from the jaws to their tips.
        if (_tongueMesh is null || camera is null) return;
        _tongueMesh.ClearSurfaces();
        bool any = false;
        foreach (var (l, view) in _jaws)
            if (l.TongueOut && Vector2Dist(l.Tip, l.Mouth) > 0.05f)
            {
                if (!any) _tongueMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
                any = true;
                var from = view.Node.GlobalTransform * new Vector3(0f, -0.05f, 0.55f + view.Lunge);
                var to = new Vector3(l.Tip.X, LevelMap.SwimBand + 0.15f, l.Tip.Y);
                // Taut while it reels her in, waving while it flies or draws back.
                float slack = l.State is LashState.Hold or LashState.Reel ? 0.06f : 0.25f;
                Ribbon(_tongueMesh, from, to, 0.13f, 0.08f, slack, l.Seed, camera.GlobalPosition, new Color(1f, 1f, 1f, 1f));
            }
        if (any) _tongueMesh.SurfaceEnd();
    }

    /// <summary>Where the wall behind it rises to its height: its hole, so its head comes out of the rock, not inside it.</summary>
    System.Numerics.Vector2 WallFace(Lashjaw l, float y)
    {
        for (float d = 0f; d <= 3f; d += 0.1f)
        {
            var q = l.Position - l.Facing * d;
            if (_map is not null && _map.HeightAt(q) >= y - 0.25f) return q + l.Facing * 0.15f;
        }
        return l.Position;
    }

    static float Vector2Dist(System.Numerics.Vector2 a, System.Numerics.Vector2 b) => System.Numerics.Vector2.Distance(a, b);

    /// <summary>A camera-facing ribbon from one point to another, wavering, tapering from its root width to its tip width.</summary>
    void Ribbon(ImmediateMesh mesh, Vector3 from, Vector3 to, float rootWidth, float tipWidth, float wave, float seed, Vector3 eye, Color color)
    {
        const int segs = 16;
        Vector3 prevL = default, prevR = default;
        var along = to - from;
        var side = along.Cross(Vector3.Up);
        side = side.LengthSquared() > 1e-6f ? side.Normalized() : Vector3.Right;
        for (int k = 0; k <= segs; k++)
        {
            float u = k / (float)segs;
            var p = from + along * u + side * (wave * Mathf.Sin(u * 11f - _time * 9f + seed) * Mathf.Sin(u * Mathf.Pi));
            var view = (eye - p).Normalized();
            var across = along.Cross(view);
            across = across.LengthSquared() > 1e-6f ? across.Normalized() * Mathf.Lerp(rootWidth, tipWidth, u) : Vector3.Zero;
            var l = p - across;
            var r = p + across;
            if (k > 0)
                foreach (var v in new[] { prevL, prevR, l, prevR, r, l })
                {
                    mesh.SurfaceSetColor(color);
                    mesh.SurfaceAddVertex(v);
                }
            prevL = l;
            prevR = r;
        }
    }

    JawView MakeJaw(Lashjaw l)
    {
        if (_tongueMesh is null)
        {
            _tongueMat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                AlbedoColor = new Color(0.55f, 0.06f, 0.12f),
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            };
            _tongueMesh = new ImmediateMesh();
            AddChild(new MeshInstance3D { Mesh = _tongueMesh, MaterialOverride = _tongueMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
        var node = new Node3D();
        AddChild(node);
        // The dark hole in the wall it lives in, just behind its head.
        var hole = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _orb, Scale = new Vector3(0.8f, 0.65f, 0.25f), Position = new Vector3(0f, -0.05f, -0.55f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        hole.SetInstanceShaderParameter(SN.Of("ember"), 0f);
        hole.SetInstanceShaderParameter(SN.Of("wobble"), 0f);
        node.AddChild(hole);
        // Its head (+Z out of the wall): the skull and the jaw hinged at the back.
        var head = new Node3D();
        node.AddChild(head);
        var skull = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _orb, Scale = new Vector3(0.42f, 0.3f, 0.75f), Position = new Vector3(0f, 0.08f, 0.15f) };
        skull.SetInstanceShaderParameter(SN.Of("ember"), 0.1f);
        skull.SetInstanceShaderParameter(SN.Of("wobble"), 0.3f);
        skull.SetInstanceShaderParameter(SN.Of("seed"), l.Seed);
        skull.SetInstanceShaderParameter(SN.Of("skin"), Kin.Skin(Kin.Lashjaw, 0.7f));
        head.AddChild(skull);
        var jaw = new Node3D { Position = new Vector3(0f, -0.06f, -0.25f) };
        head.AddChild(jaw);
        var jawMesh = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _orb, Scale = new Vector3(0.36f, 0.14f, 0.66f), Position = new Vector3(0f, -0.03f, 0.5f) };
        jawMesh.SetInstanceShaderParameter(SN.Of("ember"), 0.05f);
        jawMesh.SetInstanceShaderParameter(SN.Of("seed"), l.Seed + 3f);
        jawMesh.SetInstanceShaderParameter(SN.Of("skin"), Kin.Skin(Kin.Lashjaw, 0.7f));
        jaw.AddChild(jawMesh);
        var eye = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _orb, Scale = Vector3.One * 0.1f, Position = new Vector3(0.25f, 0.24f, 0.4f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        eye.SetInstanceShaderParameter(SN.Of("wobble"), 0f);
        eye.SetInstanceShaderParameter(SN.Of("ember_tint"), Kin.Tint(new Color(1f, 0.35f, 0.3f)));
        head.AddChild(eye);
        var tip = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _orb, Scale = Vector3.One * 0.17f, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        tip.SetInstanceShaderParameter(SN.Of("ember"), 0.9f);
        tip.SetInstanceShaderParameter(SN.Of("wobble"), 1f);
        tip.SetInstanceShaderParameter(SN.Of("skin"), Kin.Skin(Kin.Lashjaw, 0.8f));
        AddChild(tip);
        return new JawView { Node = node, Head = head, Jaw = jaw, Eye = eye, Tip = tip, Hole = hole };
    }

    void ClearLashjaws()
    {
        foreach (var view in _jaws.Values)
        {
            view.Node.QueueFree();
            view.Tip.QueueFree();
        }
        _jaws.Clear();
        _tongueMesh?.ClearSurfaces();
    }
}
