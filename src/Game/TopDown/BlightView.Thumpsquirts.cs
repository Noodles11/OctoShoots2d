using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Thumpsquirts on screen (docs/CORRUPTION.md): a corrupted sea squirt, an upright tube of glossy ink with two siphons
/// glowing a dim stolen ember. It stands on the floor where the floor comes near her plane and hangs just under it over
/// deep water. Winding up it contracts into a squat drum, trembling; it springs up stretched thin, lands squashed and
/// rebounds; to roll it tips onto its rim and runs on it like a coin, spinning. Where a leap will land, a dark ring
/// tightens on the ground. Freed, a pale sea squirt is left settled on the floor.
/// </summary>
public partial class BlightView
{
    /// <summary>The tube: its height and radius, and how far under her plane it hangs over deep water.</summary>
    const float TubeHeight = 1.6f, TubeRadius = 0.42f, TubeHang = 1.2f;

    sealed class ThumpTube
    {
        public Node3D Node = null!;
        public MeshInstance3D Body = null!, Mark = null!;
        public MeshInstance3D[] Siphons = null!;
        public float Spin, Tilt;
    }

    readonly Dictionary<Thumpsquirt, ThumpTube> _tubes = new();
    readonly List<MeshInstance3D> _freedSquirts = new();
    readonly CylinderMesh _tubeMesh = new() { TopRadius = 1f, BottomRadius = 1f, Height = 2f, RadialSegments = 24, Rings = 6 };
    StandardMaterial3D? _paleSquirt;

    float TubeBase(System.Numerics.Vector2 at) => Mathf.Max(Floor(at), LevelMap.SwimBand - TubeHang);

    void SyncThumps(PlaneWorld world, float dt)
    {
        var live = new HashSet<Thumpsquirt>(world.Thumpsquirts);
        var gone = new List<Thumpsquirt>();
        foreach (var (t, view) in _tubes)
            if (!live.Contains(t))
            {
                view.Node.QueueFree();
                view.Mark.QueueFree();
                gone.Add(t);
            }
        foreach (var t in gone) _tubes.Remove(t);

        foreach (var t in world.Thumpsquirts)
        {
            if (!_tubes.TryGetValue(t, out var view)) _tubes[t] = view = MakeTube(t);
            float sx = 1f, sy = 1f, lift = 0f, tremble = 0f, lean = 0f;
            var at = t.Position;
            float baseY = TubeBase(at);
            switch (t.State)
            {
                case ThumpState.Idle:
                    sy = 1f + 0.04f * Mathf.Sin(_time * 1.5f + t.Seed);
                    break;
                case ThumpState.Windup:
                {
                    // Contracting into a squat drum, trembling with the effort.
                    float k = Mathf.Clamp(t.Timer / ThumpTuning.Windup, 0f, 1f);
                    sy = 1f - 0.45f * k * k;
                    sx = 1f + 0.32f * k * k;
                    tremble = 0.05f * k;
                    break;
                }
                case ThumpState.Flight:
                {
                    // Up stretched thin, an arc to where it lands, and stretching again as it drops.
                    float k = t.LeapT;
                    float dist = (t.To - t.From).Length();
                    float fromY = TubeBase(t.From), toY = TubeBase(t.To);
                    baseY = Mathf.Lerp(fromY, toY, k);
                    lift = (2.4f + 0.18f * dist) * 4f * k * (1f - k);
                    sy = 1f + 0.4f * (1f - k) * (1f - k) + 0.2f * k * k;
                    sx = 1f / Mathf.Sqrt(sy);
                    lean = Mathf.Lerp(0.3f, -0.25f, k);
                    break;
                }
                case ThumpState.Landing:
                {
                    // Squashed flat by the landing, then springing back with a wobble.
                    float k = Mathf.Clamp(t.Timer / ThumpTuning.Squash, 0f, 1f);
                    float bounce = Mathf.Exp(-4f * k) * Mathf.Cos(9f * k);
                    sy = 1f - 0.45f * bounce;
                    sx = 1f + 0.3f * bounce;
                    break;
                }
                case ThumpState.Rest:
                    // Heaving, spent.
                    sy = 1f + 0.08f * Mathf.Sin(_time * 4f + t.Seed);
                    sx = 1f - 0.04f * Mathf.Sin(_time * 4f + t.Seed);
                    break;
            }
            // Tipping onto its rim, rolling on it, righting itself.
            float tiltWant = t.State switch
            {
                ThumpState.Tipping => Mathf.Clamp(t.Timer / ThumpTuning.Tip, 0f, 1f),
                ThumpState.Rolling => 1f,
                ThumpState.Rising => 1f - Mathf.Clamp(t.Timer / ThumpTuning.Tip, 0f, 1f),
                _ => 0f,
            };
            view.Tilt = tiltWant;
            if (t.State == ThumpState.Rolling) view.Spin += t.Velocity.Length() / (TubeRadius * 1.2f) * dt;
            float tilt = Mathf.DegToRad(68f) * view.Tilt * view.Tilt * (3f - 2f * view.Tilt);

            var heading = new Vector3(t.Heading.X, 0f, t.Heading.Y);
            var travel = t.State == ThumpState.Flight ? new Vector3(t.To.X - t.From.X, 0f, t.To.Y - t.From.Y) : heading;
            var basis = Basis.Identity;
            if (tilt > 1e-3f && heading.LengthSquared() > 1e-4f) basis = new Basis(heading.Normalized(), tilt) * basis;
            if (Mathf.Abs(lean) > 1e-3f && travel.LengthSquared() > 1e-4f) basis = new Basis(travel.Normalized().Cross(Vector3.Up).Normalized(), -lean) * basis;
            basis *= new Basis(Vector3.Up, view.Spin + t.Seed);
            var jitter = tremble > 0f ? new Vector3(Mathf.Sin(_time * 61f + t.Seed), 0f, Mathf.Cos(_time * 53f + t.Seed)) * tremble : Vector3.Zero;
            view.Node.Transform = new Transform3D(basis, new Vector3(at.X, baseY + lift, at.Y) + jitter);
            float h = TubeHeight * sy;
            view.Body.Position = new Vector3(0f, h * 0.5f, 0f);
            view.Body.Scale = new Vector3(TubeRadius * sx, h * 0.5f, TubeRadius * sx);
            for (int i = 0; i < view.Siphons.Length; i++)
            {
                float a = i * Mathf.Pi + 0.6f;
                view.Siphons[i].Position = new Vector3(Mathf.Cos(a) * TubeRadius * sx * 0.45f, h + 0.02f, Mathf.Sin(a) * TubeRadius * sx * 0.45f);
                view.Siphons[i].SetInstanceShaderParameter(SN.Of("ember"), t.State is ThumpState.Windup or ThumpState.Rest ? 1.1f : 0.7f);
            }

            // Where a leap will land: a dark ring tightening on the ground as it falls.
            view.Mark.Visible = t.State == ThumpState.Flight;
            if (view.Mark.Visible)
            {
                float k = t.LeapT;
                float r = Mathf.Lerp(1.8f, 0.7f, k);
                view.Mark.Position = new Vector3(t.To.X, Floor(t.To) + 0.07f, t.To.Y);
                view.Mark.Scale = new Vector3(r, 1f, r);
                view.Mark.SetInstanceShaderParameter(SN.Of("tint"), new Color(0.1f, 0.3f, 0.02f, 0.35f + 0.55f * k));
                view.Mark.SetInstanceShaderParameter(SN.Of("width"), 0.12f + 0.12f * k);
            }
        }
    }

    ThumpTube MakeTube(Thumpsquirt t)
    {
        var node = new Node3D();
        AddChild(node);
        var body = new MeshInstance3D { Mesh = _tubeMesh, MaterialOverride = _orb };
        body.SetInstanceShaderParameter(SN.Of("ember"), 0.25f);
        body.SetInstanceShaderParameter(SN.Of("wobble"), 0.5f);
        body.SetInstanceShaderParameter(SN.Of("seed"), t.Seed);
        body.SetInstanceShaderParameter(SN.Of("skin"), Kin.Skin(Kin.Thumpsquirt, 0.7f));
        node.AddChild(body);
        var siphons = new MeshInstance3D[2];
        for (int i = 0; i < siphons.Length; i++)
        {
            siphons[i] = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _orb, Scale = Vector3.One * 0.11f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            siphons[i].SetInstanceShaderParameter(SN.Of("seed"), t.Seed + i * 7f);
            siphons[i].SetInstanceShaderParameter(SN.Of("wobble"), 0.4f);
            siphons[i].SetInstanceShaderParameter(SN.Of("ember_tint"), Kin.Tint(Kin.Thumpsquirt));
            siphons[i].SetInstanceShaderParameter(SN.Of("skin"), Kin.Skin(Kin.Thumpsquirt, 0.5f));
            node.AddChild(siphons[i]);
        }
        var mark = new MeshInstance3D { Mesh = _quad, MaterialOverride = _ring, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(mark);
        return new ThumpTube { Node = node, Body = body, Siphons = siphons, Mark = mark };
    }

    void ClearThumps()
    {
        foreach (var view in _tubes.Values)
        {
            view.Node.QueueFree();
            view.Mark.QueueFree();
        }
        _tubes.Clear();
        foreach (var s in _freedSquirts) s.QueueFree();
        _freedSquirts.Clear();
    }

    /// <summary>Freed: a pale, translucent sea squirt settled on the floor where it burst.</summary>
    void LeaveFreedSquirt(System.Numerics.Vector2 at)
    {
        _paleSquirt ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.86f, 0.8f, 0.62f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.3f,
            EmissionEnabled = true,
            Emission = new Color(1f, 0.7f, 0.55f),
            EmissionEnergyMultiplier = 0.25f,
            RimEnabled = true,
            Rim = 0.6f,
        };
        float floor = Floor(at);
        var squirt = new MeshInstance3D
        {
            Mesh = _tubeMesh,
            MaterialOverride = _paleSquirt,
            Position = new Vector3(at.X, floor + 0.4f, at.Y),
            Scale = new Vector3(0.26f, 0.4f, 0.26f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(squirt);
        _freedSquirts.Add(squirt);
    }
}
