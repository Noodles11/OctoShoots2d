using System.Linq;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Combat on screen: everyone's shots (her bubbles, Queen Clam's pearls, the pufferlings' needles), pearls and the shop's
/// stands. (The pufferlings themselves are PufferlingView; the way on is the level's shaft, drawn by the level.)
/// </summary>
public partial class CombatView : Node3D
{
    readonly List<MeshInstance3D> _shots = new();
    readonly List<Node3D> _pearls = new();
    /// <summary>The shells lying in the reef: each a silver nautilus (NautilusShell), lying as it fell — turned at random
    /// in every axis — turning slowly about its own tilt, bobbing a little on the swim band.</summary>
    readonly Dictionary<PlaneShell, (MeshInstance3D Mesh, Basis Lie, Vector3 Spin, float Phase)> _shells = new();
    /// <summary>A shell's size on the plane (across, metres).</summary>
    public const float ShellSize = 0.62f;
    /// <summary>Hearts dropped by freed mobs: a small red heart and its glow each, pooled.</summary>
    readonly List<Node3D> _hearts = new();
    readonly List<Node3D> _stands = new();
    /// <summary>The stands' price tags: shown only while Clementine is inside the shop.</summary>
    readonly List<Node3D> _prices = new();
    StandardMaterial3D? _heart;
    static ImageTexture? _tagTexture;
    Poi? _shop;
    StandardMaterial3D _herShot = null!, _theirShot = null!;
    Mesh _needleMesh = null!;
    ShaderMaterial _needle = null!;
    /// <summary>Queen Clam's pearls are corruption (art rules in CLAUDE.md): glossy ink orbs, the royal one bigger with a violet ember.</summary>
    ShaderMaterial _clamPearl = null!, _royalPearl = null!, _inkDrop = null!;
    float _time;

    // Light bubbles (docs/LIGHT-BUBBLES.md): every bubble of hers carries a lantern, glows in the water and lights the
    // swim layer round it; where one pops it leaves a bloom of light; merges pulse and chime; kills flare by merge count.
    /// <summary>A bubble's core brightness: dim and warm alone, ×1.35 per bubble merged in, capped below her bell's.</summary>
    public static float CoreLight(int bubbles) => Mathf.Min(BaseLight * Mathf.Pow(MergeGain, bubbles - 1), MaxLight);
    /// <summary>From her gold (one bubble) to white-gold (a full one).</summary>
    public static float Warmth(int bubbles) => Mathf.Clamp((bubbles - 1) / (float)(PlaneCombatTuning.BubbleCap - 1), 0f, 1f);
    const float BaseLight = 0.32f, MergeGain = 1.35f, MaxLight = 1f;
    /// <summary>The glow round a bubble: a fixed 0.8 m disc whose strength grows with merging, ≤ 0.4 of her halo.</summary>
    const float HaloRadius = 0.8f, HaloMin = 0.16f, HaloMax = 0.4f;
    /// <summary>Their light on the swim layer: radius in metres (never past 3, merged or not), strength as a share of hers.</summary>
    const float GroundRadius = 1.5f, GroundStrength = 0.35f;
    /// <summary>Halos past this distance from her are off screen and not sent.</summary>
    const float LightReach = 25f;
    public const int MaxLights = 16;
    /// <summary>The pop: the lantern flares (×1.5) for 150 ms; the bloom it leaves fades over a second.</summary>
    const float FlareSeconds = 0.15f, FlareGain = 1.5f, BloomSeconds = 1f, BloomRadius = 1.5f;
    /// <summary>A merge pulses for 200 ms; a kill by a bubble of at least BigKill flashes the screen's edges.</summary>
    const float MergePulseSeconds = 0.2f;
    public const int BigKill = 6;

    /// <summary>A bubble merged here, into one of this many: the view pulses; the listener chimes (pitch climbing).</summary>
    public event System.Action<Vector3, int>? Merged;

    /// <summary>The swim-layer lights this frame, brightest first, for the post pass.</summary>
    public Vector4[] Lights { get; } = new Vector4[MaxLights];
    public int LightCount { get; private set; }
    /// <summary>Her bubbles in flight as lenses (x, height, z, radius): the light through them falls on the floor in rings.</summary>
    public const int MaxLenses = 12;
    public Vector4[] Lenses { get; } = new Vector4[MaxLenses];
    public int LensCount { get; private set; }

    sealed class Bloom
    {
        public System.Numerics.Vector2 At;
        public float Age, Life, Strength, Radius;
    }

    readonly List<Bloom> _blooms = new();
    readonly List<MeshInstance3D> _halos = new();
    ShaderMaterial _halo = null!;
    readonly QuadMesh _haloQuad = new() { Size = new Vector2(2f, 2f) };
    /// <summary>Her bubbles as last drawn: where each was and how many it held (pops and kills look the bubble up).</summary>
    Dictionary<PlaneShot, (System.Numerics.Vector2 At, int Bubbles)> _seen = new(), _seenNext = new();
    readonly List<(System.Numerics.Vector2 At, float Strength, float Radius)> _candidates = new();

    public override void _Ready()
    {
        _halo = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/bubble_halo.gdshader"), RenderPriority = -1 };
        _needleMesh = PufferlingView.NeedleMesh();
        _needle = PufferlingView.NeedleMaterial();
        _herShot = Glow(new Color(1f, 0.85f, 0.65f));
        _bubble = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/plane_bubble.gdshader") };
        _sparkMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/light_shot.gdshader"), RenderPriority = 2 };
        _trails = new LightTrails();
        AddChild(_trails);
        _fireworks = new FxParticles(1024, additive: true);
        AddChild(_fireworks);
        _dust = new MagicParticles(512);
        AddChild(_dust);
        _afterimages = new FxParticles(1536, additive: true);
        AddChild(_afterimages);
        _wake = new FxParticles(400, additive: false, FxParticles.Bubble());
        AddChild(_wake);
        _theirShot = Glow(new Color(1f, 0.25f, 0.3f));
        var orb = GD.Load<Shader>("res://assets/shaders/corruption_orb.gdshader");
        _clamPearl = new ShaderMaterial { Shader = orb };
        _clamPearl.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("ember_color"), new Color(0.4f, 0.2f, 0.6f));
        _royalPearl = new ShaderMaterial { Shader = orb };
        _royalPearl.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("ember_color"), new Color(0.7f, 0.35f, 1f));
        // Drippus Prime's drops: pieces of its ink, glowing with its magenta ember.
        _inkDrop = new ShaderMaterial { Shader = orb };
        _inkDrop.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("ember_color"), BlightView.Kin.PrimeCore);

    }

    static StandardMaterial3D Glow(Color c) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        AlbedoColor = c,
        EmissionEnabled = true,
        Emission = c,
        EmissionEnergyMultiplier = 3f,
    };

    /// <summary>
    /// A light orb (a pickup that washes some of her corruption away): a bubble with a lantern of her light inside, like
    /// her old bubble shots, a soft halo round it and a little warm light on the world. size scales it (1 on the floor,
    /// larger on a shop stand).
    /// </summary>
    Node3D LightOrb(float size)
    {
        var node = new Node3D();
        var shell = new MeshInstance3D { Mesh = _unitSphere, MaterialOverride = _bubble, Scale = Vector3.One * 0.34f * size, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        shell.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("lantern"), 0.85f);
        shell.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("warmth"), 0.35f);
        shell.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("rainbow"), 0.4f);
        shell.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("wobble"), 0.35f);
        shell.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), (float)GD.RandRange(0.0, 10.0));
        shell.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("hole"), new Vector4(0f, 1f, 0f, 0f));
        node.AddChild(shell);
        var halo = new MeshInstance3D { Mesh = _haloQuad, MaterialOverride = _halo, Scale = Vector3.One * 0.8f * size, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        halo.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("strength"), 0.35f);
        var her = HerColor.Light;
        halo.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("color"), new Vector3(her.R, her.G, her.B));
        node.AddChild(halo);
        node.AddChild(new OmniLight3D { LightColor = her, LightEnergy = 0.9f * size, OmniRange = 2.8f * size, ShadowEnabled = false });
        return node;
    }

    /// <summary>A heart, flat, always facing the camera (billboard material).</summary>
    static ArrayMesh HeartMesh()
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        const int steps = 48;
        const float k = 0.026f;
        var centre = new Vector3(0f, 0.05f, 0f);
        Vector3 At(float t) => new(k * 16f * Mathf.Pow(Mathf.Sin(t), 3), k * (13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t)), 0f);
        for (int i = 0; i < steps; i++)
        {
            float t0 = Mathf.Tau * i / steps, t1 = Mathf.Tau * (i + 1) / steps;
            st.SetNormal(Vector3.Back);
            st.AddVertex(centre);
            st.AddVertex(At(t1));
            st.AddVertex(At(t0));
        }
        return st.Commit();
    }

    static StandardMaterial3D HeartMaterial() => new()
    {
        AlbedoColor = new Color(0.95f, 0.15f, 0.25f),
        EmissionEnabled = true,
        Emission = new Color(1f, 0.25f, 0.3f),
        EmissionEnergyMultiplier = 1.3f,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
    };

    /// <summary>
    /// A small price tag hanging on a string below and to the side of the item: a cream card with a hole, the number,
    /// and a shell drawn on it.
    /// </summary>
    static Node3D PriceTag(int price)
    {
        var tag = new Node3D { Visible = false };
        var at = new Vector3(0.95f, -0.55f, 0.65f);
        // The string: from just under the item to the tag's hole.
        var hole = at + new Vector3(-0.42f, 0.03f, 0f);
        var from = new Vector3(0.2f, -0.3f, 0.15f);
        var line = new ImmediateMesh();
        line.SurfaceBegin(Mesh.PrimitiveType.Lines);
        line.SurfaceAddVertex(from);
        line.SurfaceAddVertex(hole);
        line.SurfaceEnd();
        tag.AddChild(new MeshInstance3D
        {
            Mesh = line,
            MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.9f, 0.85f, 0.75f), NoDepthTest = true },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        tag.AddChild(new Sprite3D
        {
            Texture = _tagTexture ??= TagTexture(),
            PixelSize = 0.0125f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Shaded = false,
            Position = at,
        });
        tag.AddChild(new Label3D
        {
            Text = price.ToString(),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            PixelSize = 0.0085f,
            FontSize = 40,
            OutlineSize = 0,
            Modulate = new Color(0.25f, 0.13f, 0.1f),
            NoDepthTest = true,
            RenderPriority = 2,
            Position = at + new Vector3(-0.05f, 0.01f, 0f),
        });
        return tag;
    }

    /// <summary>The tag card: rounded, cream, with a punched hole on the left and a scallop shell on the right.</summary>
    static ImageTexture TagTexture()
    {
        const int w = 96, h = 44;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        img.Fill(new Color(0f, 0f, 0f, 0f));
        var card = new Color(0.98f, 0.93f, 0.82f);
        var edge = new Color(0.45f, 0.3f, 0.22f);
        var shell = new Color(1f, 0.72f, 0.6f);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            // Rounded rectangle with a 9 px radius, and a 1.5 px rim.
            float dx = Mathf.Max(Mathf.Max(9f - x, x - (w - 10f)), 0f), dy = Mathf.Max(Mathf.Max(9f - y, y - (h - 10f)), 0f);
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d > 9f) continue;
            float hole = new Vector2(x - 12f, y - h / 2f).Length();
            if (hole < 4f) continue;
            Color c = d > 7.5f || hole < 5.5f ? edge : card;
            // The shell: a fan of ribs, hinge at the bottom.
            var rel = new Vector2(x - 75f, y - 32f);
            float r = rel.Length(), ang = Mathf.Atan2(rel.X, -rel.Y);
            if (r < 16f && Mathf.Abs(ang) < 1.15f && rel.Y < 1f)
            {
                bool rib = Mathf.Abs(Mathf.Sin(ang * 4.5f)) < 0.18f || r > 14f;
                c = rib ? edge : shell;
            }
            if (new Rect2(70f, 31f, 10f, 4f).HasPoint(new Vector2(x, y))) c = shell;
            img.SetPixel(x, y, c);
        }
        return ImageTexture.CreateFromImage(img);
    }

    /// <param name="fresh">Pearls offered for the first time since an achievement unlocked them: they wear a NEW chip.</param>
    public void Show(PlaneWorld world, ItemCatalog? catalog, ISet<string>? fresh = null)
    {
        foreach (var sh in _shells.Values) sh.Mesh.QueueFree();
        foreach (var st in _stands) st.QueueFree();
        foreach (var h in _hearts) h.QueueFree();
        _hearts.Clear();
        _shells.Clear();
        _stands.Clear();
        _prices.Clear();
        _shop = world.Map.Pois.FirstOrDefault(p => p.Kind == PoiKind.Shop);
        // The shop's goods float on their own (a clam room's pearl hangs still instead): pearls in their colours, the top-up a red
        // heart. Each hangs a little price tag on a string (its price and a shell), shown only while she is inside the shop.
        foreach (var stand in world.Stands)
        {
            var node = new Node3D { Position = new Vector3(stand.Position.X, LevelMap.SwimBand, stand.Position.Y) };
            if (stand.Kind == StandKind.Pearl)
            {
                Color a = new(1f, 0.95f, 0.85f), b = new(0.8f, 0.85f, 1f);
                if (catalog is not null && catalog.TryGet(stand.ItemId, out var item) && item.Pearl is { } look)
                {
                    a = new Color(look.Colors[0]);
                    b = new Color(look.Colors[^1]);
                }
                node.AddChild(new MeshInstance3D
                {
                    Mesh = new SphereMesh { Radius = 0.42f, Height = 0.84f, RadialSegments = 24, Rings = 12 },
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = a, EmissionEnabled = true, Emission = b, EmissionEnergyMultiplier = 1.4f, Roughness = 0.15f, Metallic = 0.3f, RimEnabled = true, Rim = 1f },
                });
                node.AddChild(new OmniLight3D { LightColor = b.Lerp(Colors.White, 0.3f), LightEnergy = 1.2f, OmniRange = 4f, ShadowEnabled = false });
            }
            else
            {
                node.AddChild(LightOrb(1.25f));
            }
            if (stand.Kind == StandKind.Pearl && fresh?.Contains(stand.ItemId) == true) node.AddChild(NewChip());
            var tag = PriceTag(stand.Price);
            node.AddChild(tag);
            _prices.Add(tag);
            AddChild(node);
            _stands.Add(node);
        }
        foreach (var s in _shots) s.QueueFree();
        foreach (var h in _halos) h.QueueFree();
        _trails.Clear();
        _fireworks.Clear();
        _dust.Clear();
        foreach (var q in _pearls) q.QueueFree();
        _shots.Clear();
        _halos.Clear();
        _merges.Clear();
        ClearLights();
        _pearls.Clear();
        _catalog = catalog;
        _pearlBorn.Clear();
        _pearlLooks.Clear();
        foreach (var sh in _shards) sh.Mesh.QueueFree();
        _shards.Clear();
        foreach (var pearl in world.Pearls)
        {
            AddPearl(pearl, -10f);
            if (fresh?.Contains(pearl.ItemId) == true) _pearls[^1].AddChild(NewChip());
        }
    }

    /// <summary>A small mint "NEW" chip floating above a pearl newly unlocked by an achievement.</summary>
    static Label3D NewChip() => new()
    {
        Text = "NEW",
        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        PixelSize = 0.012f,
        FontSize = 40,
        OutlineSize = 10,
        Modulate = new Color(0.44f, 0.89f, 0.76f),
        OutlineModulate = new Color(0.05f, 0.2f, 0.18f, 0.9f),
        NoDepthTest = true,
        Position = new Vector3(0f, 1f, 0f),
    };

    ItemCatalog? _catalog;
    /// <summary>When each pearl appeared (a boss's reward floats down from above).</summary>
    readonly List<float> _pearlBorn = new();
    /// <summary>Each world pearl's own material (its look, and how pale its ritual has left it) and its light.</summary>
    readonly List<(ShaderMaterial? Look, OmniLight3D Light)> _pearlLooks = new();

    /// <summary>Where her bell is now (the absorbing ritual circles it).</summary>
    public Vector3 HerAt;

    sealed class Shard
    {
        public MeshInstance3D Mesh = null!;
        public StandardMaterial3D Material = null!;
        public Vector3 Velocity, Spin;
        public float Age;
    }

    readonly List<Shard> _shards = new();
    const float ShardSeconds = 0.4f;

    /// <summary>
    /// The ritual's end: the pale, emptied pearl shatters into pieces that tumble outward and dissolve, with a few
    /// glints of what light was left in it.
    /// </summary>
    public void ShatterPearl(Vector3 at)
    {
        var prism = new PrismMesh { Size = new Vector3(0.16f, 0.2f, 0.1f) };
        for (int i = 0; i < 11; i++)
        {
            var dir = _fireworks.RandDir();
            var material = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.9f, 0.9f, 0.92f, 0.95f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                EmissionEnabled = true,
                Emission = new Color(1f, 0.95f, 0.88f),
                EmissionEnergyMultiplier = 0.5f,
                Roughness = 0.15f,
                Metallic = 0.3f,
                RimEnabled = true,
            };
            var mesh = new MeshInstance3D { Mesh = prism, MaterialOverride = material, Position = at + dir * 0.15f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            mesh.Scale = Vector3.One * _fireworks.RandRange(0.6f, 1.3f);
            AddChild(mesh);
            _shards.Add(new Shard { Mesh = mesh, Material = material, Velocity = dir * _fireworks.RandRange(1.8f, 3.6f), Spin = _fireworks.RandDir() * 12f });
        }
        var glint = new Color(1.3f, 1.25f, 1.15f);
        for (int i = 0; i < 18; i++)
            _fireworks.Emit(at, _fireworks.RandDir() * _fireworks.RandRange(1f, 3f), _fireworks.RandRange(0.25f, 0.5f), 0.06f, 0.01f, glint, glint with { A = 0f }, drag: 3f);
    }

    /// <summary>The ritual's climax round her: a strong pulse of light, like an explosion, and a bloom on the water.</summary>
    public void RitualBurst(Vector3 at)
    {
        var flat = new System.Numerics.Vector2(at.X, at.Z);
        AddBloom(flat, 2.2f, 1.1f, 3f);
        LightBurst(flat, 3.2f);
    }
    const float PearlFall = 1.4f, PearlFallHeight = 7f;
    /// <summary>A pearl lying in the world: its glow from within (pearl_common's glow) and the light it sheds round it.</summary>
    const float WorldPearlGlow = 0.55f, WorldPearlLight = 0.9f;

    /// <summary>How a shell lying near this point is turned right now (for its flight to the counter), if one is.</summary>
    public Basis? ShellLie(System.Numerics.Vector2 at)
    {
        Basis? best = null;
        float near = 1.5f;
        foreach (var (sh, view) in _shells)
        {
            float d = System.Numerics.Vector2.Distance(sh.Position, at);
            if (d >= near) continue;
            near = d;
            best = view.Mesh.GlobalBasis.Orthonormalized();
        }
        return best;
    }

    /// <summary>A pearl: glowing in its own colours, turning slowly.</summary>
    void AddPearl(PlanePearl pearl, float born)
    {
        var catalog = _catalog;
        {
            Color a = new(1f, 0.95f, 0.85f), b = new(0.8f, 0.85f, 1f);
            if (catalog is not null && catalog.TryGet(pearl.ItemId, out var item) && item.Pearl is { } look)
            {
                a = new Color(look.Colors[0]);
                b = new Color(look.Colors[^1]);
            }
            var node = new Node3D { Position = new Vector3(pearl.Position.X, LevelMap.SwimBand, pearl.Position.Y) };
            // Its own copy of the pearl's look (colours in its pattern), so its ritual can pale it alone.
            ShaderMaterial? look_ = catalog is not null && catalog.TryGet(pearl.ItemId, out var def) ? (ShaderMaterial)OctoShoots.Game.Fx.PearlMaterials.Get(def).Duplicate() : null;
            // Glowing from within, but not so much that it burns white: its colours in their pattern must read.
            look_?.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("glow"), WorldPearlGlow);
            node.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.42f, Height = 0.84f, RadialSegments = 24, Rings = 12 },
                MaterialOverride = look_ is not null ? look_ : new StandardMaterial3D { AlbedoColor = a, EmissionEnabled = true, Emission = b, EmissionEnergyMultiplier = 1.4f, Roughness = 0.15f, Metallic = 0.3f, RimEnabled = true, Rim = 1f },
            });
            var light = new OmniLight3D { LightColor = b.Lerp(Colors.White, 0.3f), LightEnergy = WorldPearlLight, OmniRange = 5f, ShadowEnabled = false };
            node.AddChild(light);
            AddChild(node);
            _pearls.Add(node);
            // A bought pearl begins its ritual at once: no fall.
            _pearlBorn.Add(pearl.Absorbing || pearl.Placed ? -10f : born);
            _pearlLooks.Add((look_, light));
        }
    }

    /// <summary>
    /// A bubble's pop, slowed from the real thing (a few milliseconds) so it reads at play speed: the film tears open at
    /// the struck point and the hole's rim sweeps across it in TearSeconds; as the rim passes, the film breaks off it in
    /// droplets that fling on outward, the chain running from the struck side to the far side, then fade in DropSeconds.
    /// </summary>
    const float TearSeconds = 0.09f, DropSeconds = 0.26f;
    const int Droplets = 22;

    sealed class PopFx
    {
        public Node3D Node = null!;
        public MeshInstance3D Shell = null!;
        public MeshInstance3D[] Drops = null!;
        /// <summary>Each droplet's place on the film (unit), when the rim reaches it, and the way it flies off.</summary>
        public Vector3[] DropAt = null!, DropVel = null!;
        public float[] DropBorn = null!;
        public Vector3 Impact;
        public float Radius, Age, Light;
    }

    /// <summary>The film's droplets: small, bright, fading on their own.</summary>
    static ShaderMaterial? _dropletMaterial;
    static ShaderMaterial DropletMaterial() => _dropletMaterial ??= new ShaderMaterial
    {
        Shader = new Shader
        {
            Code = @"shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, shadows_disabled;
instance uniform float fade = 1.0;
instance uniform float lantern = 0.0;
void fragment() {
	float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 1.5);
	ALBEDO = mix(mix(vec3(0.92, 0.98, 1.0), vec3(1.0, 0.86, 0.6), lantern), vec3(1.0, 0.97, 0.9), rim);
	ALPHA = clamp((0.55 + 0.45 * rim) * fade, 0.0, 1.0);
}",
        },
    };

    readonly List<PopFx> _pops = new();
    readonly SphereMesh _unitSphere = new() { Radius = 1f, Height = 2f, RadialSegments = 20, Rings = 10 };
    ShaderMaterial _bubble = null!;
    /// <summary>Her shots of light: a camera-facing mote (light_shot.gdshader), their afterlight, and their fireworks.</summary>
    readonly QuadMesh _spark = new() { Size = Vector2.One };
    ShaderMaterial _sparkMat = null!;
    LightTrails _trails = null!;
    FxParticles _fireworks = null!;
    /// <summary>The magic particles a light burst throws up (falling back like dust) and a pearl's absorbing bursts out.</summary>
    MagicParticles _dust = null!;
    /// <summary>The light her shots shed as they fly (afterimage bursts), and Pistol Shrimp's cavitation bubbles in their wake.</summary>
    FxParticles _afterimages = null!, _wake = null!;
    /// <summary>Per shot: when it next sheds a burst of light, and how many bounces it had left (a bounce glints).</summary>
    Dictionary<PlaneShot, (float Next, int Bounces)> _shed = new(), _shedNext = new();
    readonly RandomNumberGenerator _rng = new();

    /// <summary>
    /// The pearl's absorbing ends (it shatters into her): a burst of magic particles flung out from her all round, up a
    /// little, slowing in the water, twirling, and popping out.
    /// </summary>
    public void MagicBurst(Vector3 at)
    {
        for (int i = 0; i < 44; i++)
        {
            float a = i * Mathf.Tau / 44f + _dust.RandRange(-0.08f, 0.08f);
            var dir = new Vector3(Mathf.Cos(a), _dust.RandRange(-0.15f, 0.45f), Mathf.Sin(a)).Normalized();
            _dust.Emit(at + dir * 0.4f, dir * _dust.RandRange(3f, 6.5f), _dust.RandRange(0.7f, 1.15f), _dust.RandRange(0.12f, 0.22f), drag: 2.2f);
        }
    }

    /// <summary>
    /// Starburst Gland: a burst of her light, painterly rather than a bright disc. A small round smear of the picture on
    /// her plane where it went off (CameraRig.AddBlur, which TopDownMain lays), and a spray of magic particles — "+"
    /// and "*" in white and ecru — thrown up and out from it, twirling, then falling back like dust in the water.
    /// </summary>
    public void LightBurst(System.Numerics.Vector2 at, float radius)
    {
        var from = new Vector3(at.X, LevelMap.SwimBand, at.Y);
        int n = Mathf.Clamp((int)(16f + 8f * radius), 16, 40);
        for (int i = 0; i < n; i++)
        {
            float a = _dust.RandRange(0f, Mathf.Tau);
            var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var v = outward * _dust.RandRange(1.5f, 3.5f) * (0.6f + 0.25f * radius) + Vector3.Up * _dust.RandRange(2.5f, 5f);
            _dust.Emit(from + outward * 0.2f, v, _dust.RandRange(0.8f, 1.3f), _dust.RandRange(0.08f, 0.16f), gravity: 7f, drag: 1.4f);
        }
    }

    const float SongSeconds = 1.6f;
    readonly List<(Node3D Node, StandardMaterial3D Material, float Age)> _songs = new();

    /// <summary>A shockwave: a flat ring racing out to its reach and thinning away, and a hard flash at its heart.</summary>
    sealed class Shock
    {
        public Node3D Node = null!;
        public StandardMaterial3D Ring = null!, Flash = null!;
        public MeshInstance3D Core = null!;
        public float Age, Reach, Life;
        public Color Color;
    }

    readonly List<Shock> _shocks = new();

    /// <summary>
    /// Pistol Shrimp Claw: the bubble collapses with a bang. A white-blue shock ring races out to the blast's reach and
    /// thins away, with a hard flash at its heart and a cold bloom on the water. (Pearl Necklace uses the same ring in
    /// gold, smaller.)
    /// </summary>
    public void Shockwave(System.Numerics.Vector2 at, float reach, Color color, float life = 0.38f)
    {
        AddBloom(at, 1.4f, 0.5f, Mathf.Min(reach, 4f));
        var node = new Node3D { Position = new Vector3(at.X, LevelMap.SwimBand, at.Y) };
        AddChild(node);
        var ring = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            AlbedoColor = color,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        node.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.86f, OuterRadius = 1f, Rings = 56, RingSegments = 6 }, MaterialOverride = ring, Scale = new Vector3(1f, 0.25f, 1f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        var flash = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            AlbedoColor = Colors.White,
        };
        var core = new MeshInstance3D { Mesh = _unitSphere, MaterialOverride = flash, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        node.AddChild(core);
        _shocks.Add(new Shock { Node = node, Ring = ring, Flash = flash, Core = core, Reach = reach, Life = life, Color = color });
    }

    /// <summary>An active pearl was used: Whale Song sends three soft rings of sound out from her (the shield is drawn on her).</summary>
    public void ActiveUsed(PlaneWorld world)
    {
        if (world.Run.Active?.Active?.Action != ActiveAction.WhaleSong) return;
        var at = world.Player.Position;
        for (int i = 0; i < 3; i++)
        {
            var material = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor = new Color(0.55f, 0.95f, 1f, 0f),
                EmissionEnabled = true,
                Emission = new Color(0.4f, 0.85f, 1f),
                EmissionEnergyMultiplier = 1.5f,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            };
            var node = new Node3D { Position = new Vector3(at.X, LevelMap.SwimBand, at.Y), Visible = false };
            node.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.94f, OuterRadius = 1f, Rings = 48, RingSegments = 6 }, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            AddChild(node);
            // Staggered: each ring starts a little after the last.
            _songs.Add((node, material, -0.28f * i));
        }
    }

    readonly Dictionary<PlaneShot, float> _merges = new();

    /// <summary>
    /// The swim-layer lights for the post pass: every bubble of hers and every bloom still glowing, within reach of her,
    /// the brightest MaxLights of them (the rest glow on their own only). Strength never passes 0.4 of her halo.
    /// </summary>
    void SyncLights(PlaneWorld world, float dt)
    {
        for (int i = _blooms.Count - 1; i >= 0; i--)
        {
            var b = _blooms[i];
            b.Age += dt;
            if (b.Age >= b.Life)
            {
                _blooms.RemoveAt(i);
                continue;
            }
            float k = 1f - b.Age / b.Life;
            _candidates.Add((b.At, GroundStrength * b.Strength * k * k, b.Radius));
        }
        var her = world.Player.Position;
        int count = 0;
        foreach (var (at, strength, radius) in _candidates.Where(c => c.Strength > 0.005f && System.Numerics.Vector2.Distance(c.At, her) <= LightReach).OrderByDescending(c => c.Strength))
        {
            if (count == MaxLights) break;
            Lights[count++] = new Vector4(at.X, at.Y, radius, Mathf.Min(strength, HaloMax));
        }
        LightCount = count;
    }

    /// <summary>
    /// A bubble popped here (on a mob, on rock, on another bubble, or where it hovered). toward: from its centre to where
    /// the film gave way, on the plane; the tear starts there, tipped a little up toward the camera.
    /// </summary>
    public void Pop(System.Numerics.Vector2 at, float radius, System.Numerics.Vector2 toward)
    {
        // Her light, spent: the lantern flares and goes out, and leaves a bloom of light on the water for a second.
        int bubbles = BubblesNear(at, radius + 1.5f);
        if (bubbles > 0)
        {
            AddBloom(at, CoreLight(bubbles), BloomSeconds, BloomRadius);
            Firework(at, bubbles, toward);
            return;
        }
        var node = new Node3D { Position = new Vector3(at.X, LevelMap.SwimBand, at.Y) };
        AddChild(node);
        var impact = new Vector3(toward.X, 0.35f, toward.Y);
        impact = impact.LengthSquared() > 1e-6f ? impact.Normalized() : Vector3.Up;
        var shell = new MeshInstance3D { Mesh = _unitSphere, MaterialOverride = _bubble, Scale = Vector3.One * radius, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        shell.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("hole"), new Vector4(impact.X, impact.Y, impact.Z, 0f));
        shell.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("wobble"), 0.6f);
        shell.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), (float)GD.RandRange(0.0, 10.0));
        shell.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("warmth"), Warmth(Mathf.Max(bubbles, 1)));
        node.AddChild(shell);
        var drops = new MeshInstance3D[Droplets];
        var place = new Vector3[Droplets];
        var vel = new Vector3[Droplets];
        var born = new float[Droplets];
        for (int i = 0; i < Droplets; i++)
        {
            // Spread over the film (a golden-angle spiral, jittered), each released when the hole's rim reaches it.
            float y = 1f - 2f * (i + 0.5f) / Droplets;
            float a = i * 2.39996f + (float)GD.RandRange(-0.3, 0.3);
            float r = Mathf.Sqrt(1f - y * y);
            var p = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            float c = p.Dot(impact);
            born[i] = TearSeconds * (1f - c) * 0.5f;
            // Flung along the rim's sweep (away from the tear) and outward.
            var sweep = p * c - impact;
            sweep = sweep.LengthSquared() > 1e-6f ? sweep.Normalized() : p;
            vel[i] = (sweep * 1.1f + p * 0.7f) * radius * (6f + 3f * (float)GD.Randf());
            place[i] = p;
            drops[i] = new MeshInstance3D { Mesh = _unitSphere, MaterialOverride = DropletMaterial(), Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            drops[i].SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("lantern"), bubbles > 0 ? 1f : 0f);
            node.AddChild(drops[i]);
        }
        _pops.Add(new PopFx { Node = node, Shell = shell, Drops = drops, DropAt = place, DropVel = vel, DropBorn = born, Impact = impact, Radius = radius, Light = bubbles > 0 ? CoreLight(bubbles) : 0f });
    }

    /// <summary>
    /// A shot of her light going out at the end of its range, like a match: a last flicker, a few embers drifting up
    /// and cooling, and a curl of smoke. No flash and no bloom on the water.
    /// </summary>
    public void Extinguish(System.Numerics.Vector2 at, System.Numerics.Vector2 dir)
    {
        var p = new Vector3(at.X, LevelMap.SwimBand, at.Y);
        var on = new Vector3(dir.X, 0f, dir.Y) * 0.6f;
        var gold = new Color(1.2f, 0.75f, 0.3f);
        var ember = new Color(0.9f, 0.3f, 0.06f, 0f);
        for (int i = 0; i < 5; i++)
            _fireworks.Emit(p, on + new Vector3(_fireworks.RandRange(-0.3f, 0.3f), _fireworks.RandRange(0.5f, 1.2f), _fireworks.RandRange(-0.3f, 0.3f)),
                _fireworks.RandRange(0.35f, 0.7f), _fireworks.RandRange(0.05f, 0.09f), 0.01f, gold, ember, drag: 2f, buoyancy: 0.4f);
        // The smoke: a pale curl that rises and spreads as it fades.
        _fireworks.Emit(p, on * 0.5f + Vector3.Up * 0.6f, 0.9f, 0.1f, 0.35f, new Color(0.5f, 0.45f, 0.4f, 0.25f), new Color(0.3f, 0.3f, 0.32f, 0f), drag: 1.5f, buoyancy: 0.3f);
    }

    /// <summary>A shot's afterlight, from its pearls (LightTrails.Style).</summary>
    static LightTrails.Style TrailStyle(PlaneShot shot)
    {
        float jag = 1f, life = 1f, width = 1f;
        // A lance: straight, long, thin. A lure's wisp: softer and lingering. A fuse: crackling wide.
        if (shot.Pierce) { jag *= 0.35f; life *= 1.7f; width *= 0.75f; }
        if (shot.Homing) { jag *= 0.8f; life *= 1.25f; width *= 0.9f; }
        if (shot.Explosive) { jag *= 1.6f; width *= 1.15f; }
        if (shot.Boomerang) life *= 1.3f;
        if (shot.Charged > 0.25f) { width *= 1.4f; life *= 1.2f; }
        // Starfish Arm: as wide as it has grown.
        if (shot.Grow) width *= Mathf.Clamp(shot.Radius / Mathf.Max(shot.BaseRadius, 0.01f), 1f, 2.5f);
        return new LightTrails.Style(jag, life, width, twin: shot.Wave);
    }

    /// <summary>Her light's colour in a shot: her own, or (Prism Pearl) the red, green or blue third of it.</summary>
    public static Color LightOf(PrismHue hue) => hue switch
    {
        PrismHue.Red => new Color(1f, 0.28f, 0.3f),
        PrismHue.Green => new Color(0.32f, 1f, 0.45f),
        PrismHue.Blue => new Color(0.3f, 0.52f, 1f),
        _ => HerColor.Light,
    };

    /// <summary>
    /// A shot of her light sheds light as it flies: every so often (never on a beat) a burst of motes breaks off it and
    /// drifts back, cooling from white-gold to ember as it fades, a ghost of the shot left glowing where it was; now
    /// and then a bigger burst. Its pearls change what it sheds: a lure's wisps float up (Anglerfish Lure), a fuse fizzes
    /// sparks to the sides (Starburst Gland), cavitation leaves a string of bubbles (Pistol Shrimp), a bounce glints
    /// (Mirror Scale).
    /// </summary>
    void Shed(PlaneShot shot, Vector3 at, Vector3 heading, float speed, float mote, float warmth, float light)
    {
        var back = speed > 1e-3f ? -heading / speed : Vector3.Zero;
        var side = new Vector3(-back.Z, 0f, back.X);
        var hot = new Color(1.6f, 1.45f, 1.1f);
        var gold = LightOf(shot.Hue).Lerp(new Color(1f, 0.95f, 0.84f), shot.Hue == PrismHue.None ? warmth : warmth * 0.3f) * 1.25f;
        var ember = new Color(1f, 0.4f, 0.1f, 0f);
        float k = Mathf.Clamp(light, 0.3f, 1.3f);
        bool first = !_shed.TryGetValue(shot, out var was);
        float next = first ? _time + _rng.RandfRange(0f, 0.05f) : was.Next;
        if (_time >= next)
        {
            bool big = _rng.Randf() < 0.2f;
            int n = (big ? _rng.RandiRange(6, 9) : _rng.RandiRange(2, 4)) + (shot.Charged > 0.25f ? 3 : 0) + Mathf.Min(shot.Bubbles - 1, 4);
            for (int i = 0; i < n; i++)
            {
                var off = new Vector3(_rng.RandfRange(-1f, 1f), _rng.RandfRange(-0.3f, 0.6f), _rng.RandfRange(-1f, 1f)) * mote * 0.35f;
                var v = back * speed * _rng.RandfRange(0.04f, 0.14f) + new Vector3(_rng.RandfRange(-1f, 1f), _rng.RandfRange(-0.2f, 0.8f), _rng.RandfRange(-1f, 1f)) * (big ? 1.6f : 0.8f);
                float life = _rng.RandfRange(0.22f, 0.48f) * (big ? 1.3f : 1f);
                float size = _rng.RandfRange(0.08f, 0.18f) * (0.8f + 0.4f * k);
                _afterimages.Emit(at + off, v, life, size, size * 0.15f, i % 3 == 0 ? hot : gold, ember, drag: 3f, buoyancy: shot.Homing ? 0.9f : 0.15f);
            }
            // The afterimage: a ghost of the shot where it was, fading fast.
            _afterimages.Emit(at, Vector3.Zero, 0.2f + (big ? 0.1f : 0f), mote * 0.7f, mote * 0.45f, gold with { A = 0.85f * k }, gold with { A = 0f }, drag: 0f);
            next = _time + _rng.RandfRange(0.045f, 0.13f) * (shot.Explosive ? 0.6f : 1f);
        }
        // A lure's wisp: a slow curl of warm motes rising off it.
        if (shot.Homing && _rng.Randf() < 0.35f)
            _afterimages.Emit(at, back * 0.4f + Vector3.Up * 0.6f + side * _rng.RandfRange(-0.4f, 0.4f), _rng.RandfRange(0.4f, 0.7f), 0.07f, 0.02f, gold, ember, drag: 1.5f, buoyancy: 0.8f);
        // A fuse: sparks fizzing out to its sides.
        if (shot.Explosive && _rng.Randf() < 0.6f)
            _afterimages.Emit(at, side * (_rng.Randf() < 0.5f ? -1f : 1f) * _rng.RandfRange(2f, 3.5f) + back + Vector3.Up * _rng.RandfRange(0.2f, 1f), _rng.RandfRange(0.12f, 0.22f), 0.05f, 0.01f, hot, ember, drag: 5f);
        // Cavitation: a string of small bubbles torn open behind it.
        if (shot.Cavitation && _rng.Randf() < 0.5f)
            BubbleFx.Rise(_wake, at + back * mote * 0.5f + new Vector3(_rng.RandfRange(-0.1f, 0.1f), 0f, _rng.RandfRange(-0.1f, 0.1f)),
                back * 0.6f + new Vector3(_rng.RandfRange(-0.3f, 0.3f), _rng.RandfRange(0.4f, 1.2f), _rng.RandfRange(-0.3f, 0.3f)), _rng.RandfRange(0.04f, 0.09f), new Color(0.95f, 1f, 1f, 1f));
        // A bounce: a glint off the wall it struck, a flash and a spray of sparks.
        if (!first && shot.BouncesLeft < was.Bounces)
        {
            _afterimages.Emit(at, Vector3.Zero, 0.12f, mote * 2.2f, mote * 0.6f, hot, gold with { A = 0f }, drag: 0f);
            for (int i = 0; i < 8; i++)
            {
                var d = _afterimages.RandDir();
                d.Y = Mathf.Abs(d.Y) * 0.5f;
                _afterimages.Emit(at, d * _rng.RandfRange(2f, 4f), _rng.RandfRange(0.15f, 0.3f), 0.06f, 0.01f, hot, ember, drag: 4f);
            }
            AddBloom(shot.Position, 1.2f, 0.35f, 1.6f);
        }
        _shedNext[shot] = (next, shot.BouncesLeft);
    }

    /// <summary>A dash: a small burst of her light where she pushed off (a small firework and a bloom on the water).</summary>
    public void DashBurst(System.Numerics.Vector2 at)
    {
        AddBloom(at, CoreLight(2), BloomSeconds * 0.6f, BloomRadius);
        Firework(at, 2, System.Numerics.Vector2.Zero);
    }

    /// <summary>
    /// Her light struck a body (its middle and the radius of its outline; the way the shot flew; the hit's strength, a
    /// plain shot's 1): flares burst round its outline.
    /// </summary>
    public void Flares(System.Numerics.Vector2 at, float radius, System.Numerics.Vector2 dir, float strength) =>
        _trails.Flares(new Vector3(at.X, LevelMap.SwimBand + 0.2f, at.Y), radius, new Vector3(dir.X, 0f, dir.Y), strength);

    /// <summary>
    /// Her light bursts like a small firework: a white flash, sparks flung out (more and faster for a bigger, merged
    /// shot) that drag to a stop and cool from white-gold to ember orange, a few slower ones drifting up, and a ring of
    /// glints flung back the way it came.
    /// </summary>
    void Firework(System.Numerics.Vector2 at, int bubbles, System.Numerics.Vector2 toward)
    {
        var p = new Vector3(at.X, LevelMap.SwimBand, at.Y);
        float k = Warmth(bubbles);
        var hot = new Color(1.6f, 1.45f, 1.1f);
        var gold = new Color(1.3f, 0.82f, 0.32f);
        var ember = new Color(1f, 0.35f, 0.08f, 0f);
        _fireworks.Emit(p, Vector3.Zero, 0.14f, 0.9f + 1.2f * k, 0.2f, hot, gold with { A = 0f }, drag: 0f);
        int sparks = Mathf.Min(16 + 3 * bubbles, 60);
        float speed = 4.5f + 4f * k;
        var back = new Vector3(-toward.X, 0f, -toward.Y);
        for (int i = 0; i < sparks; i++)
        {
            var dir = _fireworks.RandDir();
            dir.Y = dir.Y * 0.45f + 0.15f;
            // Thrown a little back from what it struck.
            dir = (dir + back * 0.35f).Normalized();
            float life = _fireworks.RandRange(0.35f, 0.7f) * (1f + 0.4f * k);
            _fireworks.Emit(p, dir * speed * _fireworks.RandRange(0.6f, 1.15f), life, _fireworks.RandRange(0.08f, 0.15f) * (1f + 0.5f * k), 0.015f, i % 3 == 0 ? hot : gold, ember, drag: 3.2f);
        }
        for (int i = 0; i < 4 + bubbles / 2; i++)
            _fireworks.Emit(p, new Vector3(_fireworks.RandRange(-0.6f, 0.6f), _fireworks.RandRange(0.8f, 1.8f), _fireworks.RandRange(-0.6f, 0.6f)), _fireworks.RandRange(0.8f, 1.3f), 0.07f, 0.01f, gold, ember, drag: 1.2f, buoyancy: 0.6f);
    }

    /// <summary>
    /// A mob freed (defeated) here: the light that washed it flares, brighter for a bigger bubble. Returns how many
    /// bubbles the one that freed it held (1 when no bubble of hers was near: a dash, an ink cloud).
    /// </summary>
    public int Kill(System.Numerics.Vector2 at)
    {
        int bubbles = Mathf.Max(BubblesNear(at, 3f), 1);
        float k = Warmth(bubbles);
        AddBloom(at, Mathf.Min(0.8f + 1.4f * k, 2.2f), 0.6f + 0.5f * k, Mathf.Min(1.8f + 1.2f * k, 3f));
        return bubbles;
    }

    /// <summary>How many bubbles the bubble of hers last seen nearest this point held (0: none within reach).</summary>
    int BubblesNear(System.Numerics.Vector2 at, float reach)
    {
        int best = 0;
        float nearest = reach * reach;
        foreach (var (_, (pos, bubbles)) in _seen)
        {
            float d = System.Numerics.Vector2.DistanceSquared(pos, at);
            if (d <= nearest)
            {
                nearest = d;
                best = bubbles;
            }
        }
        return best;
    }

    void AddBloom(System.Numerics.Vector2 at, float strength, float life, float radius) =>
        _blooms.Add(new Bloom { At = at, Strength = strength, Life = life, Radius = Mathf.Min(radius, 3f) });

    /// <summary>A new room: no light carries over.</summary>
    public void ClearLights()
    {
        _blooms.Clear();
        _seen.Clear();
        LightCount = 0;
    }

    public void Sync(PlaneWorld world, float dt)
    {
        _time += dt;
        while (_shots.Count < world.Shots.Count)
        {
            var s = new MeshInstance3D { Mesh = _unitSphere, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(s);
            _shots.Add(s);
            var halo = new MeshInstance3D { Mesh = _haloQuad, MaterialOverride = _halo, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Scale = Vector3.One * HaloRadius, Visible = false };
            AddChild(halo);
            _halos.Add(halo);
        }
        _candidates.Clear();
        LensCount = 0;
        _seenNext.Clear();
        for (int i = 0; i < _shots.Count; i++)
        {
            bool used = i < world.Shots.Count;
            _shots[i].Visible = used;
            _halos[i].Visible = false;
            if (!used) continue;
            var shot = world.Shots[i];
            _shots[i].Position = new Vector3(shot.Position.X, LevelMap.SwimBand, shot.Position.Y);
            _shots[i].Mesh = shot.Needle ? _needleMesh : _unitSphere;
            if (shot.Needle)
            {
                // A pufferling's needle: a spike along its flight.
                var d = new Vector3(shot.Velocity.X, 0f, shot.Velocity.Y);
                _shots[i].Basis = d.LengthSquared() > 1e-6f ? new Basis(new Quaternion(Vector3.Up, d.Normalized())) : Basis.Identity;
                _shots[i].MaterialOverride = _needle;
                _shots[i].SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("fade"), 1f);
                continue;
            }
            _shots[i].Basis = Basis.Identity;
            if (shot.FromPlayer)
            {
                // A shot of light: a small hot mote, no bubble (it hits where the sim says, with the sim's radius). It
                // grows a little and brightens as it merges; an Ink Sac one has a violet heart; a Pearl Diver throw is
                // the gathered light, bigger and brighter. Its lightning afterlight is drawn by the trails.
                bool charged = shot.Charged > 0.25f;
                float seed = (System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(shot) & 1023) * 0.0137f;
                float mote = Mathf.Min(0.58f * (1f + 0.1f * (shot.Bubbles - 1)), 1.5f) * (charged ? 1.5f : 1f)
                    // Room for its pearls' marks: the dividing cell (Mitosis), the star (Starfish Arm).
                    * (shot.Split || shot.Grow ? 1.6f : 1f);
                _shots[i].Mesh = _spark;
                _shots[i].Scale = Vector3.One * mote;
                _shots[i].MaterialOverride = _sparkMat;

                // Merged since the last frame: a pulse of light and a chime.
                if (_seen.TryGetValue(shot, out var was) && shot.Bubbles > was.Bubbles)
                {
                    _merges[shot] = 0f;
                    AddBloom(shot.Position, CoreLight(shot.Bubbles) * 1.2f, MergePulseSeconds, GroundRadius);
                    Merged?.Invoke(_shots[i].Position, shot.Bubbles);
                }
                _seenNext[shot] = (shot.Position, shot.Bubbles);
                float pulse = 0f;
                if (_merges.TryGetValue(shot, out float since))
                {
                    since += dt;
                    if (since >= MergePulseSeconds) _merges.Remove(shot);
                    else
                    {
                        _merges[shot] = since;
                        pulse = Mathf.Sin(Mathf.Pi * since / MergePulseSeconds);
                    }
                }
                // Hanging at the end of its range: it breathes slowly like an ember, live for a merge or a dash.
                float ember = shot.Rest > 0f ? 0.78f + 0.22f * Mathf.Sin(_time * 2.4f + seed * 9f) : 1f;
                float light = (charged ? Mathf.Max(CoreLight(shot.Bubbles), 0.75f) : CoreLight(shot.Bubbles)) * ember * (1f + 0.5f * pulse) * shot.Dim;
                float warmth = charged ? Mathf.Max(Warmth(shot.Bubbles), 0.5f) : Warmth(shot.Bubbles);
                _shots[i].SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), seed);
                _shots[i].SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ink"), 0f);
                var hue = LightOf(shot.Hue);
                _shots[i].SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("prism_hue"), shot.Hue == PrismHue.None ? new Color(0f, 0f, 0f, 0f) : hue with { A = 1f });
                // A full one burns white: a small star.
                _shots[i].SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("lantern"), Mathf.Max(light, 0.35f * shot.Dim) * (shot.Bubbles >= PlaneCombatTuning.BubbleCap ? 1.25f : 1f));
                _shots[i].SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("warmth"), warmth);
                // Its pearls, seen in it: a lance (Swordfish Bill), two cores (Mitosis), a five-point star (Starfish
                // Arm), a whirl (Boomerang Shrimp), a ring of nacre (Pearl Diver's charge).
                var heading = new Vector3(shot.Velocity.X, 0f, shot.Velocity.Y);
                float speed = heading.Length();
                _shots[i].SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("motion"), new Vector4(speed > 1e-3f ? heading.X / speed : 1f, 0f, speed > 1e-3f ? heading.Z / speed : 0f, shot.Pierce ? 1.6f : 0f));
                _shots[i].SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("twin"), shot.Split ? 1f : 0f);
                _shots[i].SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("points"), shot.Grow ? 5f : 4f);
                _shots[i].SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("spin"), shot.Boomerang ? 9f : 1f);
                _shots[i].SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("pearl"), Mathf.Clamp((shot.Charged - 0.25f) / 0.75f, 0f, 1f));
                float strength = Mathf.Clamp(light * 1.4f, 0.35f, 1.2f) * (shot.Explosive ? 1.25f : 1f);
                _trails.Feed(shot, _shots[i].Position, warmth, strength, TrailStyle(shot));
                Shed(shot, _shots[i].Position, heading, speed, mote, warmth, light);

                // Its glow in the water: a fixed-size disc, stronger (never wider) as it merges.
                float k = Mathf.Clamp(light / MaxLight, 0f, 1.5f);
                var halo = _halos[i];
                halo.Visible = true;
                halo.Position = _shots[i].Position;
                halo.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("strength"), Mathf.Min(Mathf.Lerp(HaloMin, HaloMax, Warmth(shot.Bubbles)) * (0.6f + 0.4f * k), HaloMax) * (shot.Explosive ? 1.3f : 1f));
                halo.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("color"), LightOf(shot.Hue).Lerp(new Color(1f, 0.95f, 0.84f), shot.Hue == PrismHue.None ? warmth : warmth * 0.3f));
                _candidates.Add((shot.Position, GroundStrength * Mathf.Min(k, 1f) * (shot.Explosive ? 1.3f : 1f), GroundRadius));
                if (LensCount < MaxLenses)
                {
                    var at = _shots[i].GlobalPosition;
                    Lenses[LensCount++] = new Vector4(at.X, at.Y, at.Z, shot.Radius * 1.2f);
                }
            }
            else if (shot.BossPearl)
            {
                // Queen Clam's pearls: lustrous, the royal one big and golden. Drippus Prime's: drops of its ink.
                _shots[i].Scale = Vector3.One * shot.Radius * (shot.Royal ? 1f + 0.08f * Mathf.Sin(_time * 9f) : 1f);
                _shots[i].MaterialOverride = shot.Royal ? _royalPearl : world.Boss?.Spec.Form == OctoShoots.Core.Bosses.BodyForm.Murkling ? _inkDrop : _clamPearl;
            }
            else
            {
                _shots[i].Scale = Vector3.One * PlaneCombatTuning.ShotRadius * 1.3f;
                _shots[i].MaterialOverride = _theirShot;
            }
        }
        (_seen, _seenNext) = (_seenNext, _seen);
        foreach (var gone in _merges.Keys.Where(key => !_seen.ContainsKey(key)).ToList()) _merges.Remove(gone);
        // Firefly Squid's surge lights the swim layer along its way and brightest at its tip.
        if (world.LightSurge is { } surge)
            for (int r = 0; r < surge.Rays.Count; r += Mathf.Max(1, surge.Rays.Count / 5))
            {
                var ray = surge.Rays[r];
                for (int k = 1; k < ray.Points.Count; k++)
                {
                    var a = ray.Points[k - 1];
                    var b = ray.Points[k];
                    float len = System.Numerics.Vector2.Distance(a, b);
                    for (float d = 2f; d < len; d += 2.5f)
                        _candidates.Add((a + (b - a) * (d / len), GroundStrength * 0.6f * surge.Power, GroundRadius));
                }
                _candidates.Add((ray.End, GroundStrength * 1.4f * surge.Power, GroundRadius * 1.6f));
            }
        SyncLights(world, dt);
        _trails.Step(dt, _time, GetViewport().GetCamera3D());
        (_shed, _shedNext) = (_shedNext, _shed);
        _shedNext.Clear();
        if (GetViewport().GetCamera3D() is { } cam)
        {
            _afterimages.Tick(dt, cam.GlobalBasis);
            _wake.Tick(dt, cam.GlobalBasis);
        }
        if (GetViewport().GetCamera3D() is { } camera)
        {
            _fireworks.Tick(dt, camera.GlobalBasis);
            _dust.Tick(dt, camera.GlobalBasis);
        }

        // Popping bubbles: the tear opens from the struck point and sweeps the film away; droplets break off the rim
        // as it passes and fly on, shrinking and fading.
        for (int i = _pops.Count - 1; i >= 0; i--)
        {
            var pop = _pops[i];
            pop.Age += dt;
            if (pop.Age >= TearSeconds + DropSeconds)
            {
                pop.Node.QueueFree();
                _pops.RemoveAt(i);
                continue;
            }
            float open = Mathf.Clamp(pop.Age / TearSeconds, 0f, 1f);
            // The rim speeds up as it goes (the film's tension pulls it).
            open = open * open * (1.6f - 0.6f * open);
            pop.Shell.Visible = open < 1f;
            pop.Shell.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("hole"), new Vector4(pop.Impact.X, pop.Impact.Y, pop.Impact.Z, Mathf.Max(open, 0.001f)));
            // The lantern flares as the film gives way, then dies.
            pop.Shell.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("lantern"), pop.Light * FlareGain * Mathf.Max(0f, 1f - pop.Age / FlareSeconds));
            for (int d = 0; d < pop.Drops.Length; d++)
            {
                float t = pop.Age - pop.DropBorn[d];
                pop.Drops[d].Visible = t >= 0f && t < DropSeconds;
                if (!pop.Drops[d].Visible) continue;
                float k = t / DropSeconds;
                pop.Drops[d].Position = pop.DropAt[d] * pop.Radius + pop.DropVel[d] * t * (1f - 0.45f * k);
                pop.Drops[d].Scale = Vector3.One * pop.Radius * 0.11f * (1f - 0.6f * k);
                pop.Drops[d].SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("fade"), 1f - k * k);
                if (pop.Light > 0f) pop.Drops[d].SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("lantern"), Mathf.Max(0f, 1f - pop.Age / (FlareSeconds * 2f)));
            }
        }

        // Shockwaves: the ring races out (fast, then easing) and thins; the flash is gone in a blink.
        for (int i = _shocks.Count - 1; i >= 0; i--)
        {
            var s = _shocks[i];
            s.Age += dt;
            float k = s.Age / s.Life;
            if (k >= 1f)
            {
                s.Node.QueueFree();
                _shocks.RemoveAt(i);
                continue;
            }
            float ease = 1f - (1f - k) * (1f - k) * (1f - k);
            s.Node.GetChild<MeshInstance3D>(0).Scale = new Vector3(s.Reach * ease, 0.25f, s.Reach * ease);
            s.Ring.AlbedoColor = s.Color with { A = s.Color.A * (1f - k) * (1f - k) };
            float f = Mathf.Clamp(1f - s.Age / 0.12f, 0f, 1f);
            s.Core.Scale = Vector3.One * (0.25f + 0.5f * (1f - f)) * Mathf.Min(s.Reach, 3f) * 0.5f;
            s.Flash.AlbedoColor = new Color(1f, 1f, 1f, 0.9f * f);
        }

        // Whale Song: rings of sound swell out and fade.
        for (int i = _songs.Count - 1; i >= 0; i--)
        {
            var (node, material, age) = _songs[i];
            age += dt;
            _songs[i] = (node, material, age);
            float k = age / SongSeconds;
            if (k >= 1f)
            {
                node.QueueFree();
                _songs.RemoveAt(i);
                continue;
            }
            node.Visible = k > 0f;
            if (k <= 0f) continue;
            float ease = 1f - (1f - k) * (1f - k);
            node.Scale = Vector3.One * (0.6f + 6.5f * ease);
            material.AlbedoColor = new Color(0.55f, 0.95f, 1f, 0.75f * (1f - k));
            material.EmissionEnergyMultiplier = 1.5f * (1f - k);
        }

        while (_pearls.Count < world.Pearls.Count) AddPearl(world.Pearls[_pearls.Count], _time);
        for (int i = 0; i < _pearls.Count && i < world.Pearls.Count; i++)
        {
            var pearl = world.Pearls[i];
            var (look, light) = _pearlLooks[i];
            if (pearl.Absorbing)
            {
                // The ritual: it circles her, paling as she takes its look, and is gone when it shatters.
                _pearls[i].Visible = pearl.Ritual < PearlRitual.ShatterAt;
                float pale = Mathf.Clamp(pearl.Ritual / PearlRitual.Mimic, 0f, 1f);
                _pearls[i].Position = BellView.RitualAt(HerAt, pearl.Ritual);
                _pearls[i].Rotation = new Vector3(0f, _time * 2.5f, 0f);
                // Held, the empty vessel trembles before it breaks.
                float tremble = pearl.Ritual > PearlRitual.Mimic ? 0.04f * Mathf.Sin(_time * 70f) : 0f;
                _pearls[i].Scale = Vector3.One * (0.85f + tremble);
                look?.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("pale"), pale);
                light.LightEnergy = WorldPearlLight * (1f - 0.85f * pale);
                continue;
            }
            _pearls[i].Visible = !pearl.Taken;
            _pearls[i].Scale = Vector3.One;
            float fall = Mathf.Clamp((_time - _pearlBorn[i]) / PearlFall, 0f, 1f);
            float drop = PearlFallHeight * (1f - fall) * (1f - fall);
            // A clam room's pearl lies still on the clam's lower valve; others float and bob.
            bool resting = world.Map.Pois.Exists(q => q.Kind == PoiKind.TreasureCave && q.Position == pearl.Position);
            float y = resting ? ClamRoomView.RestHeight : LevelMap.SwimBand + 0.25f * Mathf.Sin(_time * 1.6f + i);
            // In its clam its light is shadowed: the shell keeps it off what lies behind.
            _pearlLooks[i].Light.ShadowEnabled = resting && OctoShoots.Game.Settings.ViewOptions.Shadows;
            _pearls[i].Position = new Vector3(pearl.Position.X, y + drop, pearl.Position.Y);
            _pearls[i].Rotation = new Vector3(0f, _time * 0.7f, 0f);
        }
        // The shattered pearl's pieces tumble out and dissolve.
        for (int i = _shards.Count - 1; i >= 0; i--)
        {
            var sh = _shards[i];
            sh.Age += dt;
            if (sh.Age >= ShardSeconds)
            {
                sh.Mesh.QueueFree();
                _shards.RemoveAt(i);
                continue;
            }
            float k = sh.Age / ShardSeconds;
            sh.Velocity *= Mathf.Exp(-3f * dt);
            sh.Mesh.Position += sh.Velocity * dt;
            sh.Mesh.Rotation += sh.Spin * dt;
            sh.Material.AlbedoColor = sh.Material.AlbedoColor with { A = 0.95f * (1f - k) };
            sh.Material.EmissionEnergyMultiplier = 0.5f * (1f - k);
            sh.Mesh.Scale *= 1f - 1.5f * dt;
        }

        // Shells: a nautilus each, lying as it fell (any way round), turning slowly about its tilt and bobbing a little.
        foreach (var sh in world.Shells)
        {
            if (_shells.ContainsKey(sh)) continue;
            var m = new MeshInstance3D { Mesh = OctoShoots.Game.Fx.NautilusShell.Mesh, MaterialOverride = OctoShoots.Game.Fx.NautilusShell.Material, CastShadow = GeometryInstance3D.ShadowCastingSetting.On, Scale = Vector3.One * ShellSize };
            AddChild(m);
            var lie = Basis.FromEuler(new Vector3(GD.Randf(), GD.Randf(), GD.Randf()) * Mathf.Tau);
            _shells[sh] = (m, lie, _fireworks.RandDir(), GD.Randf() * Mathf.Tau);
        }
        if (_shells.Count > world.Shells.Count)
            foreach (var gone in _shells.Keys.Where(k => !world.Shells.Contains(k)).ToList())
            {
                _shells[gone].Mesh.QueueFree();
                _shells.Remove(gone);
            }
        foreach (var (sh, view) in _shells)
        {
            view.Mesh.Position = new Vector3(sh.Position.X, LevelMap.SwimBand - 0.25f + 0.08f * Mathf.Sin(_time * 2.2f + view.Phase), sh.Position.Y);
            view.Mesh.Basis = (new Basis(view.Spin, 0.35f * _time + view.Phase) * view.Lie).Scaled(Vector3.One * ShellSize);
        }
        // Light orbs (the sim's hearts): a bubble with her light inside, breathing gently where they fell.
        while (_hearts.Count < world.Hearts.Count)
        {
            var node = LightOrb(1f);
            AddChild(node);
            _hearts.Add(node);
        }
        for (int i = 0; i < _hearts.Count; i++)
        {
            bool used = i < world.Hearts.Count;
            _hearts[i].Visible = used;
            if (!used) continue;
            var heart = world.Hearts[i];
            float breathe = 0.5f + 0.5f * Mathf.Sin(_time * 2.6f + i);
            _hearts[i].Position = new Vector3(heart.Position.X, LevelMap.SwimBand + 0.12f * Mathf.Sin(_time * 1.8f + i), heart.Position.Y);
            _hearts[i].Scale = Vector3.One * (1f + 0.06f * breathe);
        }
        for (int i = 0; i < _stands.Count && i < world.Stands.Count; i++)
        {
            var stand = world.Stands[i];
            _stands[i].Visible = !stand.Sold;
            _stands[i].Position = new Vector3(stand.Position.X, LevelMap.SwimBand + 0.2f * Mathf.Sin(_time * 1.6f + i * 1.3f), stand.Position.Y);
        }
        bool inShop = _shop is not null && System.Numerics.Vector2.Distance(world.Player.Position, _shop.Position) <= _shop.Radius + 1f;
        foreach (var price in _prices) price.Visible = inShop;

    }
}
