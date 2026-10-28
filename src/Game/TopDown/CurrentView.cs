using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Current surges on screen (DESIGN-TOPDOWN §4.7): the moving water seen as a shimmer, like the air over a hot road
/// seen from afar. One ribbon of water laid along the canyon the surge runs through, over its channel and spilling
/// past its walls (where it is only seen, not felt): the picture under it wavers, bent by eddies of denser water that
/// roll down the canyon with the flow and boil as they go. No lines, no edges: patchy, softer past the walls, fading
/// out at its sides and ends. It wakes as the surge builds — the warning before it bites — runs faster and shimmers
/// harder at full strength, and stills as it eases off.
/// </summary>
public partial class CurrentView : Node3D
{
    /// <summary>How high over her plane the shimmer lies, and how far the flow it shows runs ahead of the water's own pace.</summary>
    const float Height = 0.4f, Pace = 1.6f;

    MeshInstance3D _ribbon = null!;
    ShaderMaterial _material = null!;
    ReefEvent? _surge;
    /// <summary>How far the shimmer has rolled down the canyon (metres, signed along its course).</summary>
    float _flow;

    public override void _Ready()
    {
        var shader = new Shader
        {
            Code = @"shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, depth_test_disabled, cull_disabled, shadows_disabled;
uniform sampler2D screen : hint_screen_texture, filter_linear_mipmap;
// The surge's strength now (0–1), how far its water has rolled along the course (m), the course's length (m), the
// ribbon's half width (m) and the share of it that is the canyon's own channel (where it is felt).
uniform float strength = 0.0;
uniform float flow = 0.0;
uniform float total = 1.0;
uniform float half_width = 1.0;
uniform float channel = 0.6;

float hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

void fragment() {
	// Metres along the course and across it (0 its middle line).
	vec2 m = vec2(UV.x, (UV.y * 2.0 - 1.0) * half_width);
	float across = abs(m.y) / half_width;
	// The water, carried along: eddies rolling down the canyon, boiling as they go (each scale its own pace).
	vec2 q = vec2(m.x - flow, m.y);
	float t = TIME;
	float big = noise(q * vec2(0.32, 0.42) + vec2(0.0, t * 0.22));
	float a = noise(q * vec2(0.9, 1.25) + vec2(big * 1.5, t * 0.55) + 3.7);
	float b = noise(q * vec2(1.05, 0.85) + vec2(-t * 0.45, big * 1.5) + 19.1);
	float fine = noise(q * vec2(2.6, 3.1) + vec2(t * 0.9, -t * 0.7) + 41.3);
	// Fine shivers riding the slow swirls: the picture wavers, it never slides.
	vec2 bend = vec2(a - 0.5, b - 0.5) + (fine - 0.5) * vec2(0.55, -0.55);
	// Where it shows: nothing at its ends and sides, weaker past the canyon's walls, patchy, as if the water came in
	// pulses.
	float ends = smoothstep(0.0, 5.0, m.x) * smoothstep(total, total - 5.0, m.x);
	float sides = 1.0 - smoothstep(0.45, 1.0, across);
	float walls = mix(0.45, 1.0, 1.0 - smoothstep(channel - 0.08, channel + 0.12, across));
	float patch = mix(0.35, 1.0, smoothstep(0.25, 0.7, big));
	float mask = ends * sides * walls * patch * strength;
	// The bend in metres on her plane, turned to screen pixels through the ribbon's own screen footprint.
	vec2 dx = vec2(dFdx(m.x), dFdx(m.y)), dy = vec2(dFdy(m.x), dFdy(m.y));
	float det = dx.x * dy.y - dx.y * dy.x;
	vec2 offset_m = bend * 0.5 * mask;
	vec2 px = abs(det) > 1e-9 ? vec2(dy.y * offset_m.x - dy.x * offset_m.y, -dx.y * offset_m.x + dx.x * offset_m.y) / det : vec2(0.0);
	px = clamp(px, vec2(-12.0), vec2(12.0));
	vec3 seen = textureLod(screen, SCREEN_UV + px / VIEWPORT_SIZE, 0.0).rgb;
	// Denser water a shade brighter, thinner a shade darker: barely, so it reads as the picture moving, not as paint.
	seen *= 1.0 + (fine - 0.5) * 0.06 * mask;
	ALBEDO = seen;
	ALPHA = clamp(mask * 2.5, 0.0, 1.0);
}",
        };
        _material = new ShaderMaterial { Shader = shader };
        _ribbon = new MeshInstance3D
        {
            MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = 200f,
            Visible = false,
        };
        AddChild(_ribbon);
    }

    /// <summary>A new level: no surge drawn.</summary>
    public void Clear()
    {
        _surge = null;
        _ribbon.Visible = false;
    }

    public void Sync(PlaneWorld world, float dt)
    {
        ReefEvent? surge = null;
        foreach (var e in world.Director.Active)
            if (e.Kind == ReefEventKind.CurrentSurge) surge = e;
        if (surge != _surge) Begin(world, surge);
        if (_surge is null) return;

        float strength = _surge.Envelope(world.Time);
        _flow += (_surge.Reverse ? -1f : 1f) * _surge.Speed * strength * Pace * dt;
        _material.SetShaderParameter(SN.Of("strength"), strength);
        _material.SetShaderParameter(SN.Of("flow"), _flow);
    }

    /// <summary>The ribbon laid along the surge's canyon: its course, out to where the current is only seen.</summary>
    void Begin(PlaneWorld world, ReefEvent? surge)
    {
        _surge = surge;
        _ribbon.Visible = surge is not null;
        _flow = 0f;
        if (surge is null) return;
        var corridor = world.Map.Corridors[surge.Corridor];
        var course = corridor.Points;
        float reach = corridor.HalfWidth + ReefDirectorTuning.CosmeticReach;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var indices = new List<int>();
        float s = 0f;
        for (int i = 0; i < course.Count; i++)
        {
            if (i > 0) s += System.Numerics.Vector2.Distance(course[i - 1], course[i]);
            // Across the course at this point: the mean of the segments either side, widened at a bend so the ribbon
            // keeps its width round it (within reason at a sharp one).
            var into = i > 0 ? Dir(course[i - 1], course[i]) : Dir(course[0], course[1]);
            var outOf = i < course.Count - 1 ? Dir(course[i], course[i + 1]) : into;
            var mean = into + outOf;
            mean = mean.LengthSquared() > 1e-6f ? System.Numerics.Vector2.Normalize(mean) : into;
            var side = new System.Numerics.Vector2(-mean.Y, mean.X);
            float miter = 1f / Mathf.Max(System.Numerics.Vector2.Dot(mean, into), 0.5f);
            foreach (float k in new[] { -1f, 1f })
            {
                var p = course[i] + side * (k * reach * miter);
                verts.Add(new Vector3(p.X, LevelMap.SwimBand + Height, p.Y));
                uvs.Add(new Vector2(s, k < 0f ? 0f : 1f));
            }
            if (i > 0)
            {
                int v = (i - 1) * 2;
                indices.AddRange(new[] { v, v + 2, v + 1, v + 1, v + 2, v + 3 });
            }
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh();
        if (indices.Count > 0) mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        _ribbon.Mesh = mesh;
        _material.SetShaderParameter(SN.Of("total"), Mathf.Max(s, 1f));
        _material.SetShaderParameter(SN.Of("half_width"), reach);
        _material.SetShaderParameter(SN.Of("channel"), corridor.HalfWidth / reach);
        _material.SetShaderParameter(SN.Of("strength"), 0f);
    }

    static System.Numerics.Vector2 Dir(System.Numerics.Vector2 a, System.Numerics.Vector2 b)
    {
        var d = b - a;
        return d.LengthSquared() > 1e-8f ? System.Numerics.Vector2.Normalize(d) : System.Numerics.Vector2.UnitX;
    }
}
