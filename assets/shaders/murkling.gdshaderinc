// A murkling (docs/CORRUPTION.md; BlightView.MurkBody): a drop of ink adrift, liquid as mercury in no gravity, painted.
// Its shape is a soft body the view simulates: lobes[12] are how far each of twelve points round it (the icosahedron's
// corners) stands out of round, so it bulges where it lags, dents where it is struck and rings back, its volume kept.
// Her hits tear holes in it (holes[6]: a direction and how wide, radians), ragged, an ember glow at their edges and its
// lit inside showing through; they close as its corruption grows back. Painted like her light is (light_stroke): its ink
// brushed in flowing bands, a ragged violet rim laid as a dry-brushed stroke, a torn silhouette, one soft dab of sheen.
// The throw (pull, bud) and its own throws (drip) are shaped as corruption_orb shapes them. worn, hit: her outline.
// Also the Blightroot's painted parts (BlightView.Octopus): skin (rgb, a how much) is a kind's own colour under the ink
// and in its rim (the Blightroot's blue; freed, the octopus's own healthy red), swell its head swelling on the pulse.

uniform float lobes[12];
uniform vec4 holes[6];
instance uniform float ember = 0.4;
instance uniform float seed = 0.0;
instance uniform float fade = 1.0;
uniform vec3 ember_color : source_color = vec3(1.0, 0.42, 0.16);
instance uniform vec4 ember_tint = vec4(0.0);
uniform vec3 rim_color : source_color = vec3(0.34, 0.09, 0.6);
instance uniform vec3 aim = vec3(0.0, 0.0, 1.0);
instance uniform float pull = 0.0;
instance uniform float bud = 0.0;
instance uniform float drip = 0.0;
instance uniform float hit = 0.0;
instance uniform float worn = 0.0;
instance uniform vec4 skin = vec4(0.0);
instance uniform float swell = 0.0;
/// 1: a body with an inside (seen through its holes); 0: a solid part (an arm, a rootlet) shaded the same on both sides.
instance uniform float hollow = 1.0;
#ifdef OVER
#include "res://assets/shaders/over_her.gdshaderinc"
varying vec3 v_world;
#endif

varying vec3 v_dir;

const vec3 DIRS[12] = vec3[12](
	vec3(-0.5257, 0.8507, 0.0), vec3(0.5257, 0.8507, 0.0), vec3(-0.5257, -0.8507, 0.0), vec3(0.5257, -0.8507, 0.0),
	vec3(0.0, -0.5257, 0.8507), vec3(0.0, 0.5257, 0.8507), vec3(0.0, -0.5257, -0.8507), vec3(0.0, 0.5257, -0.8507),
	vec3(0.8507, 0.0, -0.5257), vec3(0.8507, 0.0, 0.5257), vec3(-0.8507, 0.0, -0.5257), vec3(-0.8507, 0.0, 0.5257));

float hash3(vec3 p) { return fract(sin(dot(p, vec3(12.9898, 78.233, 37.719))) * 43758.5453); }

float hash2(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float noise2(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash2(i), hash2(i + vec2(1.0, 0.0)), f.x), mix(hash2(i + vec2(0.0, 1.0)), hash2(i + vec2(1.0, 1.0)), f.x), f.y);
}

/// How far into its nearest hole a point lies (> 0 inside), its edge torn.
float hole_at(vec3 p, out float rim) {
	float best = -1.0;
	rim = 0.0;
	for (int i = 0; i < 6; i++) {
		vec4 h = holes[i];
		if (h.w <= 0.0) continue;
		float ang = acos(clamp(dot(p, h.xyz), -1.0, 1.0));
		// A torn edge: it wanders round the hole.
		vec3 t = normalize(cross(h.xyz, abs(h.y) < 0.9 ? vec3(0.0, 1.0, 0.0) : vec3(1.0, 0.0, 0.0)));
		vec3 b = cross(h.xyz, t);
		float around = atan(dot(p, b), dot(p, t));
		float torn = h.w * (0.8 + 0.35 * noise2(vec2(around * 2.5 + float(i) * 7.0 + seed, h.w * 3.0)));
		float into = torn - ang;
		if (into > best) best = into;
		rim = max(rim, 1.0 - smoothstep(0.0, 0.12, abs(into)));
	}
	return best;
}

void vertex() {
	vec3 p = normalize(VERTEX);
	v_dir = p;
	// The soft body: each lobe's standing out (or in), blended smoothly round it.
	float sum = 0.0, wsum = 0.0;
	for (int i = 0; i < 12; i++) {
		float w = exp(7.0 * (dot(p, DIRS[i]) - 1.0));
		sum += lobes[i] * w;
		wsum += w;
	}
	VERTEX += NORMAL * (sum / max(wsum, 1e-4) + swell * 0.18);
	// A torn hole's lip rolls in a little.
	float rim;
	float into = hole_at(p, rim);
	VERTEX -= NORMAL * 0.12 * smoothstep(-0.25, 0.0, into);
	if (pull != 0.0 || bud > 0.0 || drip > 0.0) {
		vec3 A = normalize(aim);
		float a = dot(p, A);
		vec3 lateral = VERTEX - A * dot(VERTEX, A);
		// The slingshot, bent in the water's plane (as corruption_orb bends it).
		vec3 P = normalize(vec3(-A.z, 0.0, A.x));
		float l = clamp(dot(VERTEX, P), -1.0, 1.0);
		float arch = 1.0 - l * l;
		float back = max(-pull, 0.0), fwd = max(pull, 0.0);
		VERTEX -= A * back * arch * (0.8 + 1.5 * max(a, 0.0));
		VERTEX -= A * dot(VERTEX, A) * back * 0.3;
		VERTEX += P * l * back * 0.3;
		VERTEX.y *= 1.0 - 0.15 * back;
		VERTEX += NORMAL * sin(TIME * 70.0 + seed * 9.0 + dot(p, vec3(5.0, 3.0, 4.0))) * 0.08 * back * back;
		VERTEX += A * fwd * arch * (0.8 + 1.4 * max(-a, 0.0));
		VERTEX -= A * dot(VERTEX, A) * fwd * 0.2;
		VERTEX += P * l * fwd * 0.12;
		// The piece of itself it throws, swelling out of its front: a knob on a narrowing neck.
		VERTEX += A * bud * 0.75 * smoothstep(0.55, 1.0, a);
		VERTEX -= lateral * bud * 0.5 * smoothstep(0.55, 0.85, a) * (1.0 - smoothstep(0.85, 1.0, a));
		float tail = smoothstep(0.2, 1.0, -a);
		VERTEX -= A * drip * 1.6 * tail * tail;
		VERTEX -= lateral * drip * 0.6 * tail;
	}
#ifdef OVER
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
#endif
}

void fragment() {
	vec3 p = normalize(v_dir);
	if (fade < 1.0 && hash3(floor(v_dir * 9.0) + seed) > fade) discard;
#ifdef OVER
	float over = over_her(v_world, VERTEX.z, VIEW_MATRIX);
	if (over < 0.01) discard;
#endif
	// Her hits' holes: open through it.
	float rim_h;
	float into = hole_at(p, rim_h);
	if (into > 0.0) discard;
	vec3 glow = ember_tint.a > 0.5 ? ember_tint.rgb : ember_color;
	float flicker = 0.7 + 0.3 * sin(TIME * 7.0 + seed * 3.0) * sin(TIME * 4.3 + seed);
	if (!FRONT_FACING && hollow > 0.5) {
		// Its inside, seen through a hole: the stolen ember's light in the ink.
		ALBEDO = vec3(0.02, 0.01, 0.02);
		ROUGHNESS = 1.0;
		SPECULAR = 0.0;
		// Deep in it the ember glows; toward the torn edge the ink is dark.
		float depth = clamp(dot(-NORMAL, VIEW), 0.0, 1.0);
		EMISSION = glow * (0.08 + 0.55 * ember * depth * depth) * flicker + rim_color * 0.08;
	} else {
		if (!FRONT_FACING) NORMAL = -NORMAL;
		float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
		vec3 n = (INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz;
		// A torn silhouette: the outline broken by the brush.
		float around = atan(n.z, n.x);
		float tear = 0.93 + 0.05 * noise2(vec2(around * 5.0 + seed, TIME * 0.6));
		if (1.0 - facing > tear) discard;
		// The ink brushed in flowing bands round it, each stroke a shade apart.
		float lon = atan(p.z, p.x);
		float band = noise2(vec2(lon * 3.0 + TIME * 0.25 + seed, p.y * 9.0));
		float bristle = noise2(vec2(lon * 14.0 + seed * 3.0, p.y * 3.0 + TIME * 0.4));
		ALBEDO = vec3(0.012, 0.01, 0.02) + vec3(0.03, 0.012, 0.04) * (band * 0.7 + bristle * 0.3);
		// A kind's own colour under the ink, brushed the same way.
		ALBEDO = mix(ALBEDO, skin.rgb * (0.22 + 0.18 * band + 0.08 * bristle), skin.a);
		vec3 rim = mix(rim_color, min(skin.rgb, vec3(1.0)), skin.a);
		ROUGHNESS = 0.78;
		SPECULAR = 0.2;
		// The rim: a violet stroke laid round it, its inner edge ragged, dry-brushed.
		float edge = 0.5 + 0.14 * noise2(vec2(around * 6.0 + seed * 2.0, TIME * 0.5));
		float stroke = smoothstep(edge, edge + 0.12, 1.0 - facing);
		float dry = 0.55 + 0.6 * noise2(vec2(around * 18.0 + seed, (1.0 - facing) * 6.0));
		EMISSION = rim * stroke * dry * (0.55 + 0.6 * skin.a + swell * 0.8) + glow * pow(facing, 6.0) * ember * flicker * 1.4;
		// One soft dab of sheen, as a painter leaves on a drop.
		float dab = smoothstep(0.86, 0.97, dot(n, normalize(vec3(-0.45, 0.8, 0.4)))) * (0.6 + 0.4 * noise2(vec2(lon * 4.0, p.y * 4.0 + seed)));
		EMISSION += vec3(0.42, 0.36, 0.55) * dab * 0.18;
		// A torn hole's lip glows with the ember behind it.
		EMISSION += glow * rim_h * (0.6 + 0.4 * flicker) * 1.2;
		// Her light's outline (as corruption_orb draws it).
		if (worn > 0.0 || hit > 0.0) {
			float e = mix(0.72, 0.42, clamp(worn, 0.0, 1.0));
			float line = smoothstep(e, e + 0.2, 1.0 - facing);
			float strength = (worn > 0.0 ? 1.1 + 0.9 * worn : 0.0) + hit * 3.0;
			EMISSION += vec3(1.0, 0.93, 0.75) * line * strength;
		}
	}
#ifdef OVER
	// Drawn after the water's pass (and her), so hazed by hand to match it: matte (no sun glint the water would
	// have softened), its colour taken toward the water's.
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
	ALBEDO = over_haze(ALBEDO, 0.3);
	EMISSION = mix(EMISSION * vec3(0.72, 0.92, 1.03), reef_water_deep * 0.2, 0.2);
	ALPHA = over;
#endif
}
