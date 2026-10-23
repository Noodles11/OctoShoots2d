using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Sealed walls (DESIGN-TOPDOWN §4.2), presentation: the carving on each wall's face — an X, faint, on a secret's or a
/// tunnel's; a sword on the way into the boss's arena, its crimson smouldering — filling with light as her shots strike
/// it and flashing with each, draining if they stop. Filled, the wall slides down into the sand (LevelView.Slide): it
/// shudders as it gives, the carving sinking with it, silt pushed up and rolling out all along its feet where it meets the
/// seabed, chips of rock flicked out, a dull grinding under it; the screen shakes as long as it goes (Sliding).
/// </summary>
public partial class SealView : Node3D
{
    sealed class Emblem
    {
        public MeshInstance3D Mesh = null!;
        public ShaderMaterial Material = null!;
        public Vector3 At;
        public float Fill, Flash, Fade = 1f;
    }

    sealed class Slide
    {
        public Emblem? Emblem;
        public List<Vector3> Feet = new();
        public float Age, Height;
        public Vector3 Facing;
    }

    readonly List<Emblem> _emblems = new();
    readonly List<Slide> _slides = new();
    FxParticles _dust = null!, _silt = null!;
    LevelMap? _map;

    /// <summary>A wall is sliding down now (the screen shakes meanwhile).</summary>
    public bool Sliding => _slides.Count > 0;

    /// <summary>The boss's sword: crimson.</summary>
    static readonly Color Crimson = new(1f, 0.12f, 0.16f);

    public override void _Ready()
    {
        _dust = new FxParticles(512, additive: false);
        AddChild(_dust);
        _silt = new FxParticles(768, additive: false);
        AddChild(_silt);
    }

    public void Show(PlaneWorld world)
    {
        _map = world.Map;
        foreach (var e in _emblems) e.Mesh.QueueFree();
        _emblems.Clear();
        _slides.Clear();
        var shader = GD.Load<Shader>("res://assets/shaders/seal_emblem.gdshader");
        foreach (var seal in world.Seals)
        {
            // On the face it is met from, where it stands a little over her plane (in focus, where her shots strike it).
            var map = world.Map;
            float H(System.Numerics.Vector2 q) => LevelView.SealHeight(map, seal.Rock, q.X, q.Y);
            float y = LevelMap.SwimBand + 0.9f;
            var spot = seal.Rock.Center;
            for (float d = 0f; d <= seal.Rock.Thick + seal.Rock.Radius + 6f; d += 0.05f)
            {
                spot = seal.Rock.Center + seal.Facing * d;
                if (H(spot) <= y) break;
            }
            const float e = 0.3f;
            var normal = new Vector3(H(spot - new System.Numerics.Vector2(e, 0f)) - H(spot + new System.Numerics.Vector2(e, 0f)), 2f * e,
                H(spot - new System.Numerics.Vector2(0f, e)) - H(spot + new System.Numerics.Vector2(0f, e))).Normalized();
            var at = new Vector3(spot.X, H(spot), spot.Y);
            var material = new ShaderMaterial { Shader = shader };
            bool boss = seal.Kind == SealKind.Boss;
            material.SetShaderParameter(SN.Of("shape"), boss ? 1 : 0);
            if (boss)
            {
                material.SetShaderParameter(SN.Of("light_color"), Crimson);
                material.SetShaderParameter(SN.Of("ember"), 1f);
            }
            var mesh = new MeshInstance3D
            {
                Mesh = new QuadMesh { Size = boss ? new Vector2(3.4f, 3.4f) : new Vector2(2f, 2f) },
                MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(mesh);
            // Tipped up toward the camera above (the face is steep: seen from overhead a carving flat on it would be
            // edge-on), standing a little proud of the stone so the tilt does not sink it; its up up the wall.
            normal = (normal + Vector3.Up * 1.2f).Normalized();
            var side = normal.Cross(Vector3.Up).Normalized();
            if (side.LengthSquared() < 1e-4f) side = Vector3.Right;
            var upish = side.Cross(normal).Normalized();
            var where = at + normal * 0.35f;
            mesh.GlobalTransform = new Transform3D(new Basis(side, upish, normal), where);
            _emblems.Add(new Emblem { Mesh = mesh, Material = material, At = where, Fade = seal.Open ? 0f : 1f });
        }
    }

    public void Sync(PlaneWorld world, float dt, Camera3D? camera)
    {
        for (int i = 0; i < _emblems.Count && i < world.Seals.Count; i++)
        {
            var e = _emblems[i];
            var seal = world.Seals[i];
            // Fills quickly with each hit; drains slower, as the sim lets it go.
            e.Fill = Mathf.MoveToward(e.Fill, seal.Glow, dt * (seal.Glow > e.Fill ? 2.5f : 1f));
            e.Flash = Mathf.MoveToward(e.Flash, 0f, dt * 3f);
            e.Mesh.Visible = e.Fade > 0.001f;
            e.Material.SetShaderParameter(SN.Of("fill"), e.Fill);
            e.Material.SetShaderParameter(SN.Of("flash"), e.Flash);
            e.Material.SetShaderParameter(SN.Of("fade"), e.Fade);
        }
        for (int i = _slides.Count - 1; i >= 0; i--)
        {
            var s = _slides[i];
            s.Age += dt;
            float t = s.Age / LevelView.SlideSeconds;
            float down = LevelView.SlideAt(t);
            // The carving goes down with its wall, glowing out.
            if (s.Emblem is { } em)
            {
                em.Mesh.GlobalPosition = em.At + Vector3.Down * (s.Height + 0.3f) * down;
                em.Fade = 1f - Mathf.SmoothStep(0.4f, 0.9f, t);
            }
            // Silt pushed up along its feet as it goes into the sand, rolling out low and settling; chips of rock flicked off.
            if (s.Feet.Count > 0 && t > 0.06f && t < 1f)
            {
                float rate = 260f * (0.3f + Mathf.Sin(Mathf.Pi * Mathf.Clamp((t - 0.06f) / 0.94f, 0f, 1f)));
                float n = rate * dt;
                int count = (int)n + (_silt.RandRange(0f, 1f) < n - (int)n ? 1 : 0);
                for (int k = 0; k < count; k++)
                {
                    var foot = s.Feet[(int)_silt.RandRange(0f, s.Feet.Count - 0.001f)];
                    // Out from under the wall, low along the floor, then up as it slows (fine silt lifts in the water).
                    var away = new Vector3(_silt.RandRange(-1f, 1f), 0f, _silt.RandRange(-1f, 1f)).Normalized();
                    var v = away * _silt.RandRange(0.6f, 2f) + Vector3.Up * _silt.RandRange(0.3f, 0.9f);
                    float size = _silt.RandRange(0.25f, 0.5f);
                    _silt.Emit(foot + away * 0.2f, v, _silt.RandRange(1.2f, 2.2f), size, size * 2.6f,
                        new Color(0.6f, 0.62f, 0.55f, 0.5f), new Color(0.62f, 0.64f, 0.58f, 0f), drag: 1.8f, buoyancy: 0.35f);
                    if (_dust.RandRange(0f, 1f) < 0.18f)
                        _dust.Emit(foot + Vector3.Up * 0.2f, away * _dust.RandRange(1.5f, 3.2f) + Vector3.Up * _dust.RandRange(1.2f, 2.4f), _dust.RandRange(0.5f, 0.8f), _dust.RandRange(0.07f, 0.13f), 0.05f,
                            new Color(0.3f, 0.32f, 0.3f, 1f), new Color(0.32f, 0.34f, 0.3f, 0f), drag: 2.2f, buoyancy: -2.5f);
                }
            }
            if (s.Age > LevelView.SlideSeconds + 0.2f) _slides.RemoveAt(i);
        }
        if (camera is not null)
        {
            _dust.Tick(dt, camera.GlobalBasis);
            _silt.Tick(dt, camera.GlobalBasis);
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
            // The wall gives and slides down into the sand.
            int i = world.Seals.FindIndex(s => s.Rock.Center == e.Position);
            if (i < 0) return;
            var seal = world.Seals[i];
            _slides.Add(new Slide
            {
                Emblem = i < _emblems.Count ? _emblems[i] : null,
                Feet = LevelView.SealFoot(_map, seal.Rock),
                Height = LevelView.SealDrop(_map, seal.Rock),
                Facing = new Vector3(seal.Facing.X, 0f, seal.Facing.Y),
            });
        }
    }
}
