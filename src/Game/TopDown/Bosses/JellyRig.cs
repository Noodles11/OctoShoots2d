using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Bosses;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown.Bosses;

/// <summary>
/// A cnidarian, hanging in the water just over her plane: a moon jelly (a wide translucent bell, its four gonads
/// showing through as rings, frilly oral arms and a fringe of fine tentacles) or a siphonophore (a colony of small
/// bells coiled into a ring, a curtain of long tentacles trailing from it — its pearl travels round the ring). The bell
/// contracts on the telegraph and pulses with each volley; open, it relaxes wide over the heart. Everything sways like
/// water. Mutations: a second bell fused into the first, a bundle of tentacles too many, ink sacs clouding the bell.
/// </summary>
public partial class JellyRig : BossRig
{
    Node3D _bell = null!;
    readonly List<(Node3D Node, float Phase)> _trailing = new();
    readonly List<Node3D> _colony = new();
    float _pulse;
    bool Siphonophore => Spec.Form == BodyForm.Siphonophore;
    /// <summary>It hangs this high over the seat (its heart at her plane).</summary>
    const float Hang = 0.6f;

    public override Node3D CrustParent => _bell;

    protected override ShaderMaterial MakeSkin()
    {
        var m = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/boss_jelly.gdshader") };
        m.SetShaderParameter("body_color", ToColor(Spec.TrueColor));
        m.SetShaderParameter("accent_color", ToColor(Spec.Accent));
        return m;
    }

    float BellHeight => R * (0.3f + 0.25f * Spec.Bulk);

    public override Vector3 Surface(float angle, float out_)
    {
        float r = Mathf.Clamp(out_, 0f, 1f);
        if (Siphonophore)
            return new Vector3(Mathf.Cos(angle) * R * 0.75f * Spec.Stretch, Hang + R * 0.25f, Mathf.Sin(angle) * R * 0.75f);
        return new Vector3(Mathf.Cos(angle) * r * R * Spec.Stretch, Hang + BellHeight * Mathf.Sqrt(Mathf.Max(0f, 1f - r * r)) * 0.9f, Mathf.Sin(angle) * r * R);
    }

    protected override void BuildBody()
    {
        _bell = new Node3D { Position = new Vector3(0f, Hang, 0f) };
        AddChild(_bell);
        if (Siphonophore) BuildColony();
        else BuildMoon();
        PearlHeight = Hang + 0.2f;
    }

    void BuildMoon()
    {
        float h = BellHeight;
        // The bell: a wide shallow dome, open beneath; four horseshoe gonads round its middle (the accent), clear between.
        var bell = BossMeshes.Lathe(t => new Vector2(R * Mathf.Sin(t * Mathf.Pi * 0.5f + 0.001f) * Spec.Stretch, h * Mathf.Cos(t * Mathf.Pi * 0.5f)), 12, 48,
            (p, u, v) =>
            {
                float a = u * Mathf.Tau, r = v;
                float ring = Mathf.Exp(-Mathf.Pow((r - 0.42f) / 0.11f, 2f)) * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 2f)), 0.6f);
                return new Color(Stain(p), ring, 0f);
            });
        Part(bell, _bell);
        // Frilly oral arms hanging from the middle.
        for (int i = 0; i < Spec.Count; i++)
        {
            float a = Mathf.Tau * i / Spec.Count + 0.4f;
            var path = BossMeshes.Path(t => new Vector3(Mathf.Cos(a) * R * 0.25f * (1f + t), -t * R * 0.9f, Mathf.Sin(a) * R * 0.25f * (1f + t)), 12);
            var arm = new Node3D();
            _bell.AddChild(arm);
            Part(BossMeshes.Tube(path, t => R * 0.09f * (1f - 0.7f * t) * (1f + 0.3f * Mathf.Sin(t * 30f)), 6, (p, t, _) => new Color(Stain(p) * 0.8f, 0.6f, 0f)), arm);
            _trailing.Add((arm, a * 2f));
        }
        // The fringe: fine tentacles all round the rim.
        int fringe = 40;
        for (int i = 0; i < fringe; i++)
        {
            float a = Mathf.Tau * i / fringe;
            var rim = new Vector3(Mathf.Cos(a) * R * Spec.Stretch, 0f, Mathf.Sin(a) * R);
            var path = BossMeshes.Path(t => rim * (1f + 0.15f * t) + Vector3.Down * t * R * 0.55f, 6);
            var t_ = new Node3D();
            _bell.AddChild(t_);
            Part(BossMeshes.Tube(path, t => 0.025f * (1f - t) + 0.005f, 3, (p, _, _) => new Color(Stain(p) * 0.6f, 0.1f, 0f)), t_);
            _trailing.Add((t_, a * 3f));
        }
    }

    void BuildColony()
    {
        // Small bells coiled in a ring round the stem, a curtain of long tentacles trailing out and down from it.
        int bells = 3 + Spec.Count;
        for (int i = 0; i < bells; i++)
        {
            float a = Mathf.Tau * i / bells;
            var at = new Vector3(Mathf.Cos(a) * R * 0.75f * Spec.Stretch, 0f, Mathf.Sin(a) * R * 0.75f);
            var node = new Node3D { Position = at, Rotation = new Vector3(0f, -a, 0.5f) };
            _bell.AddChild(node);
            float r = R * (0.24f + 0.08f * Spec.Bulk), h = r * 1.2f;
            Part(BossMeshes.Lathe(t => new Vector2(r * Mathf.Sin(t * Mathf.Pi * 0.5f + 0.001f), h * Mathf.Cos(t * Mathf.Pi * 0.5f)), 8, 20,
                (p, u, v) => new Color(Stain(node.Transform * p), Mathf.Exp(-Mathf.Pow((v - 0.5f) / 0.15f, 2f)) * 0.8f, 0f)), node);
            _colony.Add(node);
            // The curtain: two long tentacles from each bell.
            for (int k = 0; k < 2; k++)
            {
                var root = at * 1.05f;
                float spread = (k - 0.5f) * 0.25f;
                var path = BossMeshes.Path(t => root + new Vector3(Mathf.Cos(a + spread), 0f, Mathf.Sin(a + spread)) * t * R * 0.9f + Vector3.Down * t * R * 0.6f, 10);
                var tn = new Node3D();
                _bell.AddChild(tn);
                Part(BossMeshes.Tube(path, t => 0.04f * (1f - 0.8f * t) + 0.01f, 4, (p, _, _) => new Color(Stain(p) * 0.7f, 0.3f, 0f)), tn);
                _trailing.Add((tn, a * 2f + k));
            }
        }
        // The stem: a pale ring holding the colony together.
        var stem = BossMeshes.Tube(BossMeshes.Path(t => new Vector3(Mathf.Cos(t * Mathf.Tau) * R * 0.75f * Spec.Stretch, 0.05f, Mathf.Sin(t * Mathf.Tau) * R * 0.75f), 48),
            _ => R * 0.07f, 6, (p, _, _) => new Color(Stain(p), 0.9f, 0f));
        Part(stem, _bell);
    }

    protected override Node3D? Mutation(MutationTheme theme, BodySite site)
    {
        var at = Surface(site.Angle, site.Out);
        switch (theme)
        {
            case MutationTheme.FusedBells:
            {
                // A second, smaller bell grown into the first, the seam between them inked.
                float r = R * (0.32f + 0.3f * site.Size) * (0.8f + 0.4f * Spec.Mutation);
                var bud = new Node3D { Position = at - Vector3.Up * 0.2f, Rotation = new Vector3(0.3f * Mathf.Sin(site.Angle), 0f, -0.3f * Mathf.Cos(site.Angle)) };
                Part(BossMeshes.Lathe(t => new Vector2(r * Mathf.Sin(t * Mathf.Pi * 0.5f + 0.001f), r * 0.55f * Mathf.Cos(t * Mathf.Pi * 0.5f)), 8, 24,
                    (p, u, v) => new Color(0.15f + 0.6f * v * v, 0.5f * Mathf.Exp(-Mathf.Pow((v - 0.45f) / 0.15f, 2f)), 0f)), bud);
                return bud;
            }
            case MutationTheme.ExtraTentacles:
            {
                // A bundle of tentacles too many, sprouting from the bell's side and hanging heavy with ink.
                var bundle = new Node3D { Position = at };
                for (int i = 0; i < 5; i++)
                {
                    float a = site.Angle + (i - 2) * 0.18f;
                    var path = BossMeshes.Path(t => new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * t * R * 0.5f + Vector3.Down * t * R * (0.6f + 0.3f * site.Size), 8);
                    Part(BossMeshes.Tube(path, t => 0.07f * (1f - 0.7f * t), 5, Inked(0.2f)), bundle);
                }
                _trailing.Add((bundle, site.Angle * 5f));
                return bundle;
            }
            default:
                return null;
        }
    }

    public override void Pose(in RigPose pose)
    {
        // The pulse: the bell contracts through the telegraph, snaps open with each volley and eases back; open, it
        // hangs relaxed and wide over the heart. Idle, it breathes slowly.
        if (pose.Fired || pose.Pulsed) _pulse = 1f;
        _pulse = Mathf.MoveToward(_pulse, 0f, pose.Dt / 0.5f);
        float squeeze = pose.Stage == BossStage.Fight && pose.Act == BossAct.Telegraph ? Mathf.SmoothStep(0f, 1f, pose.Warn) : 0f;
        float relax = pose.Open ? 0.06f : 0f;
        float breathe = 0.04f * Mathf.Sin(pose.Time * 1.4f);
        float k = squeeze * 0.18f - _pulse * 0.12f - relax + breathe;
        _bell.Scale = new Vector3(1f - k, 1f + k * 1.6f, 1f - k);
        _bell.Position = new Vector3(0f, Hang + 0.15f * Mathf.Sin(pose.Time * 0.8f) + _pulse * 0.2f, 0f);
        // Everything trailing sways like water, lagging the bell.
        foreach (var (node, phase) in _trailing)
            node.Rotation = new Vector3(0.08f * Mathf.Sin(pose.Time * 1.1f + phase), 0f, 0.08f * Mathf.Cos(pose.Time * 0.9f + phase * 1.3f) + _pulse * 0.1f);
        // A siphonophore's colony pulses one bell after another.
        for (int i = 0; i < _colony.Count; i++)
        {
            float w = 1f + 0.08f * Mathf.Sin(pose.Time * 3f - i * 0.9f);
            _colony[i].Scale = new Vector3(w, 2f - w, w);
        }
        // Freed, it drifts up as it goes (bible §6.5: jellies drift up).
        if (pose.Stage == BossStage.Freed) _bell.Position += Vector3.Up * pose.Melt * 0.8f;
    }
}
