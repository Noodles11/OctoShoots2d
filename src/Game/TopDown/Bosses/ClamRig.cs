using Godot;
using OctoShoots.Core.Bosses;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown.Bosses;

/// <summary>
/// A giant clam (Bivalve): two heavy ribbed valves on a hinge at the back (clam_shell.gdshader), a dark corrupted mantle
/// between them, the pearl on the mantle. The lid lifts on the telegraph, stands open while it fires, rattles in the
/// stagger and slams on the snap; freed, it lies wide open. Mutations: shell overgrowing past the animal in ribbed
/// knobs, a third valve forcing the hinge, ink sacs along the mantle. Queen Clam is this rig's reference roll.
/// </summary>
public partial class ClamRig : BossRig
{
    const float OpenDeg = 52f;
    Node3D _hinge = null!;
    MeshInstance3D _bottom = null!, _top = null!;
    StandardMaterial3D _mantle = null!;
    ShaderMaterial _shell = null!;
    float _open;

    public override Node3D CrustParent => _top;

    public override Vector3 Surface(float angle, float out_)
    {
        float rho = Mathf.Clamp(out_, 0f, 1f);
        return new Vector3(Mathf.Cos(angle) * rho * R * Spec.Stretch, 0.95f * Mathf.Sqrt(Mathf.Max(0f, 1f - rho * rho)) * 0.55f + 0.05f, Mathf.Sin(angle) * rho * R * 0.88f);
    }

    protected override void BuildBody()
    {
        _shell = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/clam_shell.gdshader") };
        var c = ToColor(Spec.TrueColor);
        _shell.SetShaderParameter("color_a", c);
        _shell.SetShaderParameter("color_b", c.Darkened(0.35f));
        _shell.SetShaderParameter("ribs", 5f + Spec.Count * 0.12f);
        float rx = R * Spec.Stretch, rz = R * 0.88f;
        float depth = 0.6f + 0.3f * Spec.Bulk;
        // The bottom valve: a flattened bowl; the top one hinged at the back (local −X), opening upward.
        var dome = new SphereMesh { Radius = 1f, Height = 1f, IsHemisphere = true, RadialSegments = 40, Rings = 12 };
        _bottom = Part(dome, this, _shell);
        _bottom.Scale = new Vector3(rx, depth, rz);
        _bottom.RotationDegrees = new Vector3(180f, 0f, 0f);
        Skinned.Add(_bottom);
        // The mantle inside, seen when it opens: dark corrupted flesh (its true colour once freed).
        _mantle = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.07f, 0.09f), Roughness = 0.35f };
        var mantle = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 1f, BottomRadius = 1f, Height = 0.04f, RadialSegments = 40 },
            Scale = new Vector3(rx * 0.94f, 1f, rz * 0.94f),
            Position = new Vector3(0f, 0.02f, 0f),
            MaterialOverride = _mantle,
        };
        AddChild(mantle);
        _hinge = new Node3D { Position = new Vector3(-rx, 0f, 0f) };
        AddChild(_hinge);
        _top = Part(dome, _hinge, _shell);
        _top.Scale = new Vector3(rx, depth + 0.2f, rz);
        _top.Position = new Vector3(rx, 0f, 0f);
        Skinned.Add(_top);
        PearlHeight = 0.36f;
    }

    protected override Node3D? Mutation(MutationTheme theme, BodySite site)
    {
        var at = Surface(site.Angle, site.Out);
        float size = (0.35f + site.Size) * (0.8f + 0.6f * Spec.Mutation);
        switch (theme)
        {
            case MutationTheme.OvergrownShell:
            {
                // Shell piling past the animal: a ribbed knob of layered calcite, ink in its folds.
                var knob = new Node3D { Position = at, Scale = Vector3.One * size };
                var mesh = BossMeshes.Lathe(t => new Vector2(Mathf.Sin(t * Mathf.Pi * 0.5f + 0.2f) * (1f - 0.15f * Mathf.Sin(t * 18f)), 0.9f * (1f - t)), 10, 18, Inked(0.6f),
                    (t, a) => 0.08f * Mathf.Sin(a * 7f));
                Part(mesh, knob);
                return knob;
            }
            case MutationTheme.ExtraLid:
            {
                // A third valve forced up out of the hinge, too small and ridged wrong.
                var lid = new Node3D { Position = at + Vector3.Up * 0.1f, Rotation = new Vector3(0f, -site.Angle, 0.6f), Scale = new Vector3(size * 1.4f, size * 0.7f, size) };
                var piece = Part(new SphereMesh { Radius = 1f, Height = 1f, IsHemisphere = true, RadialSegments = 24, Rings = 8 }, lid, _shell);
                Skinned.Add(piece);
                var ink = Part(BossMeshes.Ball(8, 14, Inked()), lid);
                ink.Scale = new Vector3(0.9f, 0.25f, 0.9f);
                return lid;
            }
            default:
                return null;
        }
    }

    public override void Pose(in RigPose pose)
    {
        float open = 0f;
        if (pose.Stage == BossStage.Fight)
        {
            open = pose.Act switch
            {
                BossAct.Telegraph => Mathf.SmoothStep(0f, 1f, pose.Warn) * 0.85f,
                BossAct.Act or BossAct.Helix => pose.Open ? 1f : 0.35f,
                BossAct.Window => 1f,
                _ => 0f,
            };
            if (pose.SnapWarn) open = 1f + 0.15f * Mathf.Sin(pose.Time * 40f);
        }
        open *= pose.SnapSlam;
        // Freed, it lies wide open (shut only for the shudder and the leap).
        if (pose.Stage == BossStage.Freed) open = pose.Melt >= 1f ? Mathf.MoveToward(_open, 1f, pose.Dt * 1.5f) : 0.2f;
        _open = open;
        _hinge.Rotation = new Vector3(0f, 0f, Mathf.DegToRad(OpenDeg) * open);
        _mantle.AlbedoColor = new Color(0.1f, 0.07f, 0.09f).Lerp(ToColor(Spec.Accent), pose.Melt);
    }
}
