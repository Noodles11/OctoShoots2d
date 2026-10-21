using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Bosses;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown.Bosses;

/// <summary>
/// An echinoderm: a sea star (a domed disc and its tapering arms, knobbly, banded in its accent) or an urchin (a
/// flattened test under a coat of spines). It lies on the floor. A star's arms curl up on the telegraph and slam down
/// with its pulse, and ripple as it creeps; an urchin's spines bristle on the telegraph and sway. Mutations: extra arms
/// crowding between the true ones, spines grown inward through the body (dark spikes), ink sacs on the disc.
/// </summary>
public partial class StarRig : BossRig
{
    readonly List<(Node3D Pivot, float Angle, float Phase)> _arms = new();
    Node3D _body = null!;
    MultiMeshInstance3D? _spines;
    float _slam = 1f, _curl;
    bool Urchin => Spec.Form == BodyForm.Urchin;
    float Disc => Urchin ? R * 0.62f : R * 0.36f;
    float Dome => Urchin ? R * 0.42f : R * 0.16f;

    public override Node3D CrustParent => _body;

    public override Vector3 Surface(float angle, float out_)
    {
        // On the arm the angle points along (a star), or over the test (an urchin).
        float r = out_ * R;
        float x = Mathf.Cos(angle) * r * Spec.Stretch, z = Mathf.Sin(angle) * r;
        float h = r <= Disc ? Dome * Mathf.Sqrt(Mathf.Max(0f, 1f - (r / Disc) * (r / Disc))) : 0.12f * (1f - (r - Disc) / (R - Disc + 0.01f)) + 0.05f;
        return new Vector3(x, h + 0.04f, z);
    }

    protected override void BuildBody()
    {
        _body = new Node3D();
        AddChild(_body);
        if (Urchin) BuildUrchin();
        else BuildStar();
        PearlHeight = Dome + 0.15f;
    }

    void BuildStar()
    {
        // The disc: a low dome, its stain from the sites, a pale ring of accent knobs.
        var disc = BossMeshes.Lathe(t => new Vector2(Disc * Mathf.Sin(t * Mathf.Pi * 0.5f + 0.001f) * 1.05f, Dome * Mathf.Cos(t * Mathf.Pi * 0.5f)), 10, 40,
            (p, u, v) => new Color(Stain(p), Knob(p, 9f) * 0.8f, 0.2f * v));
        Part(disc, _body);
        float length = R * (0.85f + 0.15f * Spec.Bulk) - Disc * 0.4f;
        for (int i = 0; i < Spec.Count; i++)
        {
            float a = Mathf.Tau * i / Spec.Count + Mathf.Pi / Spec.Count;
            AddArm(a, length, Disc * 0.55f, false);
        }
    }

    /// <summary>One arm: a flattened tube tapering to its tip, knobbly along its back (its accent), on a pivot that curls it.</summary>
    Node3D AddArm(float angle, float length, float width, bool mutated)
    {
        var pivot = new Node3D { Position = new Vector3(Mathf.Cos(angle) * Disc * 0.55f * Spec.Stretch, 0.05f, Mathf.Sin(angle) * Disc * 0.55f), Rotation = new Vector3(0f, -angle, 0f) };
        _body.AddChild(pivot);
        var path = BossMeshes.Path(t => new Vector3(t * length, 0.05f * Mathf.Sin(t * Mathf.Pi), 0f), 14);
        var mesh = BossMeshes.Tube(path, t => width * (1f - 0.82f * t) + 0.02f, 10,
            (p, t, around) =>
            {
                var world = pivot.Transform * p;
                float back = Mathf.Max(0f, Mathf.Sin(around * Mathf.Tau));
                float knob = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 30f)), 6f) * back;
                return new Color(mutated ? 1f - 0.3f * t : Stain(world), knob * 0.9f, 0.3f * (1f - back));
            }, _ => 0.45f);
        Part(mesh, pivot);
        _arms.Add((pivot, angle, angle * 1.7f));
        return pivot;
    }

    void BuildUrchin()
    {
        // The test: a flattened sphere, banded in rows (its accent), the spines on it.
        var test = BossMeshes.Lathe(t => new Vector2(Disc * Mathf.Sin(t * Mathf.Pi * 0.55f + 0.001f), Dome * Mathf.Cos(t * Mathf.Pi * 0.55f)), 12, 40,
            (p, u, v) => new Color(Stain(p), 0.5f + 0.5f * Mathf.Sin(u * Mathf.Tau * Spec.Count), 0.3f));
        Part(test, _body);
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = BossMeshes.Cone(5) };
        var rng = new RandomNumberGenerator { Seed = Spec.Seed ^ 0x5419EUL };
        // Fewer, stouter spines than a brush: rows by its count, each spine long and tapering, radiating from the test.
        int rows = 4 + Spec.Count / 4, perRow = 16;
        mm.InstanceCount = rows * perRow;
        float spine = R * (0.3f + 0.3f * Spec.Bulk);
        int k = 0;
        for (int row = 0; row < rows; row++)
        for (int j = 0; j < perRow; j++, k++)
        {
            float v = (row + 0.5f) / rows * 0.9f, a = Mathf.Tau * (j + 0.5f * (row % 2)) / perRow;
            var p = new Vector3(Mathf.Cos(a) * Disc * Mathf.Sin(v * Mathf.Pi * 0.55f), Dome * Mathf.Cos(v * Mathf.Pi * 0.55f), Mathf.Sin(a) * Disc * Mathf.Sin(v * Mathf.Pi * 0.55f));
            // Radiating out from the test (leaning outward more than up, so from above it is a burst of spines, not a tuft).
            var n = new Vector3(p.X / Disc, p.Y / Dome * 0.4f + 0.15f, p.Z / Disc).Normalized();
            var basis = new Basis(new Quaternion(Vector3.Up, n)).Scaled(new Vector3(0.11f, spine * rng.RandfRange(0.75f, 1.1f), 0.11f));
            mm.SetInstanceTransform(k, new Transform3D(basis, p));
            mm.SetInstanceColor(k, new Color(Stain(p), 0.2f, 0.4f));
        }
        _spines = new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = Skin };
        _body.AddChild(_spines);
        Skinned.Add(_spines);
    }

    /// <summary>A spot pattern of knobs over the disc (its accent).</summary>
    static float Knob(Vector3 p, float freq) => Mathf.Pow(Mathf.Max(0f, Mathf.Sin(p.X * freq) * Mathf.Sin(p.Z * freq)), 4f);

    protected override Node3D? Mutation(MutationTheme theme, BodySite site)
    {
        switch (theme)
        {
            case MutationTheme.ExtraArms when !Urchin:
            {
                // An arm too many, crowding in between the true ones: thinner, stunted, inked through.
                float length = R * (0.5f + 0.3f * site.Size) - Disc * 0.4f;
                var arm = AddArm(site.Angle, length, Disc * 0.38f, true);
                return arm;
            }
            case MutationTheme.ExtraArms:
            case MutationTheme.InvertedSpines:
            {
                // Spines grown inward: a cluster of dark spikes driven back into the body, stubs standing out of it.
                var at = Surface(site.Angle, site.Out);
                var cluster = new Node3D { Position = at };
                var rng = new RandomNumberGenerator { Seed = (ulong)(site.Angle * 1000f + 7f) };
                int n = 5 + (int)(site.Size * 6f);
                for (int i = 0; i < n; i++)
                {
                    var spike = Part(BossMeshes.Cone(5), cluster);
                    spike.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("surge"), 0f);
                    float tilt = rng.RandfRange(0.4f, 1.1f), turn = rng.RandfRange(0f, Mathf.Tau);
                    spike.Rotation = new Vector3(tilt * Mathf.Cos(turn), 0f, tilt * Mathf.Sin(turn));
                    spike.Scale = new Vector3(0.07f, (0.35f + site.Size) * (0.7f + 0.6f * Spec.Mutation), 0.07f);
                    spike.MaterialOverride = Skin;
                    // Painted solid ink: these are the Leak's, not the animal's.
                    spike.Mesh = BossMeshes.Lathe(t => new Vector2(t, 1f - t), 2, 5, (_, _, _) => new Color(1f, 0f, 0.5f));
                }
                return cluster;
            }
            default:
                return null;
        }
    }

    public override void Pose(in RigPose pose)
    {
        // The curl: arms lift through the telegraph, slam down with the pulse, and lie loose otherwise.
        float target = pose.Stage == BossStage.Fight && pose.Act == BossAct.Telegraph ? 0.45f * pose.Warn : 0f;
        if (pose.Pulsed || pose.Fired) _slam = 0f;
        _slam = Mathf.MoveToward(_slam, 1f, pose.Dt / 0.4f);
        _curl = Mathf.MoveToward(_curl, target, pose.Dt * 2f);
        float slamDip = (1f - _slam) * 0.25f;
        foreach (var (pivot, angle, phase) in _arms)
        {
            // Creeping, the arms ripple one after another; still, they sway a hair, like water moving them.
            float ripple = 0.06f * Mathf.Sin(pose.Time * (1.2f + pose.Speed * 2f) + phase) * (0.4f + pose.Speed);
            pivot.Rotation = new Vector3(0f, -angle, _curl - slamDip + ripple);
        }
        if (_spines is not null)
        {
            // An urchin bristles on the telegraph (and every spine leans with the swell).
            float bristle = 1f + (pose.Act == BossAct.Telegraph ? 0.2f * pose.Warn : 0f) + 0.15f * (1f - _slam);
            _spines.Scale = new Vector3(1f, bristle, 1f);
            _spines.Rotation = new Vector3(0.03f * Mathf.Sin(pose.Time * 0.9f), 0f, 0.03f * Mathf.Cos(pose.Time * 0.7f));
        }
        // Freed, its spines lie down (bible §6.5) and it lifts a little.
        if (pose.Stage == BossStage.Freed && _spines is not null) _spines.Scale = new Vector3(1f, 1f - 0.4f * pose.Melt, 1f);
    }
}
