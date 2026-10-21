using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Bosses;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown.Bosses;

/// <summary>
/// A gastropod on the floor: a sea hare (a soft, heavy body along its facing, two parapodia flaps folded over its back,
/// two rolled rhinophores at the front — never eyes — mottled with pale rings) or a snail (a broad muscular foot under
/// a coiled shell, two tentacles). A sea hare's flaps part and it rears up on the telegraph, and its body ripples as it
/// creeps; a snail draws its foot into the shell between openings and comes out when open. Mutations: extra flaps,
/// extra tentacles, ink sacs along the mantle, the shell overgrowing the animal.
/// </summary>
public partial class GastropodRig : BossRig
{
    Node3D _body = null!, _front = null!;
    readonly List<(Node3D Flap, float Side)> _flaps = new();
    Node3D? _shell, _foot;
    float _rear, _out;
    bool Snail => Spec.Form == BodyForm.Snail;
    float Length => R * Spec.Stretch;
    float Height => R * (0.4f + 0.25f * Spec.Bulk);

    public override Node3D CrustParent => Snail && _shell is not null ? _shell : _body;

    public override Vector3 Surface(float angle, float out_)
    {
        float x = Mathf.Cos(angle) * out_ * Length, z = Mathf.Sin(angle) * out_ * R * 0.6f;
        float along = Mathf.Clamp(x / Length, -1f, 1f);
        float h = Height * Mathf.Sqrt(Mathf.Max(0f, 1f - along * along)) * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(z / (R * 0.6f), 2f)));
        return new Vector3(x, (Snail ? h * 0.5f : h) + 0.05f, z);
    }

    protected override void BuildBody()
    {
        _body = new Node3D();
        AddChild(_body);
        if (Snail) BuildSnail();
        else BuildHare();
    }

    void BuildHare()
    {
        // The body: a soft tube along the facing, thickest behind its middle, its back mottled with pale rings.
        var path = BossMeshes.Path(t => new Vector3(Mathf.Lerp(-Length, Length, t), 0f, 0f), 24);
        var body = BossMeshes.Tube(path, t => R * 0.6f * Mathf.Pow(Mathf.Sin(Mathf.Pi * Mathf.Clamp(t * 1.05f, 0.02f, 0.98f)), 0.6f), 16,
            (p, t, around) =>
            {
                float rings = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(p.X * 3.1f) * Mathf.Sin(p.Z * 4.3f + around * 9f)), 5f);
                return new Color(Stain(p), rings, 0.25f * Mathf.Abs(Mathf.Sin(around * Mathf.Pi * 7f)));
            }, t => Height / (R * 0.6f));
        Part(body, _body).Position = new Vector3(0f, Height * 0.55f, 0f);
        // The parapodia: two flaps folded up over the back, parting on the telegraph.
        for (int side = -1; side <= 1; side += 2) AddFlap(side, 0f, 1f);
        // The front: two rolled rhinophores (scent organs, not eyes), and the oral tentacles.
        _front = new Node3D { Position = new Vector3(Length * 0.9f, Height * 0.7f, 0f) };
        _body.AddChild(_front);
        for (int side = -1; side <= 1; side += 2)
        {
            var horn = BossMeshes.Tube(BossMeshes.Path(t => new Vector3(t * 0.25f * R, t * 0.45f * R, side * (0.12f + 0.08f * t) * R), 8),
                t => 0.08f * R * (1f - 0.7f * t), 6, (p, t, a) => new Color(0.2f, 0.4f + 0.4f * Mathf.Sin(t * 20f), 0.4f));
            Part(horn, _front);
        }
        PearlHeight = Height * 1.1f;
    }

    /// <summary>A parapodium flap along the back on one side (side −1 or 1), at offset along the body, of size scale.</summary>
    Node3D AddFlap(float side, float offset, float scale, bool mutated = false)
    {
        var pivot = new Node3D { Position = new Vector3(offset * Length, Height * 0.95f, side * R * 0.22f) };
        _body.AddChild(pivot);
        float len = Length * 1.1f * scale, rise = Height * 0.8f * scale;
        var flap = BossMeshes.Sheet((u, v) => new Vector3(Mathf.Lerp(-len * 0.5f, len * 0.5f, u), v * rise * Mathf.Sin(u * Mathf.Pi), side * v * rise * 0.35f * (1f + 0.3f * Mathf.Sin(u * 9f))),
            16, 6, (p, u, v) => new Color(mutated ? 1f - 0.3f * v : Stain(pivot.Transform * p), 0.4f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(u * 20f) * Mathf.Sin(v * 9f)), 3f), 0.3f * v));
        Part(flap, pivot);
        _flaps.Add((pivot, side));
        return pivot;
    }

    void BuildSnail()
    {
        // The foot: broad and flat, its edge frilled; the head and two tentacles in front.
        _foot = new Node3D();
        _body.AddChild(_foot);
        var foot = BossMeshes.Lathe(t => new Vector2(R * 0.95f * Mathf.Sin(t * Mathf.Pi * 0.5f + 0.001f), R * 0.2f * Mathf.Cos(t * Mathf.Pi * 0.5f)), 6, 32,
            (p, u, v) => new Color(Stain(p), 0f, 0.2f * v));
        Part(foot, _foot).Scale = new Vector3(Spec.Stretch * 1.15f, 1f, 0.7f);
        _front = new Node3D { Position = new Vector3(Length * 0.95f, R * 0.18f, 0f) };
        _foot.AddChild(_front);
        for (int side = -1; side <= 1; side += 2)
            Part(BossMeshes.Tube(BossMeshes.Path(t => new Vector3(t * 0.4f * R, t * 0.35f * R, side * (0.1f + 0.15f * t) * R), 8),
                t => 0.06f * R * (1f - 0.6f * t), 5, (p, _, _) => new Color(0.15f, 0f, 0.2f)), _front);
        // The shell: a coiled cone of Count whorls, banded along the coil (the accent), its lip toward the front.
        _shell = new Node3D { Position = new Vector3(-R * 0.15f, R * 0.15f, 0f), Rotation = new Vector3(0f, 0f, -0.25f) };
        _body.AddChild(_shell);
        int whorls = Spec.Count;
        float height = R * (0.7f + 0.5f * Spec.Bulk);
        var coil = BossMeshes.Path(t =>
        {
            float turn = t * whorls * Mathf.Tau, r = R * 0.75f * (1f - 0.92f * t);
            return new Vector3(Mathf.Cos(turn) * r, t * height, Mathf.Sin(turn) * r);
        }, whorls * 24);
        Part(BossMeshes.Tube(coil, t => R * 0.42f * (1f - 0.9f * t) + 0.03f, 14,
            (p, t, a) => new Color(Stain(_shell.Transform * p), 0.5f + 0.5f * Mathf.Sin(t * whorls * 14f), 0.4f * Mathf.Abs(Mathf.Sin(t * 120f)))), _shell);
        PearlHeight = R * 0.35f;
    }

    protected override Node3D? Mutation(MutationTheme theme, BodySite site)
    {
        var at = Surface(site.Angle, site.Out);
        switch (theme)
        {
            case MutationTheme.ExtraFlaps when !Snail:
                return AddFlap(Mathf.Sin(site.Angle) >= 0f ? 1f : -1f, Mathf.Cos(site.Angle) * site.Out * 0.6f, 0.45f + 0.4f * site.Size, true);
            case MutationTheme.ExtraTentacles:
            case MutationTheme.ExtraFlaps:
            {
                // Tentacles growing where none belong, curling up off the back.
                var bundle = new Node3D { Position = at };
                for (int i = 0; i < 4; i++)
                {
                    float a = site.Angle + (i - 1.5f) * 0.5f;
                    var path = BossMeshes.Path(t => new Vector3(Mathf.Cos(a) * t * 0.4f * R, t * R * (0.45f + 0.3f * site.Size) - t * t * 0.2f * R, Mathf.Sin(a) * t * 0.4f * R), 8);
                    Part(BossMeshes.Tube(path, t => 0.07f * R * (1f - 0.75f * t), 5, Inked(0.3f)), bundle);
                }
                return bundle;
            }
            case MutationTheme.OvergrownShell:
            {
                // The shell keeps growing past the animal: a second, crooked whorl swallowing the foot's edge.
                var growth = new Node3D { Position = at, Rotation = new Vector3(0.4f, site.Angle, 0.3f) };
                float s = R * (0.35f + 0.35f * site.Size) * (0.8f + 0.5f * Spec.Mutation);
                var coil = BossMeshes.Path(t => new Vector3(Mathf.Cos(t * 9f) * s * (1f - t), t * s * 1.3f, Mathf.Sin(t * 9f) * s * (1f - t)), 30);
                Part(BossMeshes.Tube(coil, t => s * 0.45f * (1f - 0.85f * t) + 0.02f, 10, Inked(0.6f)), growth);
                return growth;
            }
            default:
                return null;
        }
    }

    public override void Pose(in RigPose pose)
    {
        bool fighting = pose.Stage == BossStage.Fight;
        // A sea hare rears up through the telegraph and its flaps part; it sinks back in its window.
        float rear = fighting && pose.Act == BossAct.Telegraph ? Mathf.SmoothStep(0f, 1f, pose.Warn) : fighting && pose.Act is BossAct.Act ? 0.6f : 0f;
        _rear = Mathf.MoveToward(_rear, rear, pose.Dt * 2.5f);
        if (!Snail)
        {
            _body.Rotation = new Vector3(0f, 0f, 0.35f * _rear);
            foreach (var (flap, side) in _flaps)
                flap.Rotation = new Vector3(side * (0.15f + 0.5f * _rear + 0.05f * Mathf.Sin(pose.Time * 1.3f + side)), 0f, 0f);
            // Creeping, a wave runs back along the body.
            float wave = pose.Speed > 0.05f ? 0.05f * Mathf.Sin(pose.Time * 5f) : 0.015f * Mathf.Sin(pose.Time * 1.2f);
            _body.Scale = new Vector3(1f + wave, 1f - wave * 0.5f, 1f);
        }
        else if (_foot is not null)
        {
            // A snail comes out of its shell to open (and to creep), and draws back in between.
            float target = !fighting ? 1f : pose.Open || pose.Act == BossAct.Telegraph ? 1f : pose.Speed > 0.05f ? 0.7f : 0.25f;
            _out = Mathf.MoveToward(_out, target, pose.Dt * 2f);
            _foot.Scale = new Vector3(0.55f + 0.45f * _out, 0.6f + 0.4f * _out, 0.6f + 0.4f * _out);
            _front.Visible = _out > 0.4f;
        }
    }
}
