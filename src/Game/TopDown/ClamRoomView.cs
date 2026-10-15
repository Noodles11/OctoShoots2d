using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Clam rooms (the treasure caves, DESIGN-TOPDOWN §4.2): the room is the inside of a giant clam. Its chamber is lined
/// with a shell wall of glowing mother-of-pearl (open at the mouth); in the middle a giant clam's lower valve holds a
/// pink velvet tongue, and the pearl lies on it. Gold motes spiral round the pearl, and a warm gold light fills the room
/// and spills out of its mouth into the canyon, with a few motes drifting out, so it can be found; it shines brighter
/// the deeper she is (the water is darker there). When the pearl is taken the motes fling out and dissolve, and the
/// light settles to an ember.
/// </summary>
public partial class ClamRoomView : Node3D
{
    sealed class Room
    {
        public Node3D Root = null!;
        public ShaderMaterial Sparks = null!;
        public OmniLight3D Inner = null!, Spill = null!;
        public CpuParticles3D Drift = null!;
        public System.Numerics.Vector2 At, Mouth;
        public float Gone, Bright;
    }

    const int Motes = 80;
    /// <summary>The pearl's tongue: its top just under the pearl's middle less its radius, so the pearl sinks into it a little.</summary>
    public const float RestHeight = LevelMap.SwimBand + 0.02f;

    readonly List<Room> _rooms = new();

    /// <summary>The rooms' gold glows for the water pass (xz, radius, strength): the room, and its mouth.</summary>
    public List<Vector4> Glows { get; } = new();
    static readonly Color Gold = new(1f, 0.8f, 0.45f);

    /// <summary>A new level: its clam rooms.</summary>
    public void Show(PlaneWorld world)
    {
        foreach (var r in _rooms) r.Root.QueueFree();
        _rooms.Clear();
        var map = world.Map;
        // Brighter the deeper she goes: the rooms are beacons in the dark.
        float bright = Mathf.Clamp(0.6f + 0.3f * (map.Depth - 1), 0.6f, 2.2f);
        foreach (var poi in map.Pois.Where(p => p.Kind == PoiKind.TreasureCave && p.Cave >= 0))
            _rooms.Add(Build(map, poi, map.Caves[poi.Cave], bright));
    }

    Room Build(LevelMap map, Poi poi, CaveSite cave, float bright)
    {
        var at = poi.Position;
        float floor = map.HeightAt(at);
        var root = new Node3D();
        AddChild(root);
        var nacre = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/clam_nacre.gdshader") };
        var velvet = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/clam_velvet.gdshader") };

        // The shell wall round the chamber, open toward the mouth.
        float chamber = cave.Chambers[0].Radius;
        float facing = Mathf.Atan2(cave.Facing.Y, cave.Facing.X);
        float wallTop = LevelMap.SwimBand + 2.4f;
        root.AddChild(new MeshInstance3D
        {
            Mesh = ClamRoomMeshes.ShellWall(chamber - 0.4f, wallTop - floor, facing + 0.8f, facing + Mathf.Tau - 0.8f),
            MaterialOverride = nacre,
            Position = new Vector3(at.X, floor, at.Y),
        });
        // The giant clam: its lower valve from the floor up to just under the swim level, the velvet tongue in it.
        float rim = LevelMap.SwimBand - 0.25f;
        root.AddChild(new MeshInstance3D
        {
            Mesh = ClamRoomMeshes.LowerValve(1.9f, rim - floor + 0.4f),
            MaterialOverride = nacre,
            Position = new Vector3(at.X, rim, at.Y),
        });
        root.AddChild(new MeshInstance3D
        {
            Mesh = ClamRoomMeshes.Tongue(1.4f, 0.44f),
            MaterialOverride = velvet,
            Position = new Vector3(at.X, RestHeight - 0.42f - 0.2f, at.Y),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });

        // Gold motes spiralling round the pearl.
        var sparks = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/clam_sparkles.gdshader") };
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = new QuadMesh { Size = Vector2.One }, InstanceCount = Motes };
        var rng = new RandomNumberGenerator { Seed = (ulong)(at.X * 131f + at.Y * 977f) };
        for (int i = 0; i < Motes; i++)
        {
            mm.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * rng.RandfRange(0.06f, 0.12f)), new Vector3(at.X, RestHeight, at.Y)));
            mm.SetInstanceCustomData(i, new Color(rng.Randf(), rng.RandfRange(0.6f, 1.4f), rng.RandfRange(1.6f, 2.8f), rng.Randf()));
        }
        root.AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = sparks, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 4f });

        // The light: filling the room, and spilling out of its mouth into the canyon.
        var inner = new OmniLight3D { LightColor = Gold, LightEnergy = 3f * bright, OmniRange = 12f, OmniAttenuation = 1.2f, ShadowEnabled = false, Position = new Vector3(at.X, LevelMap.SwimBand + 2.5f, at.Y) };
        root.AddChild(inner);
        var mouth = cave.Mouth + cave.Facing * 3f;
        var spill = new OmniLight3D { LightColor = Gold, LightEnergy = 2f * bright, OmniRange = 11f, OmniAttenuation = 1.4f, ShadowEnabled = false, Position = new Vector3(mouth.X, LevelMap.SwimBand + 3f, mouth.Y) };
        root.AddChild(spill);
        // A few motes drifting out of the mouth, catching the eye from the canyon.
        var moteMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(1f, 0.82f, 0.45f, 0.9f),
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            VertexColorUseAsAlbedo = true,
        };
        var fade = new Gradient();
        fade.SetColor(0, new Color(1f, 1f, 1f, 0f));
        fade.SetColor(1, new Color(1f, 1f, 1f, 0f));
        fade.AddPoint(0.2f, Colors.White);
        var drift = new CpuParticles3D
        {
            Position = new Vector3(cave.Mouth.X, LevelMap.SwimBand + 0.6f, cave.Mouth.Y),
            Amount = 26,
            Lifetime = 4.5f,
            Preprocess = 4.5f,
            Mesh = new QuadMesh { Size = new Vector2(0.09f, 0.09f), Material = moteMat },
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 1.6f,
            Direction = new Vector3(cave.Facing.X, 0.25f, cave.Facing.Y),
            Spread = 40f,
            Gravity = new Vector3(0f, 0.08f, 0f),
            InitialVelocityMin = 0.4f,
            InitialVelocityMax = 1f,
            DampingMin = 0.05f,
            DampingMax = 0.15f,
            ColorRamp = fade,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        root.AddChild(drift);
        return new Room { Root = root, Sparks = sparks, Inner = inner, Spill = spill, Drift = drift, At = at, Mouth = cave.Mouth + cave.Facing * 1.5f, Bright = bright };
    }

    public void Sync(PlaneWorld world, float dt)
    {
        foreach (var room in _rooms)
        {
            // Its pearl still waiting? Once taken (or being absorbed), the motes dissolve and the light settles.
            bool waiting = world.Pearls.Any(p => p.Position == room.At && !p.Taken && !p.Absorbing);
            room.Gone = Mathf.MoveToward(room.Gone, waiting ? 0f : 1f, dt / 1.2f);
            room.Sparks.SetShaderParameter("gone", room.Gone);
            float k = 1f - 0.65f * room.Gone;
            room.Inner.LightEnergy = 3f * room.Bright * k;
            room.Spill.LightEnergy = 2f * room.Bright * k;
            room.Drift.Emitting = room.Gone < 0.5f;
        }
        Glows.Clear();
        foreach (var room in _rooms)
        {
            float k = (1f - 0.65f * room.Gone) * room.Bright;
            Glows.Add(new Vector4(room.At.X, room.At.Y, 5.5f, 0.55f * k));
            Glows.Add(new Vector4(room.Mouth.X, room.Mouth.Y, 4.5f, 0.35f * k));
        }
    }
}
