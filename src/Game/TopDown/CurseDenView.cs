using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The curse den in the reef (DESIGN-TOPDOWN §4.2): black fog seeping out of its tunnel's mouth (the sign of what is
/// inside, seen from the canyon), and, when its end holds one, the shell fountain — a little vent of rock with a pale
/// glow in its throat and bubbles streaming up, from which the shells spout.
/// </summary>
public partial class CurseDenView : Node3D
{
    Node3D? _site;

    /// <summary>A new level: its den's mouth and end.</summary>
    public void Show(PlaneWorld world)
    {
        _site?.QueueFree();
        _site = null;
        if (world.Curse is not { } den) return;
        var map = world.Map;
        var cave = map.Caves[map.Pois[den.Poi].Cave];
        _site = new Node3D();
        AddChild(_site);
        _site.AddChild(Seep(cave.Mouth, cave.Facing));
        if (den.Reward == CurseReward.Fountain) _site.AddChild(Fountain(new Vector3(den.End.X, map.HeightAt(den.End), den.End.Y)));
    }

    static ImageTexture? _puff;

    /// <summary>A soft round puff: opaque in the middle, nothing at the edge.</summary>
    static ImageTexture Puff()
    {
        if (_puff is not null) return _puff;
        const int n = 64;
        var img = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float d = new Vector2(x - n / 2f + 0.5f, y - n / 2f + 0.5f).Length() / (n / 2f);
            float a = Mathf.Clamp(1f - d, 0f, 1f);
            img.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
        }
        return _puff = ImageTexture.CreateFromImage(img);
    }

    /// <summary>The dark seeping out: slow black puffs rolling out of the mouth into the canyon, thinning as they go.</summary>
    static CpuParticles3D Seep(System.Numerics.Vector2 mouth, System.Numerics.Vector2 facing)
    {
        var fade = new Gradient();
        fade.SetColor(0, new Color(1f, 1f, 1f, 0f));
        fade.SetColor(1, new Color(1f, 1f, 1f, 0f));
        fade.AddPoint(0.25f, new Color(1f, 1f, 1f, 1f));
        fade.AddPoint(0.6f, new Color(1f, 1f, 1f, 0.7f));
        var grow = new Curve();
        grow.AddPoint(new Vector2(0f, 0.5f));
        grow.AddPoint(new Vector2(1f, 1f));
        return new CpuParticles3D
        {
            Position = new Vector3(mouth.X, LevelMap.SwimBand + 0.2f, mouth.Y),
            Amount = 44,
            Lifetime = 6f,
            Preprocess = 6f,
            Mesh = new QuadMesh
            {
                Size = new Vector2(1.9f, 1.9f),
                Material = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
                    VertexColorUseAsAlbedo = true,
                    AlbedoColor = new Color(0.02f, 0.008f, 0.04f, 0.36f),
                    AlbedoTexture = Puff(),
                    NoDepthTest = false,
                },
            },
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 1.6f,
            Direction = new Vector3(facing.X, 0.05f, facing.Y),
            Spread = 45f,
            Gravity = Vector3.Zero,
            InitialVelocityMin = 0.35f,
            InitialVelocityMax = 0.9f,
            DampingMin = 0.05f,
            DampingMax = 0.12f,
            AngularVelocityMin = -12f,
            AngularVelocityMax = 12f,
            ScaleAmountMin = 0.8f,
            ScaleAmountMax = 1.6f,
            ScaleAmountCurve = grow,
            ColorRamp = fade,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
    }

    /// <summary>The shell fountain: a squat vent of dark rock, a pale glow in its throat, bubbles streaming up.</summary>
    static Node3D Fountain(Vector3 at)
    {
        var root = new Node3D { Position = at };
        var rock = new StandardMaterial3D { AlbedoColor = new Color(0.16f, 0.13f, 0.16f), Roughness = 0.85f };
        root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.32f, BottomRadius = 0.75f, Height = 0.55f, RadialSegments = 14, Rings = 2 }, MaterialOverride = rock, Position = new Vector3(0f, 0.27f, 0f) });
        root.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.22f, OuterRadius = 0.36f, Rings = 18, RingSegments = 8 }, MaterialOverride = rock, Position = new Vector3(0f, 0.55f, 0f) });
        // The glow in its throat: the shells' own pearly gold.
        var glow = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(1f, 0.88f, 0.6f), EmissionEnabled = true, Emission = new Color(1f, 0.82f, 0.5f), EmissionEnergyMultiplier = 2.5f };
        root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.2f, Height = 0.02f, RadialSegments = 14 }, MaterialOverride = glow, Position = new Vector3(0f, 0.52f, 0f) });
        root.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.82f, 0.55f), LightEnergy = 1.2f, OmniRange = 2.5f, Position = new Vector3(0f, 0.9f, 0f), ShadowEnabled = false });
        root.AddChild(new CpuParticles3D
        {
            Position = new Vector3(0f, 0.55f, 0f),
            Amount = 14,
            Lifetime = 1.6f,
            Mesh = new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 6, Rings = 3, Material = glow },
            Direction = Vector3.Up,
            Spread = 12f,
            Gravity = new Vector3(0f, 0.6f, 0f),
            InitialVelocityMin = 0.4f,
            InitialVelocityMax = 0.9f,
            ScaleAmountMin = 0.5f,
            ScaleAmountMax = 1.4f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        return root;
    }
}
