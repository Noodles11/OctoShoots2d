using System.Linq;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Combat on screen: everyone's shots (her bubbles, Queen Clam's pearls, the pufferlings' needles), pearls and the shop's
/// stands. (The pufferlings themselves are PufferlingView; the way on is the level's shaft, drawn by the level.)
/// </summary>
public partial class CombatView : Node3D
{
    readonly List<MeshInstance3D> _shots = new();
    readonly List<Node3D> _pearls = new();
    readonly List<MeshInstance3D> _shells = new();
    /// <summary>Hearts dropped by freed mobs: a small red heart and its glow each, pooled.</summary>
    readonly List<Node3D> _hearts = new();
    readonly List<Node3D> _stands = new();
    /// <summary>The stands' price tags: shown only while Clementine is inside the shop.</summary>
    readonly List<Node3D> _prices = new();
    StandardMaterial3D? _heart;
    static ImageTexture? _tagTexture;
    Poi? _shop;
    StandardMaterial3D _shell = null!;
    StandardMaterial3D _herShot = null!, _theirShot = null!;
    Mesh _needleMesh = null!;
    ShaderMaterial _needle = null!;
    StandardMaterial3D _clamPearl = null!, _royalPearl = null!;
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
        _theirShot = Glow(new Color(1f, 0.25f, 0.3f));
        _clamPearl = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.96f, 0.9f), Roughness = 0.12f, Metallic = 0.3f, RimEnabled = true, Rim = 1f, EmissionEnabled = true, Emission = new Color(1f, 0.82f, 0.9f), EmissionEnergyMultiplier = 0.9f };
        _royalPearl = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.85f, 0.45f), Roughness = 0.1f, Metallic = 0.5f, RimEnabled = true, Rim = 1f, EmissionEnabled = true, Emission = new Color(1f, 0.6f, 0.95f), EmissionEnergyMultiplier = 1.6f };

    }

    static StandardMaterial3D Glow(Color c) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        AlbedoColor = c,
        EmissionEnabled = true,
        Emission = c,
        EmissionEnergyMultiplier = 3f,
    };

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

    /// <summary>A small scallop shell: a flattened, ribbed dome.</summary>
    static ArrayMesh ShellMesh()
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        const int ribs = 9;
        const float fan = 2.4f, r = 0.24f;
        var hinge = new Vector3(0f, 0.02f, r * 0.55f);
        for (int i = 0; i < ribs; i++)
        {
            float a0 = -fan / 2f + fan * i / ribs, a1 = -fan / 2f + fan * (i + 1) / ribs, am = (a0 + a1) * 0.5f;
            Vector3 P(float a, float lift) => new(Mathf.Sin(a) * r, lift, -Mathf.Cos(a) * r + r * 0.55f);
            Vector3 e0 = P(a0, 0.02f), e1 = P(a1, 0.02f), ridge = P(am, 0.09f);
            foreach (var (a, b) in new[] { (e0, ridge), (ridge, e1) })
            {
                var n = (b - hinge).Cross(a - hinge).Normalized();
                st.SetNormal(n);
                st.AddVertex(hinge);
                st.AddVertex(b);
                st.AddVertex(a);
            }
        }
        return st.Commit();
    }

    /// <param name="fresh">Pearls offered for the first time since an achievement unlocked them: they wear a NEW chip.</param>
    public void Show(PlaneWorld world, ItemCatalog? catalog, ISet<string>? fresh = null)
    {
        foreach (var sh in _shells) sh.QueueFree();
        foreach (var st in _stands) st.QueueFree();
        foreach (var h in _hearts) h.QueueFree();
        _hearts.Clear();
        _shells.Clear();
        _stands.Clear();
        _prices.Clear();
        _shop = world.Map.Pois.FirstOrDefault(p => p.Kind == PoiKind.Shop);
        _shell ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.86f, 0.78f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.7f, 0.55f),
            EmissionEnergyMultiplier = 0.9f,
            Roughness = 0.35f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        // The shop's goods float on their own, like the treasure room's pearl: pearls in their colours, the top-up a red
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
                node.AddChild(new MeshInstance3D { Mesh = HeartMesh(), MaterialOverride = _heart ??= HeartMaterial() });
                node.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.4f, 0.45f), LightEnergy = 1.2f, OmniRange = 4f, ShadowEnabled = false });
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
        foreach (var q in _pearls) q.QueueFree();
        _shots.Clear();
        _halos.Clear();
        _merges.Clear();
        ClearLights();
        _pearls.Clear();
        _catalog = catalog;
        _pearlBorn.Clear();
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
    const float PearlFall = 1.4f, PearlFallHeight = 7f;

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
            node.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.42f, Height = 0.84f, RadialSegments = 24, Rings = 12 },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = a, EmissionEnabled = true, Emission = b, EmissionEnergyMultiplier = 1.4f, Roughness = 0.15f, Metallic = 0.3f, RimEnabled = true, Rim = 1f },
            });
            node.AddChild(new OmniLight3D { LightColor = b.Lerp(Colors.White, 0.3f), LightEnergy = 1.8f, OmniRange = 5f, ShadowEnabled = false });
            AddChild(node);
            _pearls.Add(node);
            _pearlBorn.Add(born);
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

    const float BlastSeconds = 0.65f;

    sealed class BlastFx
    {
        public Node3D Node = null!;
        public MeshInstance3D Cloud = null!;
        public MeshInstance3D[] Puffs = null!;
        public Vector3[] PuffDirs = null!;
        public StandardMaterial3D Material = null!;
        public float Radius, Age;
    }

    readonly List<BlastFx> _blasts = new();

    /// <summary>Ink Sac: a bubble burst into ink here — a dark violet cloud billows out to the blast's reach and thins away.</summary>
    public void InkBlast(System.Numerics.Vector2 at, float radius)
    {
        var node = new Node3D { Position = new Vector3(at.X, LevelMap.SwimBand, at.Y) };
        AddChild(node);
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.16f, 0.06f, 0.26f, 0.7f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            EmissionEnabled = true,
            Emission = new Color(0.45f, 0.15f, 0.75f),
            EmissionEnergyMultiplier = 0.6f,
            RimEnabled = true,
        };
        var cloud = new MeshInstance3D { Mesh = _unitSphere, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        node.AddChild(cloud);
        var puffs = new MeshInstance3D[8];
        var dirs = new Vector3[8];
        for (int i = 0; i < puffs.Length; i++)
        {
            float a = i * Mathf.Tau / puffs.Length + 0.3f * Mathf.Sin(i * 2.7f);
            dirs[i] = new Vector3(Mathf.Cos(a), 0.25f + 0.2f * Mathf.Sin(i * 1.9f), Mathf.Sin(a));
            puffs[i] = new MeshInstance3D { Mesh = _unitSphere, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            node.AddChild(puffs[i]);
        }
        _blasts.Add(new BlastFx { Node = node, Cloud = cloud, Puffs = puffs, PuffDirs = dirs, Material = material, Radius = radius });
    }

    const float SongSeconds = 1.6f;
    readonly List<(Node3D Node, StandardMaterial3D Material, float Age)> _songs = new();

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
        if (bubbles > 0) AddBloom(at, CoreLight(bubbles), BloomSeconds, BloomRadius);
        var node = new Node3D { Position = new Vector3(at.X, LevelMap.SwimBand, at.Y) };
        AddChild(node);
        var impact = new Vector3(toward.X, 0.35f, toward.Y);
        impact = impact.LengthSquared() > 1e-6f ? impact.Normalized() : Vector3.Up;
        var shell = new MeshInstance3D { Mesh = _unitSphere, MaterialOverride = _bubble, Scale = Vector3.One * radius, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        shell.SetInstanceShaderParameter("hole", new Vector4(impact.X, impact.Y, impact.Z, 0f));
        shell.SetInstanceShaderParameter("wobble", 0.6f);
        shell.SetInstanceShaderParameter("seed", (float)GD.RandRange(0.0, 10.0));
        shell.SetInstanceShaderParameter("warmth", Warmth(Mathf.Max(bubbles, 1)));
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
            drops[i].SetInstanceShaderParameter("lantern", bubbles > 0 ? 1f : 0f);
            node.AddChild(drops[i]);
        }
        _pops.Add(new PopFx { Node = node, Shell = shell, Drops = drops, DropAt = place, DropVel = vel, DropBorn = born, Impact = impact, Radius = radius, Light = bubbles > 0 ? CoreLight(bubbles) : 0f });
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
                _shots[i].SetInstanceShaderParameter("fade", 1f);
                continue;
            }
            _shots[i].Basis = Basis.Identity;
            if (shot.FromPlayer)
            {
                // A light bubble: a film that wobbles, livelier as it moves, weaving a little off its line the way a
                // real one does (drawn only; it hits where the sim says), with a lantern inside. An Ink Sac one is dark
                // with ink, its lantern muffled; a Pearl Diver throw is the gathered light, a bright lantern.
                bool charged = shot.Charged > 0.25f;
                float r = shot.Radius * (charged ? 1.3f : 1.7f);
                float seed = (System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(shot) & 1023) * 0.0137f;
                float speed = shot.Velocity.Length();
                float lively = Mathf.Clamp(speed / 8f, 0f, 1f);
                var side = speed > 1e-3f ? new Vector3(-shot.Velocity.Y, 0f, shot.Velocity.X) / speed : Vector3.Zero;
                _shots[i].Position += side * Mathf.Sin(shot.Age * 11f + seed * 7f) * 0.05f * lively + Vector3.Up * 0.04f * Mathf.Sin(shot.Age * 3.1f + seed * 5f);
                _shots[i].Scale = Vector3.One * r;
                _shots[i].MaterialOverride = _bubble;

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
                bool full = shot.Bubbles >= PlaneCombatTuning.BubbleCap;
                _shots[i].SetInstanceShaderParameter("seed", seed);
                _shots[i].SetInstanceShaderParameter("wobble", (0.45f + 0.55f * lively) * (charged ? 0.5f : 1f));
                _shots[i].SetInstanceShaderParameter("hole", new Vector4(0f, 1f, 0f, 0f));
                _shots[i].SetInstanceShaderParameter("fade", 1f);
                _shots[i].SetInstanceShaderParameter("ink", shot.Explosive ? 1f : 0f);
                // A full bubble is a small star: the rainbow sheen becomes its faint corona.
                _shots[i].SetInstanceShaderParameter("rainbow", full ? 0.55f : 0f);
                _shots[i].SetInstanceShaderParameter("lantern", light);
                _shots[i].SetInstanceShaderParameter("warmth", warmth);

                // Its glow in the water: a fixed-size disc, stronger (never wider) as it merges.
                float k = Mathf.Clamp(light / MaxLight, 0f, 1.5f);
                var halo = _halos[i];
                halo.Visible = true;
                halo.Position = _shots[i].Position;
                halo.SetInstanceShaderParameter("strength", Mathf.Min(Mathf.Lerp(HaloMin, HaloMax, Warmth(shot.Bubbles)) * (0.6f + 0.4f * k), HaloMax) * (shot.Explosive ? 0.4f : 1f));
                halo.SetInstanceShaderParameter("color", new Color(1f, 0.769f, 0.42f).Lerp(new Color(1f, 0.95f, 0.84f), warmth));
                _candidates.Add((shot.Position, GroundStrength * Mathf.Min(k, 1f) * (shot.Explosive ? 0.4f : 1f), GroundRadius));
            }
            else if (shot.BossPearl)
            {
                // Queen Clam's pearls: lustrous, the royal one big and golden.
                _shots[i].Scale = Vector3.One * shot.Radius * (shot.Royal ? 1f + 0.08f * Mathf.Sin(_time * 9f) : 1f);
                _shots[i].MaterialOverride = shot.Royal ? _royalPearl : _clamPearl;
            }
            else
            {
                _shots[i].Scale = Vector3.One * PlaneCombatTuning.ShotRadius * 1.3f;
                _shots[i].MaterialOverride = _theirShot;
            }
        }
        (_seen, _seenNext) = (_seenNext, _seen);
        foreach (var gone in _merges.Keys.Where(key => !_seen.ContainsKey(key)).ToList()) _merges.Remove(gone);
        SyncLights(world, dt);

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
            pop.Shell.SetInstanceShaderParameter("hole", new Vector4(pop.Impact.X, pop.Impact.Y, pop.Impact.Z, Mathf.Max(open, 0.001f)));
            // The lantern flares as the film gives way, then dies.
            pop.Shell.SetInstanceShaderParameter("lantern", pop.Light * FlareGain * Mathf.Max(0f, 1f - pop.Age / FlareSeconds));
            for (int d = 0; d < pop.Drops.Length; d++)
            {
                float t = pop.Age - pop.DropBorn[d];
                pop.Drops[d].Visible = t >= 0f && t < DropSeconds;
                if (!pop.Drops[d].Visible) continue;
                float k = t / DropSeconds;
                pop.Drops[d].Position = pop.DropAt[d] * pop.Radius + pop.DropVel[d] * t * (1f - 0.45f * k);
                pop.Drops[d].Scale = Vector3.One * pop.Radius * 0.11f * (1f - 0.6f * k);
                pop.Drops[d].SetInstanceShaderParameter("fade", 1f - k * k);
                if (pop.Light > 0f) pop.Drops[d].SetInstanceShaderParameter("lantern", Mathf.Max(0f, 1f - pop.Age / (FlareSeconds * 2f)));
            }
        }

        // Ink blasts: the cloud billows out to its reach and thins; puffs roll outward and up.
        for (int i = _blasts.Count - 1; i >= 0; i--)
        {
            var blast = _blasts[i];
            blast.Age += dt;
            float k = blast.Age / BlastSeconds;
            if (k >= 1f)
            {
                blast.Node.QueueFree();
                _blasts.RemoveAt(i);
                continue;
            }
            float grow = 1f - (1f - k) * (1f - k) * (1f - k);
            blast.Cloud.Scale = new Vector3(1f, 0.55f, 1f) * blast.Radius * (0.35f + 0.65f * grow);
            blast.Material.AlbedoColor = new Color(0.16f, 0.06f, 0.26f, 0.7f * (1f - k) * (1f - k));
            blast.Material.EmissionEnergyMultiplier = 0.9f * (1f - k);
            for (int d = 0; d < blast.Puffs.Length; d++)
            {
                blast.Puffs[d].Position = blast.PuffDirs[d] * blast.Radius * (0.4f + 0.75f * grow);
                blast.Puffs[d].Scale = Vector3.One * blast.Radius * 0.32f * (1f - 0.5f * k);
            }
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
            _pearls[i].Visible = !pearl.Taken;
            float fall = Mathf.Clamp((_time - _pearlBorn[i]) / PearlFall, 0f, 1f);
            float drop = PearlFallHeight * (1f - fall) * (1f - fall);
            _pearls[i].Position = new Vector3(pearl.Position.X, LevelMap.SwimBand + drop + 0.25f * Mathf.Sin(_time * 1.6f + i), pearl.Position.Y);
            _pearls[i].Rotation = new Vector3(0f, _time * 0.7f, 0f);
        }

        // Shells: a pooled mesh each, turning slowly and bobbing a little on the swim band.
        while (_shells.Count < world.Shells.Count)
        {
            var m = new MeshInstance3D { Mesh = ShellMesh(), MaterialOverride = _shell, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(m);
            _shells.Add(m);
        }
        for (int i = 0; i < _shells.Count; i++)
        {
            bool used = i < world.Shells.Count;
            _shells[i].Visible = used;
            if (!used) continue;
            var sh = world.Shells[i];
            _shells[i].Position = new Vector3(sh.Position.X, LevelMap.SwimBand - 0.25f + 0.08f * Mathf.Sin(_time * 2.2f + i * 0.7f), sh.Position.Y);
            _shells[i].Rotation = new Vector3(0.35f, _time * 0.8f + i, 0f);
            _shells[i].Scale = Vector3.One * 1.7f;
        }
        // Hearts: like the shop's heart but smaller, beating gently where they fell.
        while (_hearts.Count < world.Hearts.Count)
        {
            var node = new Node3D();
            node.AddChild(new MeshInstance3D { Mesh = HeartMesh(), MaterialOverride = _heart ??= HeartMaterial(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            node.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.4f, 0.45f), LightEnergy = 0.8f, OmniRange = 2.5f, ShadowEnabled = false });
            AddChild(node);
            _hearts.Add(node);
        }
        for (int i = 0; i < _hearts.Count; i++)
        {
            bool used = i < world.Hearts.Count;
            _hearts[i].Visible = used;
            if (!used) continue;
            var heart = world.Hearts[i];
            float beat = Mathf.Pow(Mathf.Max(Mathf.Sin(_time * 5f + i), 0f), 6f);
            _hearts[i].Position = new Vector3(heart.Position.X, LevelMap.SwimBand + 0.12f * Mathf.Sin(_time * 1.8f + i), heart.Position.Y);
            _hearts[i].Scale = Vector3.One * 0.7f * (1f + 0.12f * beat);
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
