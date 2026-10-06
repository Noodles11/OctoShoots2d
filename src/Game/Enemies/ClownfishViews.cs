using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Sim;
using OctoShoots.Game.Fx;
using OctoShoots.Game.Util;

namespace OctoShoots.Game.Enemies;

/// <summary>
/// The neutral clownfish and the clownfish ninjas. A ninja is the same fish with a dark headband and
/// two trailing ribbons: during a wind-up the band glows red and a starfish grows at its mouth.
/// </summary>
public partial class ClownfishViews : Node3D
{
    sealed class FishView
    {
        public required Node3D Root;
        public required MeshInstance3D Body;
        public MeshInstance3D? Band;
        public MeshInstance3D? Star;
        public float Size = 1f;
        public float Appear = 1f;
        public bool WasAlive;
    }

    readonly Dictionary<int, FishView> _views = new();
    ArrayMesh _bodyMesh = null!;
    ArrayMesh _bandMesh = null!;
    ArrayMesh _starMesh = null!;
    ShaderMaterial _material = null!;
    StandardMaterial3D _starMaterial = null!;
    float _time;

    public override void _Ready()
    {
        _bodyMesh = ReefMeshes.Clownfish();
        _bandMesh = ReefMeshes.Headband();
        _starMesh = ReefMeshes.Starfish();
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/clownfish.gdshader") };
        _starMaterial = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            EmissionEnabled = true,
            Emission = new Color(1f, 0.35f, 0.1f),
            EmissionEnergyMultiplier = 0.9f,
        };
    }

    public void Clear()
    {
        foreach (var v in _views.Values) v.Root.QueueFree();
        _views.Clear();
    }

    public void Sync(World world, float alpha, float dt)
    {
        _time += dt;
        var seen = new HashSet<int>();

        foreach (var f in world.Clownfish)
        {
            seen.Add(f.Id);
            var view = Get(f.Id, ninja: false);
            Place(view, Conv.Lerp(f.PrevPosition, f.Position, alpha), f.Facing.G(), view.Size);
            SetSwim(view.Body, f.Id, f.Velocity.Length());
        }

        foreach (var e in world.Enemies.Where(e => e.Kind == EnemyKind.ClownNinja))
        {
            seen.Add(e.Id);
            var view = Get(e.Id, ninja: true);
            SyncNinja(world, e, view, alpha, dt);
        }

        foreach (int id in _views.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            _views[id].Root.QueueFree();
            _views.Remove(id);
        }
    }

    void SyncNinja(World world, Enemy e, FishView view, float alpha, float dt)
    {
        view.Root.Visible = e.Alive;
        if (!e.Alive)
        {
            view.WasAlive = false;
            return;
        }
        if (!view.WasAlive)
        {
            view.WasAlive = true;
            view.Appear = 0f; // pops out of the anemone
        }
        view.Appear = Mathf.Min(1f, view.Appear + dt / 0.4f);

        float charge = 0f;
        if (e.State == EnemyState.Telegraph)
            charge = 1f - Mathf.Clamp(e.StateTimer / world.TelegraphTime(e), 0f, 1f);

        float size = view.Size * Mathf.SmoothStep(0.1f, 1f, view.Appear) * (1f + 0.12f * charge);
        Place(view, Conv.Lerp(e.PrevPosition, e.Position, alpha), e.Facing.G(), size);
        SetSwim(view.Body, e.Id, e.Velocity.Length());

        // The wind-up: the band glows and pulses, a starfish grows at the mouth.
        float glow = charge * (0.65f + 0.35f * Mathf.Sin(_time * 38f));
        view.Band!.SetInstanceShaderParameter("glow", glow);
        view.Band.SetInstanceShaderParameter("swim", Mathf.Clamp(e.Velocity.Length() / 3.5f, 0f, 1f));
        view.Star!.Visible = charge > 0.05f;
        view.Star.Transform = new Transform3D(
            (new Basis(Vector3.Right, -Mathf.Pi / 2f) * new Basis(Vector3.Up, _time * 9f)).Scaled(Vector3.One * 0.17f * charge),
            new Vector3(0f, -0.02f, -0.36f - 0.04f * charge));

        // Status tints and the hit flash.
        Color tint = Colors.White;
        float amount = 0f;
        if (e.FrozenTimer > 0f) { tint = new Color(0.6f, 0.85f, 1f); amount = 0.65f; }
        else if (e.StunTimer > 0f) { tint = new Color(0.7f, 0.7f, 0.75f); amount = 0.5f; }
        else if (e.CharmTimer > 0f) { tint = new Color(1f, 0.45f, 0.8f); amount = 0.5f; }
        if (e.BurnTimer > 0f) { tint = new Color(1f, 0.45f, 0.1f); amount = 0.55f; }
        else if (e.PoisonTimer > 0f) { tint = new Color(0.3f, 0.85f, 0.2f); amount = 0.45f; }
        foreach (var part in new[] { view.Body, view.Band! })
        {
            part.SetInstanceShaderParameter("tint", tint);
            part.SetInstanceShaderParameter("tint_amount", amount);
            part.SetInstanceShaderParameter("flash", e.HurtTimer > 0f ? 0.9f : 0f);
        }
    }

    static void Place(FishView view, Vector3 position, Vector3 facing, float size)
    {
        view.Root.Transform = new Transform3D(Conv.LookAlong(facing).Scaled(Vector3.One * size), position);
    }

    void SetSwim(MeshInstance3D body, int id, float speed)
    {
        body.SetInstanceShaderParameter("swim", Mathf.Clamp(speed / 3.5f, 0f, 1f));
        body.SetInstanceShaderParameter("phase", id * 1.7f);
    }

    FishView Get(int id, bool ninja)
    {
        if (_views.TryGetValue(id, out var existing)) return existing;

        var root = new Node3D();
        AddChild(root);
        var body = new MeshInstance3D { Mesh = _bodyMesh, MaterialOverride = _material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        root.AddChild(body);
        var view = new FishView { Root = root, Body = body, Size = ninja ? 1f : 0.9f + 0.18f * ((id * 37) % 11) / 10f };
        if (ninja)
        {
            view.Band = new MeshInstance3D { Mesh = _bandMesh, MaterialOverride = _material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            body.AddChild(view.Band);
            view.Band.SetInstanceShaderParameter("phase", id * 1.7f);
            view.Star = new MeshInstance3D { Mesh = _starMesh, MaterialOverride = _starMaterial, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            root.AddChild(view.Star);
        }
        _views[id] = view;
        return view;
    }
}
