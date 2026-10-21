// The corruption's mark on the reef (docs/CORRUPTION.md): ink dripping upward out of the ground, as fine as plankton.
// Each drop, from its own spot on the floor, on its own loop (its own period and phase, so the drift never pulses
// together):
//   swell  (0 → 0.28): a bead of ink wells up out of the floor and swells, sitting on it;
//   stretch (→ 0.42): it lifts, drawing a neck of ink up behind it from the ground that thins as it goes;
//   pinch  (0.42): the neck snaps, the drop springs round (a small wobble);
//   rise   (→ 1): buoyant, it rises, slow off the mark, quicker, then easing, with a small sway, and fades near the top;
// then another wells up. A drop is a camera-facing quad shaped and shaded here (a sphere with a neck), half clear. It
// takes no light, shading or fog from the scene and is drawn after the water (topdown_post, the first thing in the
// transparent pass), so nothing tints or blurs it: its roundness is its own (a fixed soft key, a cold violet rim, a
// glint), as if from another dimension (rock in front still hides it: it tests the depth). It is seen out of phase, a
// fixed chromatic aberration: its red is printed a little to one side and its cyan to the other, so every drop wears
// a red fringe on its left and a cyan one on its right. Where the ground is
// cleansed a spot's drops stop coming; the last of them light up in the gold of corruption dying as they go (only the
// drops glow; the ground does not). `fade` brings a level's whole drift in and out (shown, the dive).
// One MultiMesh instance per drop: its transform at the floor (its scale is the drop's radius); instance custom:
// (seed, u, v, how high it rises).
shader_type spatial;
render_mode unshaded, cull_disabled, world_vertex_coords, shadows_disabled, fog_disabled, depth_draw_never;

uniform sampler2D corruption_map : filter_linear, repeat_disable;
uniform vec3 ink : source_color = vec3(0.025, 0.008, 0.045);
uniform vec3 gold : source_color = vec3(1.0, 0.72, 0.32);
uniform vec3 rim_color : source_color = vec3(0.42, 0.18, 0.72);
/// The whole drift's presence (1 shown; 0 gone: before it fades in, and through the dive).
uniform float fade = 1.0;
/// How opaque a drop is at its fullest.
uniform float opacity = 0.7;
/// One drop's loop takes this long (seconds), give or take period_spread.
uniform float period = 5.5;
uniform float period_spread = 2.0;
/// The chromatic aberration: how far the red and the cyan are printed apart (in units of a drop's radius, each way),
/// and how strongly their fringes show.
uniform float split = 0.2;
uniform float fringe = 0.75;
uniform vec3 fringe_red : source_color = vec3(1.0, 0.16, 0.3);
uniform vec3 fringe_cyan : source_color = vec3(0.1, 0.9, 1.0);
/// The quad is this much wider than the drop, so the fringes fit (a split up to 0.25).
const float WIDE = 1.5;

varying float v_burn;
varying float v_show;
// The drop's frame in units of its radius: x across, y up from the body's centre; the quad spans y0 → y0 + span.
varying float v_y0;
varying float v_span;
varying float v_neck;
varying float v_squash;

void vertex() {
	vec4 c = INSTANCE_CUSTOM;
	vec2 fr = textureLod(corruption_map, c.yz, 0.0).rg;
	v_burn = fr.g;
	vec3 base = MODEL_MATRIX[3].xyz;
	float radius = length(MODEL_MATRIX[0].xyz);
	// How much corruption is here (none: no drops; burning: the last drops lit gold).
	float here = max(smoothstep(0.03, 0.55, fr.r), fr.g * 0.6);
	v_show = 0.0;
	v_neck = 0.0;
	v_squash = 1.0;
	v_y0 = -1.0;
	v_span = 2.0;
	if (here <= 0.001) {
		VERTEX = base;
	} else {
		float seed = c.x;
		float p = period + period_spread * (fract(seed * 7.13) - 0.5) * 2.0;
		float t = fract(TIME / p + seed);
		float r = radius * mix(0.6, 1.0, here);
		// Swell: the bead grows out of the floor (its centre at its own radius above it).
		float swell = smoothstep(0.0, 0.28, t);
		float rb = r * mix(0.15, 1.0, swell);
		// Stretch: it lifts off, drawing the neck up behind it; then the neck snaps.
		float pull = smoothstep(0.28, 0.42, t);
		float released = step(0.42, t);
		float neck = (1.0 - released) * swell;
		// Rise: buoyant, slow off the mark, quicker, easing near the top (ease-in-out), from the pinch point.
		float k = clamp((t - 0.42) / 0.58, 0.0, 1.0);
		float rise = c.w * (k * k * (3.0 - 2.0 * k));
		float lift = rb + pull * r * 1.6 + rise;
		// The spring round after the pinch: a quick squash-and-stretch that settles.
		float since = max(t - 0.42, 0.0) * p;
		float wobble = released * exp(-since * 9.0) * sin(since * 38.0) * 0.25;
		// While stretching the drop is pulled long (taller, narrower).
		float stretch = 1.0 + (1.0 - released) * pull * 0.35 + wobble;
		v_squash = stretch;
		// Fades at the top of its rise.
		float fading = 1.0 - smoothstep(0.78, 1.0, t);
		v_show = smoothstep(0.0, 0.06, t) * fading;
		v_neck = neck;
		// The quad: from the floor (while the neck holds) or just under the drop, to just over it.
		float bottom = neck > 0.001 ? -lift / rb : -1.0 * stretch;
		float top = 1.0 * stretch;
		v_y0 = bottom;
		v_span = top - bottom;
		// A little sway as it rises (the drift, not the current).
		float ph = seed * 6.2831;
		vec3 sway = vec3(sin(TIME * 0.9 + ph), 0.0, cos(TIME * 0.7 + ph * 1.3)) * 0.1 * k;
		// Facing the camera, its "up" the world's up as the camera sees it.
		vec3 fwd = INV_VIEW_MATRIX[2].xyz;
		vec3 up = vec3(0.0, 1.0, 0.0) - fwd * fwd.y;
		up = length(up) > 0.15 ? normalize(up) : INV_VIEW_MATRIX[1].xyz;
		vec3 right = normalize(cross(up, fwd));
		float x = (UV.x * 2.0 - 1.0) * rb * WIDE;
		float y = (bottom + (1.0 - UV.y) * (top - bottom)) * rb;
		vec3 centre = base + vec3(0.0, lift, 0.0) + sway;
		VERTEX = centre + right * x + up * y;
	}
}

// Whether a point (x across, y up from its centre, in units of its radius) lies in the drop: its body, a sphere pulled
// a little long while it stretches, or its neck, down to the floor below the body, thick at the ground and thinning
// toward the drop.
bool in_drop(float x, float y) {
	vec2 q = vec2(x * v_squash, y / v_squash);
	if (dot(q, q) < 1.0) return true;
	if (v_neck > 0.001 && y < 0.0) {
		float along = clamp((y - v_y0) / max(-v_y0 - 0.9, 0.01), 0.0, 1.0);
		return abs(x) < v_neck * mix(0.5, 0.12, along);
	}
	return false;
}

void fragment() {
	// Where in the drop: x across, y up from its centre, in units of its radius.
	float x = (UV.x * 2.0 - 1.0) * WIDE;
	float y = v_y0 + (1.0 - UV.y) * v_span;
	// Out of phase: the red print lies to the left, the cyan to the right; where only one covers, its fringe shows.
	bool here = in_drop(x, y);
	bool red = in_drop(x + split, y), cyan = in_drop(x - split, y);
	if (!here && red == cyan) discard;
	if (!here) {
		ALBEDO = red ? fringe_red : fringe_cyan;
		ALPHA = clamp(opacity * v_show * fade * fringe, 0.0, 1.0);
	} else {
		vec2 q = vec2(x * v_squash, y / v_squash);
		float r2 = dot(q, q);
		bool body = r2 < 1.0;
		vec3 n = body ? vec3(q, sqrt(max(1.0 - r2, 0.0))) : normalize(vec3(x * 2.0, 0.0, 1.0));
		// Its own roundness: a soft key from the upper left, a cold violet rim, a small glint (view space).
		vec3 key = normalize(vec3(-0.45, 0.6, 0.65));
		float shade = 0.45 + 0.55 * clamp(dot(n, key), 0.0, 1.0);
		float rim = pow(1.0 - n.z, 2.0);
		float glint = pow(clamp(dot(reflect(-key, n), vec3(0.0, 0.0, 1.0)), 0.0, 1.0), 24.0);
		vec3 col = ink * 2.6 * shade + rim_color * rim * 0.7 + vec3(0.85, 0.75, 1.0) * glint * 0.6;
		// Its own edges where only one print covers: tinted toward that print's colour.
		if (!red) col = mix(col, fringe_cyan * 0.7, 0.5 * fringe);
		else if (!cyan) col = mix(col, fringe_red * 0.7, 0.5 * fringe);
		// Cleansed: the drop itself lights up gold (the ground under it does not).
		float lit = clamp(v_burn * 1.5, 0.0, 1.0);
		ALBEDO = mix(col, gold * (1.4 + 0.8 * glint), lit);
		ALPHA = clamp(opacity * v_show * fade * (0.85 + 0.25 * rim) + lit * 0.3 * v_show * fade, 0.0, 1.0);
	}
}
