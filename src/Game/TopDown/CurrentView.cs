using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Current surges on screen (DESIGN-TOPDOWN §4.7): pale streaks of moving water rushing down the canyon the surge runs
/// through, over its channel and spilling a little past its walls (where it is only seen, not felt). They appear as
/// the surge builds, race at full strength, and thin out as it eases off — the warning before it bites.
/// </summary>
public partial class CurrentView : Node3D
{
    const int Streaks = 240;
    const float StreakLength = 1.6f, StreakWidth = 0.07f;

    sealed class Streak
    {
        public float S, Side, Height, Life, Age, Pace;
    }

    MultiMesh _mesh = null!;
    readonly Streak[] _streaks = new Streak[Streaks];
    readonly RandomNumberGenerator _rng = new();
    ReefEvent? _surge;
    /// <summary>The canyon's course, and the distance along it to each of its points.</summary>
    List<System.Numerics.Vector2> _course = new();
    readonly List<float> _at = new();

    public override void _Ready()
    {
        var shader = new Shader
        {
            Code = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;
void fragment() {
	// Bright in the middle of its length, fading to nothing at both ends and at the edges.
	float along = sin(UV.x * 3.14159);
	float across = 1.0 - abs(UV.y * 2.0 - 1.0);
	ALBEDO = COLOR.rgb;
	ALPHA = COLOR.a * along * along * across;
}",
        };
        _mesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            InstanceCount = Streaks,
            VisibleInstanceCount = 0,
            Mesh = new QuadMesh { Size = new Vector2(1f, 1f), Orientation = PlaneMesh.OrientationEnum.Y },
        };
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = _mesh,
            MaterialOverride = new ShaderMaterial { Shader = shader },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = 200f,
        });
        for (int i = 0; i < Streaks; i++) _streaks[i] = new Streak();
    }

    /// <summary>A new level: no surge drawn.</summary>
    public void Clear()
    {
        _surge = null;
        _mesh.VisibleInstanceCount = 0;
    }

    public void Sync(PlaneWorld world, float dt)
    {
        ReefEvent? surge = null;
        foreach (var e in world.Director.Active)
            if (e.Kind == ReefEventKind.CurrentSurge) surge = e;
        if (surge != _surge) Begin(world, surge);
        if (_surge is null) return;

        float strength = _surge.Envelope(world.Time);
        float total = _at[^1];
        float reach = world.Map.Corridors[_surge.Corridor].HalfWidth + ReefDirectorTuning.CosmeticReach;
        for (int i = 0; i < Streaks; i++)
        {
            var st = _streaks[i];
            st.Age += dt;
            // The water runs faster than what it carries; streaks race on ahead.
            st.S += (_surge.Reverse ? -1f : 1f) * _surge.Speed * strength * st.Pace * dt;
            if (st.Age >= st.Life || st.S < 0f || st.S > total) Respawn(st, reach, total);
            var (at, dir) = Sample(st.S);
            var side = new System.Numerics.Vector2(-dir.Y, dir.X);
            var p = at + side * st.Side;
            var flat = new Vector3(dir.X, 0f, dir.Y) * (_surge.Reverse ? -1f : 1f);
            var basis = new Basis(flat * StreakLength * (0.6f + 0.6f * strength), Vector3.Up, new Vector3(-flat.Z, 0f, flat.X) * StreakWidth);
            _mesh.SetInstanceTransform(i, new Transform3D(basis, new Vector3(p.X, LevelMap.SwimBand + st.Height, p.Y)));
            // Fades in and out over its own life; dimmer where the flow is only seen, past the canyon's walls.
            float life = Mathf.Sin(Mathf.Pi * Mathf.Clamp(st.Age / st.Life, 0f, 1f));
            float inside = Mathf.Abs(st.Side) <= reach - ReefDirectorTuning.CosmeticReach ? 1f : 0.45f;
            _mesh.SetInstanceColor(i, new Color(0.78f, 0.95f, 1f, 0.42f * strength * life * inside));
        }
    }

    void Begin(PlaneWorld world, ReefEvent? surge)
    {
        _surge = surge;
        _mesh.VisibleInstanceCount = surge is null ? 0 : Streaks;
        if (surge is null) return;
        _course = world.Map.Corridors[surge.Corridor].Points;
        _at.Clear();
        float sum = 0f;
        for (int i = 0; i < _course.Count; i++)
        {
            if (i > 0) sum += System.Numerics.Vector2.Distance(_course[i - 1], _course[i]);
            _at.Add(sum);
        }
        float reach = world.Map.Corridors[surge.Corridor].HalfWidth + ReefDirectorTuning.CosmeticReach;
        foreach (var st in _streaks)
        {
            Respawn(st, reach, sum);
            st.Age = _rng.RandfRange(0f, st.Life);
        }
    }

    void Respawn(Streak st, float reach, float total)
    {
        st.S = _rng.RandfRange(0f, total);
        st.Side = _rng.RandfRange(-reach, reach);
        st.Height = _rng.RandfRange(-0.6f, 1.6f);
        st.Life = _rng.RandfRange(0.8f, 1.8f);
        st.Pace = _rng.RandfRange(1.4f, 2.2f);
        st.Age = 0f;
    }

    /// <summary>The point at distance s along the course, and the course's direction there.</summary>
    (System.Numerics.Vector2 At, System.Numerics.Vector2 Dir) Sample(float s)
    {
        for (int i = 1; i < _course.Count; i++)
        {
            if (s > _at[i] && i < _course.Count - 1) continue;
            var a = _course[i - 1];
            var b = _course[i];
            float len = _at[i] - _at[i - 1];
            float k = len > 1e-4f ? Mathf.Clamp((s - _at[i - 1]) / len, 0f, 1f) : 0f;
            var d = b - a;
            return (a + d * k, d.LengthSquared() > 1e-8f ? System.Numerics.Vector2.Normalize(d) : System.Numerics.Vector2.UnitX);
        }
        return (_course[0], System.Numerics.Vector2.UnitX);
    }
}
