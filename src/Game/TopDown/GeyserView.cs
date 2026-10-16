using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Bubble geysers (PlaneGeysers.cs, DESIGN-TOPDOWN §4.7): a small dark vent in a pale rim of sand on the seabed,
/// trickling a bubble now and then. On the schedule it fizzes (a stream of small bubbles, the warning), then sprays: a
/// froth bursts out of the vent, a ring runs out over the floor with the push, and a column of bubbles rises all the
/// way to the surface — leaving fast, wobbling, swelling as they climb — until the spray dies down.
/// </summary>
public partial class GeyserView : Node3D
{
    /// <summary>Bubbles a second: idle, fizzing, spraying.</summary>
    const float Idle = 1.5f, Fizz = 26f, Spray = 170f;

    sealed class Vent
    {
        public System.Numerics.Vector2 At;
        public float Floor, Emit;
        public MeshInstance3D Ring = null!;
        public float RingAge = -1f;
    }

    readonly List<Vent> _vents = new();
    FxParticles _bubbles = null!, _froth = null!;
    StandardMaterial3D _ringMat = null!, _ventMat = null!;
    float _time;

    public override void _Ready()
    {
        _bubbles = new FxParticles(3000, additive: false, FxParticles.Bubble());
        AddChild(_bubbles);
        _froth = new FxParticles(600, additive: false);
        AddChild(_froth);
        _ringMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(0.85f, 0.97f, 1f, 0.5f),
        };
        _ventMat = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.9f };
    }

    /// <summary>A new level: its vents.</summary>
    public void Show(PlaneWorld world)
    {
        foreach (var v in _vents) v.Ring.GetParent()?.QueueFree();
        _vents.Clear();
        _bubbles.Clear();
        _froth.Clear();
        foreach (var at in world.Director.Geysers)
        {
            float floor = Mathf.Min(world.Map.HeightAt(at), LevelMap.BlockHeight);
            var root = new Node3D { Position = new Vector3(at.X, floor, at.Y) };
            AddChild(root);
            // The vent: a low cone of pale sand round a dark hole.
            var cone = BossMeshes.Lathe(t =>
            {
                float r = Mathf.Lerp(0.18f, 0.9f, t);
                float h = 0.12f * Mathf.Sin(Mathf.Clamp(t * 1.6f, 0f, 1f) * Mathf.Pi) - (t < 0.12f ? 0.15f : 0f);
                return new Vector2(r, h);
            }, 10, 24, (p, u, v) => v < 0.15f ? new Color(0.06f, 0.07f, 0.08f) : new Color(0.72f, 0.68f, 0.58f).Lerp(new Color(0.55f, 0.52f, 0.45f), v));
            root.AddChild(new MeshInstance3D { Mesh = cone, MaterialOverride = _ventMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            var ring = new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = 0.9f, OuterRadius = 1f, Rings = 48, RingSegments = 4 },
                MaterialOverride = _ringMat, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(0f, 0.15f, 0f),
            };
            root.AddChild(ring);
            _vents.Add(new Vent { At = at, Floor = floor, Ring = ring });
        }
    }

    public void Sync(PlaneWorld world, float dt, Camera3D? camera)
    {
        _time += dt;
        for (int i = 0; i < _vents.Count; i++)
        {
            var vent = _vents[i];
            // What this vent is doing: idle, fizzing (the warning) or spraying (how hard).
            float spray = 0f;
            bool fizz = false;
            foreach (var e in world.Director.Active)
            {
                if (e.Kind != ReefEventKind.GeyserSpray || e.Geyser != i) continue;
                float k = Geysers.Envelope(e, world.Time, out float t);
                if (t < 0f) fizz = true;
                spray = Mathf.Max(spray, k);
            }
            foreach (var ev in world.Events)
                if (ev.Type == PlaneEventType.GeyserSpray && System.Numerics.Vector2.DistanceSquared(ev.Position, vent.At) < 0.01f) Burst(vent);
            float rate = spray > 0f ? Spray * spray : fizz ? Fizz : Idle;
            vent.Emit += rate * dt;
            while (vent.Emit >= 1f)
            {
                vent.Emit -= 1f;
                Bubble(vent, spray, fizz);
            }
            // The push's ring running out over the floor.
            if (vent.RingAge >= 0f)
            {
                vent.RingAge += dt;
                float k = vent.RingAge / 0.6f;
                vent.Ring.Visible = k < 1f;
                vent.Ring.Scale = new Vector3(0.6f + GeyserTuning.Reach * k, 0.4f, 0.6f + GeyserTuning.Reach * k);
                _ringMat.AlbedoColor = new Color(0.85f, 0.97f, 1f, 0.55f * (1f - k));
                if (k >= 1f) vent.RingAge = -1f;
            }
        }
        if (camera is not null)
        {
            _bubbles.Tick(dt, camera.GlobalBasis);
            _froth.Tick(dt, camera.GlobalBasis);
        }
    }

    /// <summary>One bubble out of the vent: a spray's leave fast and big, a fizz's small and slow, an idle one now and then.</summary>
    void Bubble(Vent vent, float spray, bool fizz)
    {
        var from = new Vector3(vent.At.X, vent.Floor + 0.1f, vent.At.Y) + new Vector3(_bubbles.RandRange(-0.15f, 0.15f), 0f, _bubbles.RandRange(-0.15f, 0.15f));
        float up = spray > 0f ? _bubbles.RandRange(3.5f, 6f) : fizz ? _bubbles.RandRange(1.2f, 2.2f) : _bubbles.RandRange(0.8f, 1.4f);
        float side = spray > 0f ? 1.1f : 0.25f;
        var v = new Vector3(_bubbles.RandRange(-side, side), up, _bubbles.RandRange(-side, side));
        float size = spray > 0f ? _bubbles.RandRange(0.12f, 0.3f) : _bubbles.RandRange(0.05f, 0.11f);
        // They rise all the way up: buoyancy keeps them climbing, the water's drag steadies them, and they swell as the
        // pressure falls (to the surface about 18 m up).
        var c0 = new Color(0.95f, 1f, 1f, 1f);
        BubbleFx.Rise(_bubbles, from, v, size, c0);
    }

    /// <summary>The spray begins: a froth bursts out of the vent and the push's ring sets off.</summary>
    void Burst(Vent vent)
    {
        var at = new Vector3(vent.At.X, vent.Floor + 0.2f, vent.At.Y);
        _froth.Burst(at, Vector3.Up, 40, 3.5f, 0.8f, 0.7f, 0.25f, 0.5f, new Color(0.92f, 0.98f, 1f, 0.7f), new Color(0.85f, 0.95f, 1f, 0f), drag: 2.5f, buoyancy: 1.5f);
        vent.RingAge = 0f;
    }
}

/// <summary>Real bubbles rising through the water, shared by the geysers and her light where it meets the seabed.</summary>
public static class BubbleFx
{
    /// <summary>The surface they rise to (m, world).</summary>
    public const float Surface = 18f;

    /// <summary>One bubble from a point with an initial velocity: it climbs to the surface, swelling as it goes.</summary>
    public static void Rise(FxParticles fx, Vector3 from, Vector3 velocity, float size, Color color)
    {
        float climb = Mathf.Max(Surface - from.Y, 2f);
        // Long enough to reach the surface at about its rising speed (buoyancy keeps it accelerating gently).
        float life = Mathf.Clamp(climb / Mathf.Max(velocity.Y, 0.8f) * 0.85f, 1.5f, 9f);
        fx.Emit(from, velocity, life, size, size * 2.4f, color, color with { A = 0f }, drag: 0.35f, buoyancy: 0.9f);
    }
}
