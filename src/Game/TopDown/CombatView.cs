using System.Linq;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Placeholder combat on screen: the shooting-dot mobs, everyone's shots, and the gateway in the rift's arena to the
/// next room (bright and turning while open; dim when closed, as it will be until the boss is cleared).
/// </summary>
public partial class CombatView : Node3D
{
    readonly List<MeshInstance3D> _mobs = new();
    readonly List<MeshInstance3D> _shots = new();
    readonly List<Node3D> _pearls = new();
    readonly List<MeshInstance3D> _shells = new();
    readonly List<Node3D> _stands = new();
    /// <summary>The stands' price tags: shown only while Clementine is inside the shop.</summary>
    readonly List<Node3D> _prices = new();
    StandardMaterial3D? _heart;
    static ImageTexture? _tagTexture;
    Poi? _shop;
    StandardMaterial3D _shell = null!;
    StandardMaterial3D _mob = null!, _mobHurt = null!, _herShot = null!, _theirShot = null!, _gate = null!, _gateCore = null!;
    Node3D _gateway = null!;
    StandardMaterial3D _clamPearl = null!, _royalPearl = null!;
    OmniLight3D _gateLight = null!;
    float _time;

    public override void _Ready()
    {
        _mob = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.05f, 0.08f), EmissionEnabled = true, Emission = new Color(0.9f, 0.15f, 0.2f), EmissionEnergyMultiplier = 0.6f, Roughness = 0.4f, RimEnabled = true, Rim = 0.8f };
        _mobHurt = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.9f, 0.9f), EmissionEnabled = true, Emission = new Color(1f, 0.6f, 0.6f), EmissionEnergyMultiplier = 2f };
        _herShot = Glow(new Color(1f, 0.85f, 0.65f));
        _bubble = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/plane_bubble.gdshader") };
        _theirShot = Glow(new Color(1f, 0.25f, 0.3f));
        _clamPearl = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.96f, 0.9f), Roughness = 0.12f, Metallic = 0.3f, RimEnabled = true, Rim = 1f, EmissionEnabled = true, Emission = new Color(1f, 0.82f, 0.9f), EmissionEnergyMultiplier = 0.9f };
        _royalPearl = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.85f, 0.45f), Roughness = 0.1f, Metallic = 0.5f, RimEnabled = true, Rim = 1f, EmissionEnabled = true, Emission = new Color(1f, 0.6f, 0.95f), EmissionEnergyMultiplier = 1.6f };

        _gate = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(1f, 0.6f, 0.25f), EmissionEnabled = true, Emission = new Color(1f, 0.55f, 0.2f), EmissionEnergyMultiplier = 3f };
        _gateCore = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(1f, 0.7f, 0.4f, 0.35f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        };
        _gateway = new Node3D();
        AddChild(_gateway);
        float r = PlaneCombatTuning.GatewayRadius;
        _gateway.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = r - 0.35f, OuterRadius = r, Rings = 48, RingSegments = 12 }, MaterialOverride = _gate });
        _gateway.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = r * 0.55f, OuterRadius = r * 0.62f, Rings = 32, RingSegments = 8 }, MaterialOverride = _gate, Position = new Vector3(0f, 0.05f, 0f) });
        _gateway.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = r - 0.3f, BottomRadius = r - 0.3f, Height = 0.02f, RadialSegments = 40 }, MaterialOverride = _gateCore });
        _gateLight = new OmniLight3D { LightColor = new Color(1f, 0.6f, 0.3f), LightEnergy = 2.5f, OmniRange = 10f, Position = new Vector3(0f, 1.2f, 0f) };
        _gateway.AddChild(_gateLight);
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

    public void Show(PlaneWorld world, ItemCatalog? catalog)
    {
        foreach (var sh in _shells) sh.QueueFree();
        foreach (var st in _stands) st.QueueFree();
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
        // heart. Each hangs a little price tag on a string ("15" and a shell), shown only while she is inside the shop.
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
            var tag = PriceTag(stand.Price);
            node.AddChild(tag);
            _prices.Add(tag);
            AddChild(node);
            _stands.Add(node);
        }
        foreach (var m in _mobs) m.QueueFree();
        foreach (var s in _shots) s.QueueFree();
        foreach (var q in _pearls) q.QueueFree();
        _mobs.Clear();
        _shots.Clear();
        _pearls.Clear();
        _catalog = catalog;
        _pearlBorn.Clear();
        foreach (var pearl in world.Pearls) AddPearl(pearl, -10f);
        var at = world.GatewayPosition;
        _gateway.Position = new Vector3(at.X, LevelMap.SwimBand - 0.4f, at.Y);
    }

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

    const float PopSeconds = 0.22f;

    sealed class PopFx
    {
        public Node3D Node = null!;
        public MeshInstance3D Shell = null!;
        public MeshInstance3D[] Drops = null!;
        public Vector3[] DropDirs = null!;
        public float Radius, Age;
    }

    readonly List<PopFx> _pops = new();
    readonly SphereMesh _unitSphere = new() { Radius = 1f, Height = 2f, RadialSegments = 20, Rings = 10 };
    ShaderMaterial _bubble = null!;

    /// <summary>A bubble popped here (on a mob, on rock, or where it stopped).</summary>
    public void Pop(System.Numerics.Vector2 at, float radius)
    {
        var node = new Node3D { Position = new Vector3(at.X, LevelMap.SwimBand, at.Y) };
        AddChild(node);
        var shell = new MeshInstance3D { Mesh = _unitSphere, MaterialOverride = _bubble, Scale = Vector3.One * radius, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        node.AddChild(shell);
        var drops = new MeshInstance3D[6];
        var dirs = new Vector3[6];
        for (int i = 0; i < drops.Length; i++)
        {
            float a = i * Mathf.Tau / drops.Length + 0.4f;
            dirs[i] = new Vector3(Mathf.Cos(a), 0.35f * Mathf.Sin(a * 2f), Mathf.Sin(a));
            drops[i] = new MeshInstance3D { Mesh = _unitSphere, MaterialOverride = _bubble, Scale = Vector3.One * radius * 0.22f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            node.AddChild(drops[i]);
        }
        _pops.Add(new PopFx { Node = node, Shell = shell, Drops = drops, DropDirs = dirs, Radius = radius });
    }

    public void Sync(PlaneWorld world, float dt)
    {
        _time += dt;
        while (_mobs.Count < world.Mobs.Count)
        {
            var m = new MeshInstance3D { Mesh = new SphereMesh { Radius = PlaneCombatTuning.MobRadius, Height = PlaneCombatTuning.MobRadius * 2f, RadialSegments = 16, Rings = 8 } };
            AddChild(m);
            _mobs.Add(m);
        }
        for (int i = 0; i < _mobs.Count; i++)
        {
            var mob = world.Mobs[i];
            var view = _mobs[i];
            view.Visible = mob.Alive;
            if (!mob.Alive) continue;
            // A slow bob, and a quicker pulse while it hunts.
            float pulse = mob.Aggro ? 1f + 0.12f * Mathf.Sin(_time * 9f + i) : 1f;
            view.Position = new Vector3(mob.Position.X, LevelMap.SwimBand + 0.15f * Mathf.Sin(_time * 1.7f + i * 1.3f), mob.Position.Y);
            view.Scale = Vector3.One * pulse;
            // A short flash on each hit, nothing in between.
            view.MaterialOverride = mob.HitFlash > 0f ? _mobHurt : _mob;
        }

        while (_shots.Count < world.Shots.Count)
        {
            var s = new MeshInstance3D { Mesh = _unitSphere, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(s);
            _shots.Add(s);
        }
        for (int i = 0; i < _shots.Count; i++)
        {
            bool used = i < world.Shots.Count;
            _shots[i].Visible = used;
            if (!used) continue;
            var shot = world.Shots[i];
            _shots[i].Position = new Vector3(shot.Position.X, LevelMap.SwimBand, shot.Position.Y);
            if (shot.FromPlayer)
            {
                // A bubble, wobbling a little as it flies.
                float r = shot.Radius * 1.7f;
                float wob = 0.06f * Mathf.Sin(shot.Age * 18f + i);
                _shots[i].Scale = new Vector3(r * (1f + wob), r * (1f - wob), r * (1f + wob));
                _shots[i].MaterialOverride = _bubble;
                _shots[i].SetInstanceShaderParameter("fade", 1f);
                _shots[i].SetInstanceShaderParameter("rainbow", shot.Bubbles >= PlaneCombatTuning.BubbleCap ? 1f : 0f);
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

        // Popping bubbles: the shell swells and fades, a few droplets fly off.
        for (int i = _pops.Count - 1; i >= 0; i--)
        {
            var pop = _pops[i];
            pop.Age += dt;
            float k = pop.Age / PopSeconds;
            if (k >= 1f)
            {
                pop.Node.QueueFree();
                _pops.RemoveAt(i);
                continue;
            }
            float fade = 1f - k;
            pop.Shell.Scale = Vector3.One * pop.Radius * (1f + 0.9f * Mathf.Sqrt(k));
            pop.Shell.SetInstanceShaderParameter("fade", fade * fade);
            for (int d = 0; d < pop.Drops.Length; d++)
            {
                pop.Drops[d].Position = pop.DropDirs[d] * pop.Radius * (1f + 3.5f * k);
                pop.Drops[d].SetInstanceShaderParameter("fade", fade);
            }
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
        for (int i = 0; i < _stands.Count && i < world.Stands.Count; i++)
        {
            var stand = world.Stands[i];
            _stands[i].Visible = !stand.Sold;
            _stands[i].Position = new Vector3(stand.Position.X, LevelMap.SwimBand + 0.2f * Mathf.Sin(_time * 1.6f + i * 1.3f), stand.Position.Y);
        }
        bool inShop = _shop is not null && System.Numerics.Vector2.Distance(world.Player.Position, _shop.Position) <= _shop.Radius + 1f;
        foreach (var price in _prices) price.Visible = inShop;

        // The gateway turns and breathes while open; closed, it is a dim ring.
        bool open = world.GatewayOpen;
        // Hidden under Queen Clam while she sits on it.
        _gateway.Visible = world.Boss is not { Landed: true, Freed: false };
        _gateway.Rotation = new Vector3(0f, _time * (open ? 0.8f : 0.1f), 0f);
        _gate.EmissionEnergyMultiplier = open ? 2.5f + 0.8f * Mathf.Sin(_time * 2.5f) : 0.3f;
        _gateCore.AlbedoColor = new Color(1f, 0.7f, 0.4f, open ? 0.25f + 0.12f * Mathf.Sin(_time * 3f) : 0.05f);
        _gateLight.LightEnergy = open ? 2.5f : 0.4f;
    }
}
