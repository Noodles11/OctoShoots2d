using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Current surges on screen (DESIGN-TOPDOWN §4.7): the moving water seen as she sees her own swells — strips of
/// denser water, each a long soft lens that magnifies what lies under it across its width (the picture bends most at
/// its middle, not at all at its edges, with a faint bright line where it bends hardest), racing down the canyon the
/// surge runs through, over its channel and spilling a little past its walls (where it is only seen, not felt). They
/// appear as the surge builds, race at full strength, and thin out as it eases off — the warning before it bites.
/// </summary>
public partial class CurrentView : Node3D
{
    const int Streaks = 70;
    const float StreakLength = 4f, StreakWidth = 0.7f;

    sealed class Streak
    {
        public float S, Side, Height, Life, Age, Pace, Size;
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
render_mode unshaded, blend_mix, depth_draw_never, depth_test_disabled, cull_disabled, shadows_disabled;
uniform sampler2D screen : hint_screen_texture, filter_linear_mipmap;
void fragment() {
	// A strip of denser water: a soft lens across its width. Its mask: nothing at its edges and its ends.
	float u = UV.x, v = UV.y * 2.0 - 1.0;
	float along = smoothstep(0.0, 0.3, u) * smoothstep(1.0, 0.7, u);
	float across = 1.0 - v * v;
	float mask = along * across * COLOR.a;
	// Across, in screen pixels: how far this pixel is from the strip's middle line, and which way that is.
	vec2 g = vec2(dFdx(UV.y), dFdy(UV.y));
	float gl = max(length(g), 1e-5);
	vec2 n = g / gl;
	float from_mid = (UV.y - 0.5) / gl;
	// Magnified: sampling nearer the middle line (up to 1.25x at the middle), so the picture under it swells.
	float mag = 0.2 * mask;
	vec2 px = 1.0 / VIEWPORT_SIZE;
	vec2 suv = SCREEN_UV - n * from_mid * mag * px;
	vec3 seen = textureLod(screen, suv, 0.0).rgb;
	// Where it bends hardest (part way to its edges), a faint bright line; the water a shade cooler inside it.
	float rim = smoothstep(0.35, 0.7, abs(v)) * (1.0 - smoothstep(0.7, 1.0, abs(v)));
	seen = mix(seen, seen * vec3(0.94, 1.0, 1.04), 0.6) + COLOR.rgb * rim * 0.06;
	ALBEDO = seen;
	ALPHA = clamp(mask * 1.6, 0.0, 1.0);
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
            var basis = new Basis(flat * StreakLength * st.Size * (0.6f + 0.6f * strength), Vector3.Up, new Vector3(-flat.Z, 0f, flat.X) * StreakWidth * st.Size);
            _mesh.SetInstanceTransform(i, new Transform3D(basis, new Vector3(p.X, LevelMap.SwimBand + st.Height, p.Y)));
            // Fades in and out over its own life; dimmer where the flow is only seen, past the canyon's walls.
            float life = Mathf.Sin(Mathf.Pi * Mathf.Clamp(st.Age / st.Life, 0f, 1f));
            float inside = Mathf.Abs(st.Side) <= reach - ReefDirectorTuning.CosmeticReach ? 1f : 0.45f;
            _mesh.SetInstanceColor(i, new Color(0.78f, 0.95f, 1f, strength * life * inside));
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
        st.Height = _rng.RandfRange(0f, 0.8f);
        st.Life = _rng.RandfRange(0.8f, 1.8f);
        st.Pace = _rng.RandfRange(1.4f, 2.2f);
        st.Size = _rng.RandfRange(0.6f, 1.4f);
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
