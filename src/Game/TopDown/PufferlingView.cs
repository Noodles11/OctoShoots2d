using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The pufferlings on screen (docs/PUFFERLING-PROPOSAL.md), presentation only: every corrupted one (the plane's mobs) and
/// every healthy or freed one (the world's fish) as the baked body (PufferlingMesh) in pufferling.gdshader, swum from the
/// sim's position, heading and state — the stroke quickening with its speed, a bank into turns, a slow bob, the blow-up.
/// The corrupted wear a pet-shop price tag clipped to the tail fin. Freed, a fish shudders while the tank colours wash
/// out, bubbles burst round it and the tag comes off and sinks. (Its needles are shots, drawn by CombatView.)
/// </summary>
public partial class PufferlingView : Node3D
{
    sealed class Fish
    {
        public Node3D Root = null!;
        public MeshInstance3D Body = null!;
        public Node3D TagPivot = null!;
        public float Yaw, Roll, TailPhase, FinPhase, Swim, Inflate, Time;
        public int Id;
        public bool HasTag = true;
    }

    sealed class Falling
    {
        public Node3D Node = null!;
        public float Age, Floor;
    }

    readonly Dictionary<object, Fish> _fish = new();
    /// <summary>Freed poofs whose light is still drifting home to Clementine (for HomeSeconds), and where she is.</summary>
    readonly List<(CpuParticles3D Bubbles, float Age)> _poofs = new();
    Vector3 _her;
    ShaderMaterial? _poofMaterial;
    const float HomeSeconds = 0.5f, HomePull = 7f;
    readonly List<Falling> _falling = new();
    ArrayMesh _mesh = null!;
    ShaderMaterial _material = null!;
    Mesh _tagMesh = null!;
    StandardMaterial3D _tagMaterial = null!, _stringMaterial = null!;
    LevelMap? _map;
    float _time;
    int _ids;

    public override void _Ready()
    {
        _mesh = PufferlingMesh.Build();
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/pufferling.gdshader") };
        // The price tag: a small cream card with a hot-pink edge, bright enough to read at a glance.
        _tagMesh = new BoxMesh { Size = new Vector3(0.15f, 0.1f, 0.012f) };
        _tagMaterial = new StandardMaterial3D
        {
            AlbedoTexture = TagTexture(),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.95f, 0.85f),
            EmissionEnergyMultiplier = 0.35f,
            Roughness = 0.5f,
        };
        _stringMaterial = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.95f, 0.92f, 0.85f) };
    }

    /// <summary>The needles' look (CombatView draws them).</summary>
    public static ShaderMaterial NeedleMaterial() => new() { Shader = GD.Load<Shader>("res://assets/shaders/needle.gdshader") };

    /// <summary>A needle: a long spike along +Y, pointed at the top — big enough to read and dodge at play distance.</summary>
    public static Mesh NeedleMesh() => new CylinderMesh { TopRadius = 0f, BottomRadius = 0.15f, Height = 1.2f, RadialSegments = 8, Rings = 1 };

    /// <summary>A card with a punched hole and a hot-pink price band.</summary>
    static ImageTexture TagTexture()
    {
        const int w = 30, h = 20;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            Color c = new(0.98f, 0.95f, 0.86f);
            if (x < 2 || y < 2 || x >= w - 2 || y >= h - 2) c = new Color(1f, 0.42f, 0.8f);
            if (y >= 8 && y <= 12 && x >= 10 && x <= 26) c = new Color(1f, 0.3f, 0.36f);
            if (new Vector2(x - 5.5f, y - 9.5f).Length() < 2f) c = new Color(0.3f, 0.2f, 0.2f);
            img.SetPixel(x, y, c);
        }
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>A new level: every fish view goes.</summary>
    public void Show(PlaneWorld world)
    {
        foreach (var f in _fish.Values) f.Root.QueueFree();
        _fish.Clear();
        _map = world.Map;
    }

    Fish Make(bool corrupted)
    {
        var root = new Node3D();
        AddChild(root);
        var body = new MeshInstance3D { Mesh = _mesh, MaterialOverride = _material };
        root.AddChild(body);
        var pivot = new Node3D { Position = new Vector3(-0.46f, 0f, 0f) };
        body.AddChild(pivot);
        var fish = new Fish { Root = root, Body = body, TagPivot = pivot, Id = _ids++, Time = GD.Randf() * 10f, HasTag = corrupted };
        if (corrupted)
        {
            // Clipped to the tail fin by a short string; it dangles and flutters as she swims.
            pivot.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.005f, BottomRadius = 0.005f, Height = 0.08f, RadialSegments = 4 }, MaterialOverride = _stringMaterial, Position = new Vector3(-0.06f, -0.06f, 0f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            pivot.AddChild(new MeshInstance3D { Mesh = _tagMesh, MaterialOverride = _tagMaterial, Position = new Vector3(-0.1f, -0.14f, 0f), Name = "Tag" });
        }
        body.SetInstanceShaderParameter("pattern", (fish.Id * 0.137f) % 1f);
        return fish;
    }

    public void Sync(PlaneWorld world, float dt)
    {
        _time += dt;
        _her = new Vector3(world.Player.Position.X, LevelMap.SwimBand, world.Player.Position.Y);
        StepPoofs(dt);
        var seen = new HashSet<object>();
        foreach (var mob in world.Mobs)
        {
            if (!mob.Alive) continue;
            seen.Add(mob);
            if (!_fish.TryGetValue(mob, out var f)) _fish[mob] = f = Make(corrupted: true);
            Pose(f, mob, dt, corrupt: 1f, flash: mob.HitFlash > 0f ? 1f : 0f, spines: mob.Spines, shudder: 0f);
        }
        foreach (var fish in world.Fish)
        {
            seen.Add(fish);
            if (!_fish.TryGetValue(fish, out var f))
            {
                // A freed one takes over the view of the pufferling it was, so it carries straight on.
                var was = fish.Freed ? _fish.FirstOrDefault(kv => kv.Key is PlaneMob m && !m.Alive && kv.Value.Root.Position.DistanceTo(Flat(fish.Position)) < 2f) : default;
                if (was.Value is { } old)
                {
                    _fish.Remove(was.Key);
                    f = old;
                    Freed(f);
                }
                else f = Make(corrupted: false);
                _fish[fish] = f;
            }
            float wash = fish.Freed ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp((fish.FreedTime - 0.25f) / 0.8f, 0f, 1f)) : 0f;
            float shudder = fish.Freed ? 1f - Mathf.Clamp(fish.FreedTime / PufferlingTuning.FreedShudder, 0f, 1f) : 0f;
            Pose(f, fish, dt, corrupt: wash, flash: 0f, spines: 1f, shudder: shudder);
        }
        foreach (var key in _fish.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            _fish[key].Root.QueueFree();
            _fish.Remove(key);
        }
        StepFalling(dt);
    }

    static Vector3 Flat(System.Numerics.Vector2 p) => new(p.X, LevelMap.SwimBand, p.Y);

    /// <summary>One fish from the sim's state: where, which way, how fast it strokes, how blown up.</summary>
    void Pose(Fish f, PlaneSwimmer s, float dt, float corrupt, float flash, float spines, float shudder)
    {
        f.Time += dt;
        float speed = s.Velocity.Length();
        f.Swim = Mathf.Lerp(f.Swim, Mathf.Clamp(speed / PufferlingTuning.WanderSpeed, 0f, 1.5f), 1f - Mathf.Exp(-4f * dt));
        f.Inflate = Mathf.Lerp(f.Inflate, s.Inflate, 1f - Mathf.Exp(-20f * dt));
        // The stroke quickens with speed; the pectorals never stop.
        f.TailPhase += dt * Mathf.Tau * (0.45f + 1.15f * f.Swim) * (1f - 0.7f * f.Inflate);
        f.FinPhase += dt * Mathf.Tau * (2.4f + 0.8f * f.Swim);
        // Facing: the sim already turns smoothly; the view follows closely and banks into the turn.
        f.Yaw = Mathf.LerpAngle(f.Yaw, -s.Heading, 1f - Mathf.Exp(-14f * dt));
        f.Roll = Mathf.Lerp(f.Roll, Mathf.Clamp(-s.TurnRate * 0.16f, -0.35f, 0.35f) * (1f - f.Inflate), 1f - Mathf.Exp(-6f * dt));
        float bob = 0.1f * Mathf.Sin(f.Time * 0.9f + f.Id * 1.7f) * (1f - 0.6f * f.Inflate);
        var pos = Flat(s.Position) + new Vector3(0f, bob - 0.05f, 0f);
        if (shudder > 0f) pos += new Vector3(Mathf.Sin(f.Time * 61f), Mathf.Sin(f.Time * 53f + 1f) * 0.5f, Mathf.Sin(f.Time * 47f + 2f)) * 0.045f * shudder;
        f.Root.Position = pos;
        f.Root.Basis = new Basis(Vector3.Up, f.Yaw) * new Basis(Vector3.Right, f.Roll) * new Basis(Vector3.Back, -0.06f * f.Swim * (1f - f.Inflate));
        float turn = Mathf.Clamp(s.TurnRate / 2.1f, -1f, 1f);
        var body = f.Body;
        body.SetInstanceShaderParameter("inflate", f.Inflate);
        body.SetInstanceShaderParameter("spines", spines);
        body.SetInstanceShaderParameter("swim", f.Swim);
        body.SetInstanceShaderParameter("tail_phase", f.TailPhase);
        body.SetInstanceShaderParameter("fin_phase", f.FinPhase);
        body.SetInstanceShaderParameter("turn", turn);
        body.SetInstanceShaderParameter("corrupt", corrupt);
        body.SetInstanceShaderParameter("flash", flash);
        // The tag rides the tail fin as the shader sweeps it (pufferling.gdshader's tail and wiggle), dangling on its string.
        if (f.HasTag)
        {
            float k = f.Inflate;
            float sweep = (Mathf.Sin(f.TailPhase - 1.2f) * 0.32f * (0.35f + 0.65f * Mathf.Min(f.Swim, 1.2f)) + turn * 0.5f) * 0.45f;
            float wiggle = Mathf.Sin(f.TailPhase + 0.46f * 4f) * 0.035f * (0.35f + 0.65f * Mathf.Min(f.Swim, 1.2f)) * (1f - k);
            f.TagPivot.Position = new Vector3(Mathf.Lerp(-0.47f, -1.02f, k), -0.02f, wiggle);
            f.TagPivot.Rotation = new Vector3(0.25f * Mathf.Sin(f.Time * 5.1f + f.Id), sweep, 0.3f * Mathf.Sin(f.Time * 3.7f + f.Id * 2f) - 0.2f * f.Swim);
        }
    }

    /// <summary>Freed: a burst of bright bubbles, and the tag comes off and sinks, spinning.</summary>
    void Freed(Fish f)
    {
        var at = f.Root.Position;
        var bubbles = new CpuParticles3D
        {
            Position = at,
            Emitting = true,
            OneShot = true,
            Explosiveness = 0.9f,
            Amount = 28,
            Lifetime = 1.4f,
            // Light bubbles: the light that freed it, little lanterns that drift home to her before they rise away.
            Mesh = new SphereMesh { Radius = 0.07f, Height = 0.14f, RadialSegments = 8, Rings = 4, Material = PoofMaterial() },
            Direction = Vector3.Up,
            Spread = 180f,
            InitialVelocityMin = 1.2f,
            InitialVelocityMax = 3f,
            Gravity = HomeGravity(at),
            DampingMin = 1.5f,
            DampingMax = 2f,
            ScaleAmountMin = 0.6f,
            ScaleAmountMax = 1.8f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(bubbles);
        _poofs.Add((bubbles, 0f));
        GetTree().CreateTimer(2.5f).Timeout += bubbles.QueueFree;
        if (!f.HasTag) return;
        f.HasTag = false;
        var tag = f.TagPivot.GetNodeOrNull<Node3D>("Tag");
        if (tag is null) return;
        var xf = tag.GlobalTransform;
        foreach (var child in f.TagPivot.GetChildren()) ((Node)child).QueueFree();
        var falling = new MeshInstance3D { Mesh = _tagMesh, MaterialOverride = _tagMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(falling);
        falling.GlobalTransform = xf;
        float floor = _map is null ? -1f : _map.HeightAt(new System.Numerics.Vector2(xf.Origin.X, xf.Origin.Z));
        _falling.Add(new Falling { Node = falling, Floor = floor + 0.05f });
    }

    ShaderMaterial PoofMaterial()
    {
        if (_poofMaterial is null)
        {
            _poofMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/plane_bubble.gdshader") };
            _poofMaterial.SetShaderParameter("base_light", 0.6f);
        }
        return _poofMaterial;
    }

    /// <summary>The pull home: toward her on the swim layer, with a little lift so the light still reads as bubbles.</summary>
    Vector3 HomeGravity(Vector3 from)
    {
        var to = _her - from;
        to.Y = 0f;
        return (to.LengthSquared() > 1e-4f ? to.Normalized() * HomePull : Vector3.Zero) + Vector3.Up * 0.4f;
    }

    /// <summary>Freed poofs: for HomeSeconds the light drifts toward her (re-aimed as she moves), then rises away.</summary>
    void StepPoofs(float dt)
    {
        for (int i = _poofs.Count - 1; i >= 0; i--)
        {
            var (bubbles, age) = _poofs[i];
            age += dt;
            if (!IsInstanceValid(bubbles))
            {
                _poofs.RemoveAt(i);
                continue;
            }
            if (age >= HomeSeconds)
            {
                bubbles.Gravity = new Vector3(0f, 1.2f, 0f);
                _poofs.RemoveAt(i);
                continue;
            }
            bubbles.Gravity = HomeGravity(bubbles.Position);
            _poofs[i] = (bubbles, age);
        }
    }

    void StepFalling(float dt)
    {
        for (int i = _falling.Count - 1; i >= 0; i--)
        {
            var t = _falling[i];
            t.Age += dt;
            var p = t.Node.Position;
            if (p.Y > t.Floor)
            {
                // Sinking like a leaf: a slow fall, swaying and spinning.
                p += new Vector3(Mathf.Sin(t.Age * 3f) * 0.4f, -0.7f, Mathf.Cos(t.Age * 2.3f) * 0.3f) * dt;
                t.Node.Position = p;
                t.Node.Rotation += new Vector3(1.3f, 2.1f, 0.7f) * dt;
            }
            if (t.Age > 12f)
            {
                t.Node.QueueFree();
                _falling.RemoveAt(i);
            }
        }
    }
}
