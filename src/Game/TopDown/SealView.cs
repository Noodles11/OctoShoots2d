using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Secret rooms' seals (DESIGN-TOPDOWN §4.2), presentation: the X carved faintly on each seal's canyon face, filling with
/// her light as her shots strike it and flashing with each; when it is full, the wall crumbles (LevelView sinks its
/// stretch of wall into the floor) in a burst of rubble and silt, and the emblem fades with it.
/// </summary>
public partial class SealView : Node3D
{
    sealed class Emblem
    {
        public MeshInstance3D Mesh = null!;
        public ShaderMaterial Material = null!;
        public float Fill, Flash, Fade = 1f;
        public bool Falling;
    }

    readonly List<Emblem> _emblems = new();
    FxParticles _dust = null!;
    LevelMap? _map;

    public override void _Ready()
    {
        _dust = new FxParticles(256, additive: false);
        AddChild(_dust);
    }

    public void Show(PlaneWorld world)
    {
        _map = world.Map;
        foreach (var e in _emblems) e.Mesh.QueueFree();
        _emblems.Clear();
        var shader = GD.Load<Shader>("res://assets/shaders/seal_emblem.gdshader");
        foreach (var seal in world.Seals)
        {
            // On the canyon face where it stands a little over her plane (in focus, where her shots strike it).
            var map = world.Map;
            float H(System.Numerics.Vector2 q) => LevelView.SealHeight(map, seal.Rock, q.X, q.Y);
            float y = LevelMap.SwimBand + 0.6f;
            var spot = seal.Rock.Center;
            for (float d = 0f; d <= seal.Rock.Radius + 6f; d += 0.05f)
            {
                spot = seal.Rock.Center + seal.Facing * d;
                if (H(spot) <= y) break;
            }
            const float e = 0.3f;
            var normal = new Vector3(H(spot - new System.Numerics.Vector2(e, 0f)) - H(spot + new System.Numerics.Vector2(e, 0f)), 2f * e,
                H(spot - new System.Numerics.Vector2(0f, e)) - H(spot + new System.Numerics.Vector2(0f, e))).Normalized();
            var at = new Vector3(spot.X, y, spot.Y);
            var material = new ShaderMaterial { Shader = shader };
            var mesh = new MeshInstance3D
            {
                Mesh = new QuadMesh { Size = new Vector2(2f, 2f) },
                MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(mesh);
            // Tipped up toward the camera above (the face is steep: seen from overhead a carving flat on it would be
            // edge-on), standing a little proud of the stone so the tilt does not sink it.
            normal = (normal + Vector3.Up * 1.2f).Normalized();
            // The quad's face (+Z) along that, its up toward the cave.
            var side = normal.Cross(Vector3.Up).Normalized();
            if (side.LengthSquared() < 1e-4f) side = Vector3.Right;
            var upish = side.Cross(normal).Normalized();
            mesh.GlobalTransform = new Transform3D(new Basis(side, upish, normal), at + normal * 0.35f);
            _emblems.Add(new Emblem { Mesh = mesh, Material = material, Falling = seal.Open, Fade = seal.Open ? 0f : 1f });
        }
    }

    public void Sync(PlaneWorld world, float dt)
    {
        for (int i = 0; i < _emblems.Count && i < world.Seals.Count; i++)
        {
            var e = _emblems[i];
            var seal = world.Seals[i];
            e.Fill = Mathf.MoveToward(e.Fill, seal.Glow, dt * 2.5f);
            e.Flash = Mathf.MoveToward(e.Flash, 0f, dt * 3f);
            if (seal.Open) e.Fade = Mathf.MoveToward(e.Fade, 0f, dt / 1.2f);
            e.Mesh.Visible = e.Fade > 0.001f;
            e.Material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("fill"), e.Fill);
            e.Material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("flash"), e.Flash);
            e.Material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("fade"), e.Fade);
        }
    }

    public void OnEvent(PlaneWorld world, PlaneEvent e)
    {
        if (_map is null) return;
        if (e.Type == PlaneEventType.SealLit)
        {
            int i = world.Seals.FindIndex(s => s.Emblem == e.Position);
            if (i >= 0 && i < _emblems.Count) _emblems[i].Flash = 1f;
        }
        else if (e.Type == PlaneEventType.SealOpened)
        {
            // The wall comes down: chunks of rock flung out and falling, and a cloud of silt rolling out of the way in.
            int i = world.Seals.FindIndex(s => s.Rock.Center == e.Position);
            if (i < 0) return;
            var seal = world.Seals[i];
            float height = LevelView.SealTop(_map, seal.Rock, seal.Facing);
            var top = new Vector3(e.Position.X, height * 0.8f, e.Position.Y);
            var outward = new Vector3(e.Direction.X, 0.5f, e.Direction.Y).Normalized();
            _dust.Burst(top, Vector3.Up, 50, 4f, 1f, 1.4f, 0.35f, 0.08f, new Color(0.32f, 0.36f, 0.34f, 1f), new Color(0.4f, 0.42f, 0.4f, 0f), drag: 2f);
            _dust.Burst(top, outward, 40, 6f, 0.7f, 1.1f, 0.22f, 0.06f, new Color(0.26f, 0.3f, 0.28f, 1f), new Color(0.3f, 0.32f, 0.3f, 0f), drag: 2.5f);
            var floor = new Vector3(e.Position.X, _map.HeightAt(e.Position) + 0.4f, e.Position.Y);
            _dust.Burst(floor, outward, 70, 3f, 1f, 2.2f, 0.6f, 1.6f, new Color(0.62f, 0.66f, 0.6f, 0.45f), new Color(0.62f, 0.66f, 0.6f, 0f), drag: 1.5f, buoyancy: 0.2f);
        }
    }
}
