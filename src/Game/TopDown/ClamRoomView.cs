using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Clam rooms (the treasure caves, DESIGN-TOPDOWN §4.2): the room is the inside of a giant clam. Its chamber is lined
/// with a shell wall of glowing mother-of-pearl (open at the mouth) and floored with it; the pearl hangs still in the
/// middle, and its light paints its pattern over all of it. Gold motes spiral round the pearl, its light in its colour
/// fills the room and spills out of the mouth into the canyon, with a few motes drifting out, so it can be found; it
/// shines brighter the deeper she is (the water is darker there). When the pearl is taken the motes fling out and
/// dissolve, and its light fades out.
/// </summary>
public partial class ClamRoomView : Node3D
{
    sealed class Room
    {
        public Node3D Root = null!;
        public ShaderMaterial Sparks = null!;
        /// <summary>The shell's material (wall and floor): the pearl's light in its pattern is painted through it.</summary>
        public ShaderMaterial Nacre = null!;
        public OmniLight3D Inner = null!, Spill = null!;
        public CpuParticles3D Drift = null!;
        public System.Numerics.Vector2 At, Mouth;
        public float Gone, Bright;
        /// <summary>The pearl's own colour (its light's mean), for the spill and the water's haze.</summary>
        public Color Tint = new(1f, 0.8f, 0.45f);
    }

    const int Motes = 80;
    /// <summary>Where the pearl hangs in the room: at the swim level.</summary>
    public const float RestHeight = LevelMap.SwimBand + 0.02f;

    readonly List<Room> _rooms = new();

    /// <summary>The rooms' glows for the water pass (xz, radius, strength): the room, and its mouth; and their colours.</summary>
    public List<Vector4> Glows { get; } = new();
    public List<Color> GlowColors { get; } = new();
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
        {
            var room = Build(map, poi, map.Caves[poi.Cave], bright);
            // Lit by the pearl that lies in it: its colours, in its pattern, painted over the whole room.
            var pearl = world.Pearls.FirstOrDefault(p => p.Position == poi.Position);
            if (pearl is not null && world.Run.Catalog?.TryGet(pearl.ItemId, out var item) == true)
            {
                room.Tint = OctoShoots.Game.Fx.PearlLight.Mean(item);
                room.Inner.LightColor = room.Tint;
                var look = item.Pearl ?? new OctoShoots.Core.Items.PearlLook { Colors = { "#ffffff" } };
                var m = room.Nacre;
                m.SetShaderParameter("glow_pos", new Vector3(poi.Position.X, RestHeight, poi.Position.Y));
                m.SetShaderParameter("glow_c0", Color.FromHtml(look.Colors[0]));
                m.SetShaderParameter("glow_c1", Color.FromHtml(look.Colors[System.Math.Min(1, look.Colors.Count - 1)]));
                m.SetShaderParameter("glow_c2", Color.FromHtml(look.Colors[^1]));
                m.SetShaderParameter("glow_pattern", (int)look.Pattern);
                m.SetShaderParameter("glow_seed", (float)(System.StringComparer.Ordinal.GetHashCode(item.Id) % 97));
                room.Spill.LightColor = room.Tint;
            }
            _rooms.Add(room);
        }
    }

    Room Build(LevelMap map, Poi poi, CaveSite cave, float bright)
    {
        var at = poi.Position;
        float floor = map.HeightAt(at);
        var root = new Node3D();
        AddChild(root);
        var nacre = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/clam_nacre.gdshader") };

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
        // The chamber's floor, mother-of-pearl as the wall is, laid over the rock from the middle to the wall's foot.
        root.AddChild(new MeshInstance3D
        {
            Mesh = ClamRoomMeshes.Floor(0f, chamber - 0.3f, p => map.HeightAt(new System.Numerics.Vector2(at.X + p.X, at.Y + p.Y)) - floor + 0.05f),
            MaterialOverride = nacre,
            Position = new Vector3(at.X, floor, at.Y),
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
        // The pearl's light: just over it, filling the room in its colour.
        var inner = new OmniLight3D { LightColor = Gold, LightEnergy = 1.2f * bright, OmniRange = 10f, OmniAttenuation = 0.7f, ShadowEnabled = false, Position = new Vector3(at.X, RestHeight + 1.4f, at.Y) };
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
        return new Room { Root = root, Sparks = sparks, Nacre = nacre, Inner = inner, Spill = spill, Drift = drift, At = at, Mouth = cave.Mouth + cave.Facing * 1.5f, Bright = bright };
    }

    public void Sync(PlaneWorld world, float dt)
    {
        foreach (var room in _rooms)
        {
            // Its pearl still waiting? Once taken (or being absorbed), the motes dissolve and its light fades out.
            bool waiting = world.Pearls.Any(p => p.Position == room.At && !p.Taken && !p.Absorbing);
            room.Gone = Mathf.MoveToward(room.Gone, waiting ? 0f : 1f, dt / 1.2f);
            room.Sparks.SetShaderParameter("gone", room.Gone);
            float k = 1f - room.Gone;
            room.Inner.LightEnergy = 1.2f * room.Bright * k;
            // The pearl's patterned light over the room, fading out once it is taken.
            room.Nacre.SetShaderParameter("glow_strength", 1.3f * room.Bright * k);
            room.Spill.LightEnergy = 2f * room.Bright * k;
            room.Inner.Visible = room.Spill.Visible = k > 0.001f;
            room.Drift.Emitting = room.Gone < 0.5f;
        }
        Glows.Clear();
        GlowColors.Clear();
        foreach (var room in _rooms)
        {
            float k = (1f - room.Gone) * room.Bright;
            Glows.Add(new Vector4(room.At.X, room.At.Y, 5.5f, 0.55f * k));
            Glows.Add(new Vector4(room.Mouth.X, room.Mouth.Y, 4.5f, 0.35f * k));
            GlowColors.Add(room.Tint);
            GlowColors.Add(room.Tint);
        }
    }
}
