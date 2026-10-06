using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Sim;
using OctoShoots.Game.Fx;
using OctoShoots.Game.Util;

namespace OctoShoots.Game.Enemies;

/// <summary>
/// The Depth 1 den creatures. A view exists only while its creature is alive and near (about 110 m), so a
/// reef full of sleeping dens costs nothing to draw. Every pose comes from the sim state: a wind-up swells,
/// flares, quivers or crouches over exactly the telegraph time.
/// </summary>
public partial class CreatureViews : Node3D
{
    const float ShowRange = 110f;
    const float HideRange = 130f;

    sealed class View
    {
        public required Node3D Root;
        public required EnemyKind Kind;
        public required List<MeshInstance3D> Parts;
        public MeshInstance3D? Body;
        public Node3D? Head;
        public MeshInstance3D? Jaw;
        public MeshInstance3D? Tube;
        public MeshInstance3D? Hole;
        public Label3D? Bang;
        public float Appear;
        public float Spin;
    }

    readonly Dictionary<int, View> _views = new();
    readonly Dictionary<EnemyKind, ArrayMesh> _meshes = new();
    readonly Dictionary<EnemyKind, ShaderMaterial> _materials = new();
    ArrayMesh _morayHead = null!, _morayJaw = null!, _morayBody = null!, _morayHole = null!;
    ShaderMaterial _morayMaterial = null!;
    float _time;

    public override void _Ready()
    {
        var shader = GD.Load<Shader>("res://assets/shaders/creature.gdshader");
        var jelly = GD.Load<Shader>("res://assets/shaders/creature_jelly.gdshader");
        ShaderMaterial Material(Shader s, float halfLength, Color glow, float spineRest = 0.65f, float rough = 0.55f)
        {
            var m = new ShaderMaterial { Shader = s };
            if (s == shader)
            {
                m.SetShaderParameter("half_len", halfLength);
                m.SetShaderParameter("glow_color", glow);
                m.SetShaderParameter("spine_rest", spineRest);
                m.SetShaderParameter("rough", rough);
            }
            return m;
        }

        _meshes[EnemyKind.SpanishDancer] = CreatureMeshes.Dancer();
        _materials[EnemyKind.SpanishDancer] = Material(shader, 0.8f, new Color(1f, 0.45f, 0.2f), rough: 0.45f);
        _meshes[EnemyKind.Pufferling] = CreatureMeshes.Puffer();
        _materials[EnemyKind.Pufferling] = Material(shader, 0.45f, new Color(1f, 0.9f, 0.4f), spineRest: 0.62f, rough: 0.4f);
        _meshes[EnemyKind.Barracuda] = CreatureMeshes.Barracuda();
        _materials[EnemyKind.Barracuda] = Material(shader, 1.1f, new Color(0.7f, 0.9f, 1f), rough: 0.25f);
        _meshes[EnemyKind.MoonJelly] = CreatureMeshes.Jelly();
        _materials[EnemyKind.MoonJelly] = Material(jelly, 0f, Colors.White);
        _meshes[EnemyKind.SeaUrchin] = CreatureMeshes.Urchin();
        _materials[EnemyKind.SeaUrchin] = Material(shader, 0.3f, new Color(0.75f, 0.4f, 1f), spineRest: 0.6f, rough: 0.35f);
        _meshes[EnemyKind.Crabby] = CreatureMeshes.Crab();
        _materials[EnemyKind.Crabby] = Material(shader, 0.4f, new Color(1f, 0.5f, 0.25f), rough: 0.4f);

        _morayHead = CreatureMeshes.MorayHead();
        _morayJaw = CreatureMeshes.MorayJaw();
        _morayBody = CreatureMeshes.MorayBody();
        _morayHole = CreatureMeshes.MorayHole();
        _morayMaterial = Material(shader, 0.5f, new Color(1f, 0.9f, 0.4f), rough: 0.5f);
    }

    public void Clear()
    {
        foreach (var v in _views.Values)
        {
            v.Root.QueueFree();
            v.Bang?.QueueFree();
        }
        _views.Clear();
    }

    void Remove(int id, View view)
    {
        view.Root.QueueFree();
        view.Bang?.QueueFree();
        _views.Remove(id);
    }

    /// <summary>Scales along the basis's own axes (Basis.Scaled scales the world axes instead).</summary>
    static Basis ScaleLocal(Basis b, Vector3 s) => new(b.Column0 * s.X, b.Column1 * s.Y, b.Column2 * s.Z);

    public void Sync(World world, Vector3 camera, float alpha, float dt)
    {
        _time += dt;
        foreach (var e in world.Enemies)
        {
            if (!World.IsDenCreature(e.Kind)) continue;
            Vector3 position = Conv.Lerp(e.PrevPosition, e.Position, alpha);
            float distance = position.DistanceTo(camera);
            _views.TryGetValue(e.Id, out var view);
            if (!e.Alive || distance > (view is null ? ShowRange : HideRange))
            {
                if (view is not null) Remove(e.Id, view);
                continue;
            }
            view ??= Build(e);
            view.Appear = Mathf.Min(1f, view.Appear + dt / 0.4f);
            Pose(world, e, view, position, alpha, dt);
        }
    }

    // ───────────────────────── building ─────────────────────────

    View Build(Enemy e)
    {
        var root = new Node3D();
        AddChild(root);
        var parts = new List<MeshInstance3D>();
        MeshInstance3D Part(Node3D parent, Mesh mesh, Material material)
        {
            var m = new MeshInstance3D { Mesh = mesh, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            parent.AddChild(m);
            parts.Add(m);
            return m;
        }

        View view;
        if (e.Kind == EnemyKind.Moray)
        {
            var hole = Part(root, _morayHole, _morayMaterial);
            var tube = Part(root, _morayBody, _morayMaterial);
            var head = new Node3D();
            root.AddChild(head);
            Part(head, _morayHead, _morayMaterial);
            var jaw = Part(head, _morayJaw, _morayMaterial);
            jaw.Position = new Vector3(0f, -0.1f, 0.1f);
            view = new View { Root = root, Kind = e.Kind, Parts = parts, Head = head, Jaw = jaw, Tube = tube, Hole = hole };
        }
        else
        {
            var body = Part(root, _meshes[e.Kind], _materials[e.Kind]);
            view = new View { Root = root, Kind = e.Kind, Parts = parts, Body = body };
        }

        // The startle mark: a bold "!" that pops over a creature that has just noticed her.
        view.Bang = new Label3D
        {
            Text = "!",
            FontSize = 220,
            PixelSize = 0.004f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Shaded = false,
            Modulate = new Color(1f, 0.85f, 0.2f),
            OutlineModulate = new Color(0.55f, 0.05f, 0.05f),
            OutlineSize = 28,
            Visible = false,
            RenderPriority = 3,
        };
        AddChild(view.Bang);

        foreach (var part in parts)
        {
            part.SetInstanceShaderParameter("phase", (e.Id * 1.7f) % Mathf.Tau);
        }
        _views[e.Id] = view;
        return view;
    }

    // ───────────────────────── posing ─────────────────────────

    static float Charge(World world, Enemy e) =>
        e.State == EnemyState.Telegraph ? 1f - Mathf.Clamp(e.StateTimer / world.TelegraphTime(e), 0f, 1f) : 0f;

    static void Set(View v, string name, Variant value)
    {
        foreach (var part in v.Parts) part.SetInstanceShaderParameter(name, value);
    }

    void Pose(World world, Enemy e, View v, Vector3 position, float alpha, float dt)
    {
        float charge = Charge(world, e);
        float scale = world.ScaleOf(e) * Mathf.SmoothStep(0.2f, 1f, v.Appear);
        float speed = e.Velocity.Length();
        float pulse = 0f, quiver = 0f, glow = 0f, wag = 0f;
        Vector3 facing = e.Facing.G();

        switch (e.Kind)
        {
            case EnemyKind.SpanishDancer:
            {
                // Flares its ruffles over the wind-up, then spins dizzily.
                pulse = e.State == EnemyState.Telegraph ? charge : e.State == EnemyState.Recover ? 0.4f : 0f;
                glow = pulse;
                if (e.State == EnemyState.Recover) v.Spin += dt * 7f;
                Vector3 flat = new(facing.X, facing.Y * 0.4f, facing.Z);
                v.Root.Transform = new Transform3D(Conv.LookAlong(flat).Rotated(Vector3.Up, v.Spin).Scaled(Vector3.One * scale), position);
                break;
            }
            case EnemyKind.Pufferling:
            {
                pulse = e.State == EnemyState.Attack ? 1f : charge;
                glow = e.State == EnemyState.Attack ? 0.5f : 0f;
                wag = 1f;
                v.Root.Transform = new Transform3D(Conv.LookAlong(facing).Scaled(Vector3.One * scale), position);
                break;
            }
            case EnemyKind.Barracuda:
            {
                quiver = charge;
                glow = e.State == EnemyState.Attack ? 1f : charge;
                wag = 1f;
                v.Root.Transform = new Transform3D(Conv.LookAlong(facing).Scaled(Vector3.One * scale), position);
                break;
            }
            case EnemyKind.MoonJelly:
            {
                // The bell contracts during the wind-up, springs open during the lunge, and beats gently otherwise.
                pulse = e.State == EnemyState.Telegraph ? charge : e.State == EnemyState.Attack ? 1f - Mathf.Clamp(e.StateTimer * 3f, 0f, 1f) * 0.2f : 0.5f + 0.5f * Mathf.Sin(_time * 2.2f + e.Id);
                pulse *= e.State is EnemyState.Telegraph or EnemyState.Attack ? 1f : 0.35f;
                glow = 0.4f + pulse * 0.5f;
                Vector3 up = Vector3.Up + e.Velocity.G() * 0.12f;
                v.Root.Transform = new Transform3D(Conv.AlignUp(up, 0f).Scaled(Vector3.One * scale), position);
                break;
            }
            case EnemyKind.SeaUrchin:
            {
                // Spines lie close while it sleeps, stand when it notices her and rattle through the wind-up.
                pulse = e.State switch { EnemyState.Idle => 0f, EnemyState.Alert => 0.5f, EnemyState.Telegraph => 0.5f + 0.5f * charge, EnemyState.Recover => 1f, _ => 0.5f };
                if (e.Small) pulse = 0.35f;
                quiver = charge;
                glow = charge;
                v.Root.Transform = new Transform3D(Conv.AlignUp(e.Up.G(), e.Id).Scaled(Vector3.One * scale), position);
                break;
            }
            case EnemyKind.Crabby:
            {
                // Crouches before the leap; legs scurry with its speed.
                Vector3 flat = new(facing.X, 0f, facing.Z);
                float squash = 1f - 0.4f * charge;
                var basis = ScaleLocal(Conv.LookAlong(flat), new Vector3(1f + 0.15f * charge, squash, 1f + 0.15f * charge) * scale);
                v.Root.Transform = new Transform3D(basis, position);
                glow = charge * 0.6f;
                break;
            }
            case EnemyKind.Moray:
                PoseMoray(world, e, v, charge);
                break;
        }

        // Status tints and the hit flash.
        Color tint = Colors.White;
        float amount = 0f;
        if (e.FrozenTimer > 0f) { tint = new Color(0.6f, 0.85f, 1f); amount = 0.65f; }
        else if (e.StunTimer > 0f) { tint = new Color(0.7f, 0.7f, 0.75f); amount = 0.5f; }
        else if (e.CharmTimer > 0f) { tint = new Color(1f, 0.45f, 0.8f); amount = 0.5f; }
        if (e.BurnTimer > 0f) { tint = new Color(1f, 0.45f, 0.1f); amount = 0.55f; }
        else if (e.PoisonTimer > 0f) { tint = new Color(0.3f, 0.85f, 0.2f); amount = 0.45f; }

        Set(v, "swim", Mathf.Clamp(speed / 4f, 0f, 1f));
        Set(v, "wag", wag);
        Set(v, "pulse", pulse);
        Set(v, "quiver", quiver);
        Set(v, "glow", glow);
        Set(v, "flash", e.HurtTimer > 0f ? 0.9f : 0f);
        Set(v, "tint", tint);
        Set(v, "tint_amount", amount);

        // The startle mark.
        if (v.Bang is { } bang)
        {
            bool show = e.State == EnemyState.Alert;
            bang.Visible = show;
            if (show)
            {
                float t = 1f - Mathf.Clamp(e.StateTimer / 0.6f, 0f, 1f);
                float pop = t < 0.25f ? Mathf.Lerp(0.3f, 1.35f, t / 0.25f) : Mathf.Lerp(1.35f, 1f, Mathf.Min(1f, (t - 0.25f) / 0.2f));
                bang.GlobalPosition = position + Vector3.Up * (world.RadiusOf(e) * 1.2f + 0.9f);
                bang.Scale = Vector3.One * pop * Mathf.Clamp(position.DistanceTo(GetViewport().GetCamera3D().GlobalPosition) / 14f, 1f, 3.5f);
            }
        }
    }

    void PoseMoray(World world, Enemy e, View v, float charge)
    {
        Vector3 up = e.Up.G();
        Vector3 anchor = e.Anchor.G();
        v.Root.Transform = new Transform3D(Conv.AlignUp(up, 0f), anchor);
        v.Hole!.Position = Vector3.Zero;

        Vector3 aim = e.Aim.G();
        Vector3 head = e.Position.G();
        v.Head!.GlobalTransform = new Transform3D(Conv.LookAlong(aim), head);
        // Jaw: closed asleep, gaping through the wind-up and the lunge.
        float gape = e.State switch { EnemyState.Telegraph => charge, EnemyState.Attack or EnemyState.Recover => 1f, EnemyState.Alert or EnemyState.Hunt => 0.2f, _ => 0f };
        v.Jaw!.Rotation = new Vector3(-Mathf.DegToRad(30f) * gape, 0f, 0f);

        // Body: from the head back into the hole.
        Vector3 start = anchor + up * 0.1f;
        Vector3 back = start - head;
        float length = back.Length();
        v.Tube!.Visible = length > 0.15f;
        if (length > 0.15f)
        {
            var basis = ScaleLocal(Conv.LookAlong(-back / length), new Vector3(1f, 1f, length));
            v.Tube.GlobalTransform = new Transform3D(basis, head);
        }
    }
}
