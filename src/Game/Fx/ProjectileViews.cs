using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Sim;
using OctoShoots.Game.Player;
using OctoShoots.Game.Util;

namespace OctoShoots.Game.Fx;

/// <summary>
/// Draws every projectile, sized from its real radius. Bubbles and creature orbs are point lights,
/// at most 32 at once (§9). Pearls are pearly, spines and pellets small and quick.
/// </summary>
public partial class ProjectileViews : Node3D
{
    const int MaxLights = 32;
    const float BubbleMeshRadius = 0.16f;
    const float DefaultShotRadius = 0.25f;

    sealed class View
    {
        public required Node3D Root;
        public required MeshInstance3D Body;
        public MeshInstance3D? Trail;
        public ProjectileKind Kind;
    }

    readonly Dictionary<int, View> _active = new();
    readonly Dictionary<ProjectileKind, Stack<View>> _pools = new();
    readonly HashSet<int> _seen = new();
    readonly OmniLight3D[] _lights = new OmniLight3D[MaxLights];

    ShaderMaterial _bubbleMaterial = null!;
    StandardMaterial3D _pearlMaterial = null!;
    StandardMaterial3D _shardMaterial = null!;
    StandardMaterial3D _orbMaterial = null!;
    StandardMaterial3D _orbTrailMaterial = null!;
    SphereMesh _bubbleMesh = null!;
    ArrayMesh _starMesh = null!;
    StandardMaterial3D _starMaterial = null!;
    StandardMaterial3D _sporeMaterial = null!;
    StandardMaterial3D _spineMaterial = null!;
    PrismMesh _spineMesh = null!;
    SphereMesh _orbMesh = null!;
    PrismMesh _shardMesh = null!;
    float _time;

    public override void _Ready()
    {
        _bubbleMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/bubble.gdshader") };
        _starMesh = ReefMeshes.Starfish();
        _starMaterial = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            EmissionEnabled = true,
            Emission = new Color(1f, 0.35f, 0.1f),
            EmissionEnergyMultiplier = 1.3f,
        };
        _bubbleMesh = new SphereMesh { Radius = BubbleMeshRadius, Height = BubbleMeshRadius * 2f, RadialSegments = 20, Rings = 10 };
        _pearlMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.93f, 0.9f),
            Metallic = 0.3f,
            Roughness = 0.15f,
            RimEnabled = true,
            Rim = 1f,
            EmissionEnabled = true,
            Emission = new Color(0.9f, 0.85f, 1f),
            EmissionEnergyMultiplier = 0.8f,
        };
        _shardMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.55f, 0.25f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.45f, 0.15f),
            EmissionEnergyMultiplier = 2f,
        };
        _shardMesh = new PrismMesh { Size = new Vector3(0.08f, 0.35f, 0.08f) };
        _orbMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(1f, 0.92f, 0.55f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.8f, 0.3f),
            EmissionEnergyMultiplier = 4f,
        };
        _orbTrailMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            AlbedoColor = new Color(1f, 0.75f, 0.25f, 0.45f),
        };
        _orbMesh = new SphereMesh { Radius = 0.2f, Height = 0.4f, RadialSegments = 12, Rings = 6 };
        // The Spanish Dancer's spores: soft pink-orange glowing globes. Spines: slim ivory darts with a violet glow.
        _sporeMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(1f, 0.55f, 0.5f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.35f, 0.3f),
            EmissionEnergyMultiplier = 3f,
        };
        _spineMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.9f, 0.8f),
            EmissionEnabled = true,
            Emission = new Color(0.75f, 0.4f, 1f),
            EmissionEnergyMultiplier = 1.6f,
        };
        _spineMesh = new PrismMesh { Size = new Vector3(0.07f, 0.55f, 0.07f) };

        for (int i = 0; i < MaxLights; i++)
        {
            _lights[i] = new OmniLight3D { OmniRange = 3f, LightEnergy = 0.8f, ShadowEnabled = false, Visible = false, LightCullMask = ~Viewmodel.RenderLayer };
            AddChild(_lights[i]);
        }
    }

    /// <summary>Neon Rave: the bubbles' tint cycles through colours.</summary>
    public void SetColorCycle(bool on, float dt)
    {
        _time += dt;
        Color tint = on ? Color.FromHsv(_time * 0.4f % 1f, 0.7f, 1f) : new Color(0.75f, 0.95f, 1f);
        _bubbleMaterial.SetShaderParameter("tint", tint);
        _bubbleMaterial.SetShaderParameter("strength", on ? 1.4f : 1f);
    }

    public void Sync(World world, float alpha, FxParticles ink, float dt)
    {
        _seen.Clear();
        int light = 0;
        foreach (var p in world.Projectiles)
        {
            _seen.Add(p.Id);
            if (!_active.TryGetValue(p.Id, out var view))
            {
                view = Take(p.Kind);
                _active[p.Id] = view;
            }

            Vector3 pos = Conv.Lerp(p.PrevPosition, p.Position, alpha);
            Vector3 vel = p.Velocity.G();
            float speed = vel.Length();
            view.Root.GlobalTransform = new Transform3D(Conv.LookAlong(vel), pos);
            float size = p.Radius / DefaultShotRadius;

            switch (p.Kind)
            {
                case ProjectileKind.Bubble:
                case ProjectileKind.Pellet:
                {
                    // Leaves the tentacle tip small, swelling to full size over the first metre; a slight squash in flight.
                    float grow = Mathf.Clamp(p.Traveled / 1.2f, 0.35f, 1f);
                    float stretch = 1f + Mathf.Min(speed * 0.015f, 0.25f);
                    view.Body.Scale = new Vector3(1f / Mathf.Sqrt(stretch), 1f / Mathf.Sqrt(stretch), stretch) * grow * size;
                    // A few tiny bubbles peel off and rise behind it.
                    if (p.Kind == ProjectileKind.Bubble && GD.Randf() < dt * 14f)
                        ink.Emit(pos, -vel * 0.03f + new Vector3(0f, 0.3f, 0f), 0.7f, 0.04f, 0.06f, new Color(0.8f, 0.95f, 1f, 0.45f), new Color(0.8f, 0.95f, 1f, 0f), 1.5f, 0.8f);
                    break;
                }
                case ProjectileKind.Pearl:
                    view.Body.Scale = Vector3.One * size * Mathf.Clamp(p.Traveled / 1.2f, 0.3f, 1f);
                    break;
                case ProjectileKind.Shard:
                    view.Body.Scale = Vector3.One;
                    break;
                case ProjectileKind.Spore:
                    view.Body.Scale = Vector3.One * (p.Radius / 0.2f) * (1f + 0.12f * Mathf.Sin(p.Age * 9f));
                    if (GD.Randf() < dt * 10f)
                        ink.Emit(pos, new Vector3(0f, 0.25f, 0f), 0.6f, 0.05f, 0.07f, new Color(1f, 0.6f, 0.55f, 0.5f), new Color(1f, 0.4f, 0.4f, 0f), 1.5f, 0.6f);
                    break;
                case ProjectileKind.Spine:
                    view.Body.Scale = Vector3.One * (p.Radius / 0.15f);
                    break;
                case ProjectileKind.Star:
                    // A tiny starfish spinning flat as it flies, shedding red-orange specks.
                    view.Body.Transform = new Transform3D(new Basis(Vector3.Up, p.Age * 16f).Scaled(Vector3.One * p.Radius * 1.6f), Vector3.Zero);
                    if (GD.Randf() < dt * 18f)
                        ink.Emit(pos, -vel * 0.03f, 0.35f, 0.05f, 0.01f, new Color(1f, 0.5f, 0.2f, 0.7f), new Color(1f, 0.3f, 0.1f, 0f), 2f);
                    break;
                case ProjectileKind.Orb when view.Trail is not null:
                    view.Trail.Scale = new Vector3(0.7f, 0.7f, 1f + speed * 0.25f);
                    view.Trail.Position = new Vector3(0f, 0f, 0.2f + speed * 0.05f);
                    break;
            }

            if (light < MaxLights && p.Kind is not (ProjectileKind.Shard or ProjectileKind.Star or ProjectileKind.Spine))
            {
                var l = _lights[light++];
                l.Visible = true;
                l.GlobalPosition = pos;
                l.LightColor = p.FromPlayer ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 0.8f, 0.35f);
                l.LightEnergy = p.FromPlayer ? 0.4f : 1.4f;
            }
        }
        for (int i = light; i < MaxLights; i++) _lights[i].Visible = false;

        var gone = new List<int>();
        foreach (var id in _active.Keys)
            if (!_seen.Contains(id)) gone.Add(id);
        foreach (var id in gone) Release(id);
    }

    public void Clear()
    {
        foreach (var id in new List<int>(_active.Keys)) Release(id);
    }

    void Release(int id)
    {
        var view = _active[id];
        _active.Remove(id);
        view.Root.Visible = false;
        Pool(view.Kind).Push(view);
    }

    Stack<View> Pool(ProjectileKind kind)
    {
        if (!_pools.TryGetValue(kind, out var pool)) _pools[kind] = pool = new Stack<View>();
        return pool;
    }

    View Take(ProjectileKind kind)
    {
        var pool = Pool(kind);
        if (pool.Count > 0)
        {
            var reused = pool.Pop();
            reused.Root.Visible = true;
            return reused;
        }

        var root = new Node3D();
        AddChild(root);
        MeshInstance3D Mesh(Mesh mesh, Material material) =>
            new() { Mesh = mesh, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };

        View view;
        switch (kind)
        {
            case ProjectileKind.Orb:
            {
                var body = Mesh(_orbMesh, _orbMaterial);
                var trail = Mesh(_orbMesh, _orbTrailMaterial);
                root.AddChild(body);
                root.AddChild(trail);
                view = new View { Root = root, Body = body, Trail = trail, Kind = kind };
                break;
            }
            case ProjectileKind.Pearl:
            {
                var body = Mesh(new SphereMesh { Radius = DefaultShotRadius * 0.7f, Height = DefaultShotRadius * 1.4f }, _pearlMaterial);
                root.AddChild(body);
                view = new View { Root = root, Body = body, Kind = kind };
                break;
            }
            case ProjectileKind.Star:
            {
                var body = Mesh(_starMesh, _starMaterial);
                root.AddChild(body);
                view = new View { Root = root, Body = body, Kind = kind };
                break;
            }
            case ProjectileKind.Spore:
            {
                var body = Mesh(_orbMesh, _sporeMaterial);
                root.AddChild(body);
                view = new View { Root = root, Body = body, Kind = kind };
                break;
            }
            case ProjectileKind.Spine:
            {
                var body = Mesh(_spineMesh, _spineMaterial);
                body.RotationDegrees = new Vector3(-90f, 0f, 0f);
                root.AddChild(body);
                view = new View { Root = root, Body = body, Kind = kind };
                break;
            }
            case ProjectileKind.Shard:
            {
                var body = Mesh(_shardMesh, _shardMaterial);
                body.RotationDegrees = new Vector3(-90f, 0f, 0f); // prism points along -Z (flight)
                root.AddChild(body);
                view = new View { Root = root, Body = body, Kind = kind };
                break;
            }
            default:
            {
                var body = Mesh(_bubbleMesh, _bubbleMaterial);
                root.AddChild(body);
                view = new View { Root = root, Body = body, Kind = kind };
                break;
            }
        }
        return view;
    }
}
