using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Clam rooms (the treasure caves, DESIGN-TOPDOWN §4.2): a chamber like any other, and in its middle a giant clam lying
/// open on the sand — three times the pearl's width — its mouth turned to the chamber's entrance, the pearl lying on its
/// lower valve. The pearl's light, in its colours and its pattern, falls out of the clam's mouth only: a cone of 120°
/// toward the entrance over the floor and the walls' foot (pearl_floor_light), a spotlight of its colour the same way,
/// the shell keeping all of it off what lies behind; and it spills out of the chamber's mouth into the canyon, so the
/// room can be found (brighter the deeper she is). Magic particles float up round the clam. When the pearl is taken the
/// particles stop, the clam closes and its light fades out.
/// </summary>
public partial class ClamRoomView : Node3D
{
    sealed class Room
    {
        public Node3D Root = null!, Hinge = null!;
        /// <summary>The pearl's light laid over the chamber (in its pattern, in the cone).</summary>
        public ShaderMaterial Floor = null!;
        public SpotLight3D Inner = null!;
        public OmniLight3D Spill = null!;
        public System.Numerics.Vector2 At, Mouth, Out;
        public Vector3 Facing;
        public float Gone, Bright, Closed, Magic;
        /// <summary>The pearl's own colour (its light's mean), for the spill and the water's haze.</summary>
        public Color Tint = new(1f, 0.8f, 0.45f);
    }

    /// <summary>The room's spotlight (out of the clam's mouth) and the light spilling out of the chamber's mouth, at Depth 1 (brighter deeper).</summary>
    const float InnerEnergy = 1.2f, SpillEnergy = 2.8f;
    /// <summary>Where the pearl lies in the room: at the swim level, on the clam's lower valve.</summary>
    public const float RestHeight = LevelMap.SwimBand + 0.02f;
    /// <summary>The clam: half its width (three times the pearl's), and how far its upper valve stands open (degrees).</summary>
    const float ClamHalf = 1.26f, OpenDeg = 62f;
    /// <summary>The pearl's radius as drawn (CombatView), for where the lower valve's lip lies under it.</summary>
    const float PearlRadius = 0.42f;

    readonly List<Room> _rooms = new();
    MagicParticles _magic = null!;

    /// <summary>The rooms' glows for the water pass (xz, radius, strength): the room, its mouth and the path out of it; and their colours.</summary>
    public List<Vector4> Glows { get; } = new();
    public List<Color> GlowColors { get; } = new();
    static readonly Color Gold = new(1f, 0.8f, 0.45f);

    public override void _Ready()
    {
        _magic = new MagicParticles(384);
        AddChild(_magic);
    }

    /// <summary>A new level: its clam rooms.</summary>
    public void Show(PlaneWorld world)
    {
        foreach (var r in _rooms) r.Root.QueueFree();
        _rooms.Clear();
        _magic.Clear();
        var map = world.Map;
        // Brighter the deeper she goes: the rooms are beacons in the dark.
        float bright = Mathf.Clamp(0.6f + 0.3f * (map.Depth - 1), 0.6f, 2.2f);
        foreach (var poi in map.Pois.Where(p => p.Kind == PoiKind.TreasureCave && p.Cave >= 0))
        {
            var room = Build(map, poi, map.Caves[poi.Cave], bright);
            // Lit by the pearl that lies in it: its colours.
            var pearl = world.Pearls.FirstOrDefault(p => p.Position == poi.Position);
            if (pearl is not null && world.Run.Catalog?.TryGet(pearl.ItemId, out var item) == true)
            {
                room.Tint = OctoShoots.Game.Fx.PearlLight.Mean(item);
                room.Inner.LightColor = room.Tint;
                var look = item.Pearl ?? new OctoShoots.Core.Items.PearlLook { Colors = { "#ffffff" } };
                var m = room.Floor;
                m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("glow_c0"), Color.FromHtml(look.Colors[0]));
                m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("glow_c1"), Color.FromHtml(look.Colors[System.Math.Min(1, look.Colors.Count - 1)]));
                m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("glow_c2"), Color.FromHtml(look.Colors[^1]));
                m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("glow_seed"), (float)(System.StringComparer.Ordinal.GetHashCode(item.Id) % 97));
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
        float chamber = cave.Chambers[0].Radius;
        var facing = new Vector3(cave.Facing.X, 0f, cave.Facing.Y).Normalized();

        // The clam: its mouth to the entrance (its local +X), the hinge behind; the lower valve a bowl under the pearl,
        // the upper standing open on the hinge.
        var clam = new Node3D { Position = new Vector3(at.X, 0f, at.Y), Rotation = new Vector3(0f, -Mathf.Atan2(facing.Z, facing.X), 0f) };
        root.AddChild(clam);
        var shell = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/clam_shell.gdshader") };
        shell.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("ribs"), 8f);
        // A healthy giant clam: bone-cream, a little darker in its grooves.
        shell.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("color_a"), new Color(0.9f, 0.86f, 0.78f));
        shell.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("color_b"), new Color(0.7f, 0.63f, 0.56f));
        float lip = RestHeight - PearlRadius * 0.9f;
        float depth = Mathf.Clamp(lip - floor, 0.18f, 1.2f);
        float rz = ClamHalf * 0.88f;
        var dome = new SphereMesh { Radius = 1f, Height = 1f, IsHemisphere = true, RadialSegments = 40, Rings = 12 };
        var lower = new MeshInstance3D { Mesh = dome, MaterialOverride = shell, Scale = new Vector3(ClamHalf, depth, rz), Position = new Vector3(0f, lip, 0f), RotationDegrees = new Vector3(180f, 0f, 0f) };
        lower.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("drained"), 0f);
        clam.AddChild(lower);
        // Its flesh inside, soft and clean, under the pearl.
        clam.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 1f, BottomRadius = 1f, Height = 0.04f, RadialSegments = 40 },
            Scale = new Vector3(ClamHalf * 0.92f, 1f, rz * 0.92f),
            Position = new Vector3(0f, lip - 0.03f, 0f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.55f, 0.6f), Roughness = 0.35f },
        });
        var hinge = new Node3D { Position = new Vector3(-ClamHalf, lip, 0f) };
        clam.AddChild(hinge);
        var upper = new MeshInstance3D { Mesh = dome, MaterialOverride = shell, Scale = new Vector3(ClamHalf, 0.55f, rz), Position = new Vector3(ClamHalf, 0f, 0f) };
        upper.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("drained"), 0f);
        hinge.AddChild(upper);

        // The pearl's light laid over the chamber, in its pattern, only in the cone out of the clam's mouth.
        var light = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/pearl_floor_light.gdshader"), RenderPriority = 1 };
        light.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("glow_pos"), new Vector3(at.X, RestHeight, at.Y));
        light.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("facing"), new Vector2(facing.X, facing.Z));
        // Reaching well out of the chamber's mouth: the light floods out and fades away before the patch ends.
        float reach = chamber + 9f;
        light.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("reach"), reach);
        root.AddChild(new MeshInstance3D { Mesh = FloorPatch(map, at, reach), MaterialOverride = light, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });

        // A spotlight of its colour out of the clam's mouth, toward the entrance, down a little: shadowed, so the clam
        // and whatever stands in the light throw shadows.
        var inner = new SpotLight3D
        {
            LightColor = Gold, LightEnergy = InnerEnergy * bright, SpotRange = chamber + 6f, SpotAngle = 60f, SpotAngleAttenuation = 1.4f,
            ShadowEnabled = OctoShoots.Game.Settings.ViewOptions.Shadows, ShadowBias = 0.08f, ShadowBlur = 1.5f,
            Position = new Vector3(at.X, RestHeight + 0.2f, at.Y),
        };
        root.AddChild(inner);
        inner.LookAt(inner.Position + facing + Vector3.Down * 0.35f, Vector3.Up);
        // The spill reaches well out over the path, so the room shows from the canyon; it stands out there.
        var mouth = cave.Mouth + cave.Facing * 7f;
        var spill = new OmniLight3D { LightColor = Gold, LightEnergy = SpillEnergy * bright, OmniRange = 17f, OmniAttenuation = 1.1f, ShadowEnabled = false, Position = new Vector3(mouth.X, LevelMap.SwimBand + 3f, mouth.Y) };
        root.AddChild(spill);
        return new Room
        {
            Root = root, Hinge = hinge, Floor = light, Inner = inner, Spill = spill, At = at, Facing = facing,
            Mouth = cave.Mouth + cave.Facing * 3f, Out = cave.Mouth + cave.Facing * 7f, Bright = bright,
        };
    }

    /// <summary>A patch of the ground's own surface round a point (this far), lifted a hair off it, for light laid on it.</summary>
    static ArrayMesh FloorPatch(LevelMap map, System.Numerics.Vector2 at, float radius)
    {
        const int n = 64;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int y = 0; y <= n; y++)
            for (int x = 0; x <= n; x++)
            {
                var q = at + new System.Numerics.Vector2((x / (float)n * 2f - 1f) * radius, (y / (float)n * 2f - 1f) * radius);
                st.AddVertex(new Vector3(q.X, map.HeightAt(q) + 0.04f, q.Y));
            }
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int a = y * (n + 1) + x, b = a + 1, c = a + n + 1, d = c + 1;
                st.AddIndex(a); st.AddIndex(c); st.AddIndex(b);
                st.AddIndex(b); st.AddIndex(c); st.AddIndex(d);
            }
        return st.Commit();
    }

    public void Sync(PlaneWorld world, float dt)
    {
        foreach (var room in _rooms)
        {
            // Its pearl still waiting? Once taken (or being absorbed), the particles stop, the clam closes and its light fades out.
            bool waiting = world.Pearls.Any(p => p.Position == room.At && !p.Taken && !p.Absorbing);
            room.Gone = Mathf.MoveToward(room.Gone, waiting ? 0f : 1f, dt / 1.2f);
            room.Closed = Mathf.MoveToward(room.Closed, waiting ? 0f : 1f, dt / 0.6f);
            float shut = room.Closed * room.Closed * (3f - 2f * room.Closed);
            room.Hinge.Rotation = new Vector3(0f, 0f, Mathf.DegToRad(OpenDeg) * (1f - shut));
            float k = 1f - room.Gone;
            room.Inner.LightEnergy = InnerEnergy * room.Bright * k;
            room.Floor.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("glow_strength"), 1.3f * Mathf.Min(room.Bright, 1.2f) * k);
            room.Spill.LightEnergy = SpillEnergy * room.Bright * k;
            room.Inner.Visible = room.Spill.Visible = k > 0.001f;
            // Magic particles floating up round the clam while its pearl waits.
            if (!waiting) continue;
            room.Magic += dt * 7f;
            while (room.Magic >= 1f)
            {
                room.Magic -= 1f;
                float a = _magic.RandRange(0f, Mathf.Tau), d = _magic.RandRange(0.4f, 2.2f);
                var from = new Vector3(room.At.X + Mathf.Cos(a) * d, RestHeight - 0.3f + _magic.RandRange(0f, 0.4f), room.At.Y + Mathf.Sin(a) * d);
                var drift = new Vector3(_magic.RandRange(-0.12f, 0.12f), _magic.RandRange(0.35f, 0.75f), _magic.RandRange(-0.12f, 0.12f));
                _magic.Emit(from, drift, _magic.RandRange(1.6f, 2.6f), _magic.RandRange(0.1f, 0.18f), drag: 0.2f);
            }
        }
        if (GetViewport().GetCamera3D() is { } camera) _magic.Tick(dt, camera.GlobalBasis);
        Glows.Clear();
        GlowColors.Clear();
        foreach (var room in _rooms)
        {
            float k = (1f - room.Gone) * room.Bright;
            // Faint over the room itself; its light pours out of the mouth into the canyon: a haze over the mouth, and a
            // wider one out on the path.
            Glows.Add(new Vector4(room.At.X, room.At.Y, 5.5f, 0.18f * k));
            Glows.Add(new Vector4(room.Mouth.X, room.Mouth.Y, 5f, 0.6f * k));
            Glows.Add(new Vector4(room.Out.X, room.Out.Y, 9f, 0.32f * k));
            GlowColors.Add(room.Tint);
            GlowColors.Add(room.Tint);
            GlowColors.Add(room.Tint);
        }
    }
}
