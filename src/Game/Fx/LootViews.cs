using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Items;
using OctoShoots.Core.Loot;
using OctoShoots.Core.Sim;
using OctoShoots.Game.Util;

namespace OctoShoots.Game.Fx;

/// <summary>
/// Pearl shells that open as Clementine approaches and show a floating description, wooden
/// treasure chests whose lids fly open, and the small pickups.
/// </summary>
public partial class LootViews : Node3D
{
    sealed class ShellView
    {
        public required Node3D Root;
        public required Node3D Lid;
        public required MeshInstance3D Pearl;
        public required OmniLight3D Light;
        public required Label3D Label;
        public required ShaderMaterial Clam;
        public required ShaderMaterial Beam, Pool, Seam;
        public required MeshInstance3D BeamMesh;
        public required CpuParticles3D Sparkles;
        public float Open;
        public float Phase;
        public string? ShownItem;
    }

    sealed class ChestView
    {
        public required Node3D Root;
        public required Node3D Lid;
        public float Open;
    }

    readonly Dictionary<int, ShellView> _shells = new();
    readonly Dictionary<int, ChestView> _chests = new();
    readonly Dictionary<int, (Node3D Node, PickupKind Kind)> _pickups = new();
    ItemCatalog _catalog = null!;
    StandardMaterial3D _wood = null!, _brass = null!;
    Shader _clamShader = null!, _glowShader = null!;
    ArrayMesh _topValve = null!, _bottomValve = null!;
    Texture2D _sparkleTexture = null!;
    float _time;
    OctoShoots.Core.Terrain.VoxelSdf? _sdf;

    public void Init(ItemCatalog catalog) => _catalog = catalog;

    public override void _Ready()
    {
        _clamShader = GD.Load<Shader>("res://assets/shaders/clam.gdshader");
        _glowShader = GD.Load<Shader>("res://assets/shaders/shell_glow.gdshader");
        _topValve = ClamMeshes.Valve(top: true);
        _bottomValve = ClamMeshes.Valve(top: false);
        _sparkleTexture = SparkleTexture();
        _wood = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.28f, 0.14f), Roughness = 0.8f };
        _brass = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.65f, 0.25f), Metallic = 0.7f, Roughness = 0.3f };
    }

    public void Clear()
    {
        foreach (var v in _shells.Values) v.Root.QueueFree();
        foreach (var v in _chests.Values) v.Root.QueueFree();
        foreach (var v in _pickups.Values) v.Node.QueueFree();
        _shells.Clear();
        _chests.Clear();
        _pickups.Clear();
    }

    public void Sync(World world, float alpha, float dt)
    {
        _time += dt;
        _sdf = world.Sdf;
        // Only the nearest open shell shows its description, so neighbouring shop shells don't overlap.
        var eye = world.Player.Position;
        var forward = world.Player.Forward;
        var focus = world.Shells
            .Where(s => s.Open && s.ItemId is not null && System.Numerics.Vector3.Dot(s.Position - eye, forward) > 0f)
            .OrderBy(s => System.Numerics.Vector3.DistanceSquared(s.Position, world.Player.Position))
            .FirstOrDefault();
        foreach (var s in world.Shells) SyncShell(s, dt, s == focus);
        foreach (var c in world.Chests) SyncChest(c, dt);
        SyncPickups(world, alpha);
    }

    // ── shells ──

    /// <summary>Height of the clam's lip above its resting point.</summary>
    const float LipY = 0.3f;

    void SyncShell(PearlShell s, float dt, bool focused)
    {
        if (!_shells.TryGetValue(s.Id, out var v))
        {
            v = BuildShell(s);
            _shells[s.Id] = v;
        }
        v.Open = Mathf.MoveToward(v.Open, s.Open ? 1f : 0f, dt * 1.6f);
        float open = Mathf.SmoothStep(0f, 1f, v.Open);
        bool hasPearl = s.ItemId is not null;

        // Shut, it breathes: the lid lifts a hair now and then and the pearl's light seeps out of the seam.
        float breath = Mathf.Max(0f, Mathf.Sin(_time * 0.9f + v.Phase)) * 2.2f;
        v.Lid.RotationDegrees = new Vector3(-(breath * (1f - open) + 68f * open), 0f, 0f);
        v.Clam.SetShaderParameter("open", open);

        if (s.ItemId != v.ShownItem) ShowItem(v, s);
        // The pearl rises out of its bed as the clam opens; shut, it is hidden inside.
        v.Pearl.Visible = hasPearl && v.Open > 0.03f;
        v.Pearl.Position = new Vector3(0f, LipY + 0.04f + 0.3f * open + 0.04f * Mathf.Sin(_time * 2f) * open, 0.05f);
        v.Pearl.RotationDegrees = new Vector3(0f, _time * 25f, 0f);

        float glow = hasPearl ? 1f : 0.15f;
        v.Light.Visible = true;
        v.Light.LightEnergy = (0.15f + 0.55f * open) * glow;
        v.Light.Position = new Vector3(0f, LipY + 0.1f + 0.5f * open, 0.05f);
        v.Beam.SetShaderParameter("intensity", open * glow * 0.6f);
        v.BeamMesh.Visible = open > 0.01f && hasPearl;
        v.Pool.SetShaderParameter("intensity", (0.12f + 0.25f * open) * glow);
        v.Seam.SetShaderParameter("intensity", (1f - open) * glow * (0.6f + breath * 0.4f));
        v.Sparkles.Emitting = hasPearl;
        v.Sparkles.SpeedScale = 0.6f + 0.8f * open;

        v.Label.Visible = focused && v.Open > 0.05f;
        v.Label.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((v.Open - 0.3f) / 0.7f, 0f, 1f));
        v.Label.OutlineModulate = new Color(0f, 0f, 0f, v.Label.Modulate.A * 0.8f);
    }

    /// <summary>
    /// A giant clam, 1.7 m across: ribbed valves with interlocking wavy lips, mother-of-pearl inside, a glowing
    /// blue mantle, the pearl on its bed, a shaft of light and rising sparkles when it opens. Shop clams are gilded.
    /// </summary>
    ShellView BuildShell(PearlShell s)
    {
        // Settle onto the seabed (the loot spot floats a little above it), sunk slightly into the sand.
        float drop = _sdf is not null && _sdf.Raycast(s.Position, -System.Numerics.Vector3.UnitY, 1.5f, out float hit) ? hit : 0f;
        var root = new Node3D { Position = s.Position.G() - new Vector3(0f, drop - 0.02f, 0f) };
        AddChild(root);
        // Each clam opens toward the most open water around it (where Clementine will come from), a little tilted.
        float yaw = 0f, best = -1f;
        for (int k = 0; k < 16; k++)
        {
            float a = k / 16f * Mathf.Tau;
            var dir = new System.Numerics.Vector3(Mathf.Sin(a), 0.15f, Mathf.Cos(a));
            dir = System.Numerics.Vector3.Normalize(dir);
            float free = _sdf is not null && _sdf.Raycast(s.Position + System.Numerics.Vector3.UnitY * 0.5f, dir, 20f, out float wall) ? wall : 20f;
            if (free > best + 0.01f) { best = free; yaw = a; }
        }
        var body = new Node3D { Rotation = new Vector3(0.04f * Mathf.Sin(s.Id), yaw, 0.04f * Mathf.Cos(s.Id * 1.3f)) };
        root.AddChild(body);

        var clam = new ShaderMaterial { Shader = _clamShader };
        clam.SetShaderParameter("gold", s.Price > 0 ? 1f : 0f);
        MeshInstance3D Mesh(Node3D parent, Mesh mesh, Material material, Vector3 position, GeometryInstance3D.ShadowCastingSetting shadow = GeometryInstance3D.ShadowCastingSetting.On)
        {
            var m = new MeshInstance3D { Mesh = mesh, MaterialOverride = material, Position = position, CastShadow = shadow };
            parent.AddChild(m);
            return m;
        }

        Mesh(body, _bottomValve, clam, new Vector3(0f, LipY, 0f));
        var lid = new Node3D { Position = new Vector3(0f, LipY, -ClamMeshes.HalfLength) };
        body.AddChild(lid);
        Mesh(lid, _topValve, clam, new Vector3(0f, 0f, ClamMeshes.HalfLength));

        ShaderMaterial Glow(int mode, float height = 3.5f)
        {
            var m = new ShaderMaterial { Shader = _glowShader };
            m.SetShaderParameter("mode", mode);
            m.SetShaderParameter("height", height);
            return m;
        }
        var off = GeometryInstance3D.ShadowCastingSetting.Off;
        // Light seeping through the seam of a shut clam: a glowing ring just inside the lip.
        var seam = Glow(2);
        Mesh(body, new PlaneMesh { Size = new Vector2(ClamMeshes.HalfWidth * 2f, ClamMeshes.HalfLength * 2f) }, seam, new Vector3(0f, LipY + 0.01f, 0f), off);
        // A pool of light on the sand.
        var pool = Glow(1);
        Mesh(root, new PlaneMesh { Size = new Vector2(4.5f, 4.5f) }, pool, new Vector3(0f, 0.04f, 0f), off);
        // A shaft of light rising from the open clam.
        var beam = Glow(0, 3.6f);
        var beamMesh = Mesh(root, new CylinderMesh { TopRadius = 1.1f, BottomRadius = 0.35f, Height = 3.6f, RadialSegments = 24, CapTop = false, CapBottom = false }, beam, new Vector3(0f, LipY + 1.8f, 0f), off);

        var pearl = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.23f, Height = 0.46f, RadialSegments = 28, Rings = 14 } };
        body.AddChild(pearl);
        var light = new OmniLight3D { OmniRange = 3.5f, OmniAttenuation = 1.6f, LightEnergy = 0.3f, ShadowEnabled = false };
        body.AddChild(light);

        // Tiny sparkles drifting up out of the clam.
        var sparkleMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            VertexColorUseAsAlbedo = true,
            AlbedoTexture = _sparkleTexture,
        };
        var fade = new Gradient();
        fade.SetColor(0, new Color(1f, 1f, 1f, 0f));
        fade.SetColor(1, new Color(1f, 1f, 1f, 0f));
        fade.AddPoint(0.2f, new Color(1f, 1f, 1f, 1f));
        fade.AddPoint(0.7f, new Color(1f, 1f, 1f, 0.8f));
        var sparkles = new CpuParticles3D
        {
            Amount = 18,
            Lifetime = 3.5f,
            Mesh = new QuadMesh { Size = new Vector2(0.09f, 0.09f), Material = sparkleMaterial },
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.45f,
            Direction = Vector3.Up,
            Spread = 25f,
            InitialVelocityMin = 0.15f,
            InitialVelocityMax = 0.4f,
            Gravity = new Vector3(0f, 0.04f, 0f),
            ScaleAmountMin = 0.4f,
            ScaleAmountMax = 1.2f,
            ColorRamp = fade,
            Position = new Vector3(0f, LipY + 0.2f, 0f),
            CastShadow = off,
        };
        body.AddChild(sparkles);

        var label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            FixedSize = true,
            FontSize = 30,
            OutlineSize = 8,
            PixelSize = 0.0008f,
            Position = new Vector3(0f, 1.9f, 0f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Width = 520f,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Visible = false,
        };
        root.AddChild(label);
        return new ShellView
        {
            Root = root, Lid = lid, Pearl = pearl, Light = light, Label = label, Clam = clam,
            Beam = beam, Pool = pool, Seam = seam, BeamMesh = beamMesh, Sparkles = sparkles, Phase = s.Id * 1.7f,
        };
    }

    void ShowItem(ShellView v, PearlShell s)
    {
        v.ShownItem = s.ItemId;
        if (s.ItemId is null || !_catalog.TryGet(s.ItemId, out var item)) return;
        v.Pearl.MaterialOverride = PearlMaterials.Get(item, overlay: false);
        Color glow = PearlMaterials.GlowColor(item);
        v.Light.LightColor = glow;
        v.Clam.SetShaderParameter("glow_color", glow);
        foreach (var m in new[] { v.Beam, v.Pool, v.Seam }) m.SetShaderParameter("color", glow);
        v.Sparkles.Color = glow.Lerp(Colors.White, 0.4f);
        var lines = ItemCaption.Describe(item);
        string price = s.Price > 0 ? $"\n◎ {s.Price} sand dollars" : "";
        v.Label.Text = $"{item.Name}\n{item.Tagline}\n{string.Join("\n", lines)}{price}";
    }

    /// <summary>A four-pointed glint with a soft core, for the sparkles.</summary>
    static Texture2D SparkleTexture()
    {
        const int size = 32;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float core = Mathf.Max(0f, 1f - r * 2.2f);
            float star = Mathf.Max(0f, 1f - Mathf.Abs(dx) * 14f) * Mathf.Max(0f, 1f - Mathf.Abs(dy)) + Mathf.Max(0f, 1f - Mathf.Abs(dy) * 14f) * Mathf.Max(0f, 1f - Mathf.Abs(dx));
            float a = Mathf.Clamp(core * core + star * 0.8f, 0f, 1f);
            image.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        return ImageTexture.CreateFromImage(image);
    }

    // ── chests ──

    void SyncChest(TreasureChest c, float dt)
    {
        if (!_chests.TryGetValue(c.Id, out var v))
        {
            var root = new Node3D { Position = c.Position.G() + new Vector3(0f, -0.2f, 0f) };
            AddChild(root);
            root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.9f, 0.45f, 0.6f) }, MaterialOverride = _wood });
            foreach (float x in new[] { -0.3f, 0.3f })
                root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.06f, 0.47f, 0.62f) }, MaterialOverride = _brass, Position = new Vector3(x, 0f, 0f) });
            var lid = new Node3D { Position = new Vector3(0f, 0.22f, -0.3f) };
            root.AddChild(lid);
            lid.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.3f, Height = 0.9f }, MaterialOverride = _wood, RotationDegrees = new Vector3(0f, 0f, 90f), Scale = new Vector3(1f, 1f, 0.6f), Position = new Vector3(0f, 0f, 0.3f) });
            lid.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.12f, 0.12f, 0.06f) }, MaterialOverride = _brass, Position = new Vector3(0f, 0.02f, 0.62f) });
            v = new ChestView { Root = root, Lid = lid };
            _chests[c.Id] = v;
        }
        v.Open = Mathf.MoveToward(v.Open, c.Opened ? 1f : 0f, dt * 4f);
        v.Lid.RotationDegrees = new Vector3(-110f * Mathf.SmoothStep(0f, 1f, v.Open), 0f, 0f);
    }

    // ── pickups ──

    void SyncPickups(World world, float alpha)
    {
        var seen = new HashSet<int>();
        foreach (var k in world.Pickups)
        {
            seen.Add(k.Id);
            if (!_pickups.TryGetValue(k.Id, out var entry))
            {
                entry = (BuildPickup(k.Kind), k.Kind);
                AddChild(entry.Node);
                _pickups[k.Id] = entry;
            }
            entry.Node.Position = Conv.Lerp(k.PrevPosition, k.Position, alpha) + new Vector3(0f, 0.06f * Mathf.Sin(_time * 3f + k.Id), 0f);
            entry.Node.Rotation = new Vector3(0.3f, _time * 2f + k.Id, 0f);
        }
        foreach (var id in _pickups.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            _pickups[id].Node.QueueFree();
            _pickups.Remove(id);
        }
    }

    Node3D BuildPickup(PickupKind kind)
    {
        var node = new Node3D();
        StandardMaterial3D Glowing(Color c, float energy, float alpha = 1f) => new()
        {
            AlbedoColor = new Color(c, alpha),
            EmissionEnabled = true,
            Emission = c,
            EmissionEnergyMultiplier = energy,
            Transparency = alpha < 1f ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
            Metallic = 0.4f,
            Roughness = 0.3f,
        };
        Mesh Coin(float r) => new CylinderMesh { TopRadius = r, BottomRadius = r, Height = 0.035f, RadialSegments = 20 };
        (Mesh mesh, Material material, Vector3 scale) look = kind switch
        {
            PickupKind.Coin => (Coin(0.13f), Glowing(new Color(1f, 0.85f, 0.35f), 0.6f), Vector3.One),
            PickupKind.Nickel => (Coin(0.17f), Glowing(new Color(0.85f, 0.9f, 1f), 0.6f), Vector3.One),
            PickupKind.Dime => (Coin(0.2f), Glowing(new Color(0.5f, 0.8f, 1f), 0.8f), Vector3.One),
            PickupKind.Heart => (new SphereMesh { Radius = 0.14f, Height = 0.28f }, Glowing(new Color(1f, 0.25f, 0.3f), 1.5f), new Vector3(1f, 0.85f, 0.7f)),
            PickupKind.HalfHeart => (new SphereMesh { Radius = 0.1f, Height = 0.2f }, Glowing(new Color(1f, 0.35f, 0.4f), 1.2f), new Vector3(1f, 0.85f, 0.7f)),
            PickupKind.FoamHeart => (new SphereMesh { Radius = 0.15f, Height = 0.3f }, Glowing(new Color(0.75f, 0.92f, 1f), 1f, 0.7f), Vector3.One),
            PickupKind.Bomb => (new SphereMesh { Radius = 0.15f, Height = 0.3f }, new StandardMaterial3D { AlbedoColor = new Color(0.08f, 0.06f, 0.14f), RimEnabled = true }, Vector3.One),
            _ => (new CapsuleMesh { Radius = 0.08f, Height = 0.3f }, Glowing(new Color(0.4f, 1f, 0.9f), 2f, 0.75f), Vector3.One),
        };
        node.AddChild(new MeshInstance3D { Mesh = look.mesh, MaterialOverride = look.material, Scale = look.scale, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        return node;
    }
}
