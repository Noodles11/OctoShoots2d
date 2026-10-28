// A corruption vent's goo (docs/ADVENTURE.md, AdventureView): a gash torn in the reef wall, plugged by a blister of
// thick ink goo bulging out of it into the water. Its skin is an oily, iridescent film (thin-film colours sliding over
// a near-black, marbled like an oil slick, brightest where it is seen edge-on), wet and glossy; through it the inside of
// the reef shows deep down: a current running along the gash, pushing streaks and specks of corruption toward the
// mouth. It breathes, ripples run along it, and her hits dent it and ring out from where they struck. Her hits so far
// light it gold, creeping in from its torn edges (lit); sealed, it dims away (shut).
// The mesh (AdventureView.GooMesh): UV.x along the gash (0..1), UV.y across it (top 0 → bottom 1); COLOR.r how far
// inside its edge a point is (0 the edge → 1 its middle); local +Y out of the wall, X along it.
shader_type spatial;
render_mode cull_disabled, shadows_disabled;

instance uniform float seed = 0.0;
instance uniform float lit = 0.0;
instance uniform float shut = 0.0;
/// Her last hit: where on it (UV), and how long ago (s).
instance uniform vec3 hit = vec3(0.5, 0.5, 100.0);
/// The gash's size (m): along it, across it.
uniform vec2 size = vec2(2.0, 1.2);
uniform vec3 gold : source_color = vec3(1.0, 0.72, 0.3);

varying float v_inside;
varying vec2 v_uv;

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

float fbm(vec2 p) {
	return 0.55 * noise2(p) + 0.3 * noise2(p * 2.1 + 3.7) + 0.15 * noise2(p * 4.3 + 9.1);
}

void vertex() {
	v_inside = COLOR.r;
	v_uv = UV;
	float r = COLOR.r;
	vec2 at = UV * size;
	// It breathes, slow and heavy, and ripples run along it.
	float swell = 0.06 * sin(TIME * 1.1 + seed * 5.0) + 0.03 * sin(at.x * 3.5 - TIME * 1.8 + seed);
	// Her hit: a dent where it struck, ringing out over it.
	float age = hit.z;
	float d = length(at - hit.xy * size);
	float dent = age < 2.0 ? -0.22 * exp(-age * 5.0) * exp(-d * d / 0.18) + 0.07 * sin(d * 12.0 - age * 14.0) * exp(-age * 3.0) * exp(-d * 0.8) : 0.0;
	VERTEX.y += (swell + dent) * pow(r, 0.7) * (1.0 - shut);
}

void fragment() {
	if (!FRONT_FACING) NORMAL = -NORMAL;
	float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	float fresnel = pow(1.0 - facing, 2.5);
	vec2 at = v_uv * size;
	// The inside of the reef, deep under the film (a little parallax, so it lies below the skin): a current running
	// along the gash, its streaks and specks of corruption pushed toward the mouth (its middle).
	vec2 deep = at + (VIEW.xy) * 0.25;
	float toward = sign(0.5 - v_uv.x);
	vec2 q = vec2(deep.x * 2.2 + toward * TIME * 0.9, deep.y * 5.0 + 0.6 * sin(deep.x * 1.7 + TIME * 0.6 + seed));
	float streak = smoothstep(0.62, 0.92, fbm(q));
	vec2 cell = vec2(deep.x * 9.0 + toward * TIME * 2.2, deep.y * 9.0);
	float speck = step(0.93, hash2(floor(cell) + seed)) * smoothstep(0.5, 0.1, length(fract(cell) - 0.5));
	vec3 inside = vec3(0.02, 0.006, 0.04) + vec3(0.42, 0.12, 0.8) * streak * 0.55 + vec3(1.0, 0.25, 0.75) * speck * 0.9;
	// Brighter deep in its middle, dark at its torn edge, where it meets the rock.
	inside *= smoothstep(0.0, 0.35, v_inside);
	// The film: thin-film colours sliding over it, marbled like an oil slick.
	float marble = fbm(at * 1.3 + vec2(TIME * 0.05, -TIME * 0.035) + seed);
	float swirl = fbm(at * 3.0 + marble * 2.5 - TIME * 0.08);
	float t = facing * 1.4 + marble * 1.1 + swirl * 0.4 + TIME * 0.02;
	vec3 film = 0.5 + 0.5 * cos(6.2831853 * (t + vec3(0.0, 0.33, 0.67)));
	// Toward the Leak's own colours: violet, teal, magenta, a touch of gold.
	film = mix(film, film * vec3(0.75, 0.5, 1.0) + vec3(0.04, 0.0, 0.08), 0.6);
	float sheen = (0.18 + 0.82 * fresnel) * (0.55 + 0.45 * smoothstep(0.35, 0.75, swirl));
	ALBEDO = vec3(0.01, 0.006, 0.016) + film * 0.05;
	ROUGHNESS = 0.12;
	SPECULAR = 0.75;
	vec3 glow = inside * (1.0 - 0.6 * sheen) + film * sheen * 0.55;
	// Her hits so far: thin veins of gold light creeping in from its torn edges, the further the more she has struck.
	float reach = v_inside + 0.2 * (noise2(at * 3.0 + seed) - 0.5);
	float within = lit > 0.0 ? 1.0 - smoothstep(lit - 0.1, lit, reach) : 0.0;
	float ridge = 1.0 - abs(2.0 * fbm(at * 3.5 + seed * 1.3) - 1.0);
	float veins = smoothstep(0.93, 0.99, ridge) + 0.5 * smoothstep(0.08, 0.0, v_inside);
	glow += gold * within * veins * (0.75 + 0.25 * sin(TIME * 5.0 + at.x * 3.0)) * 0.9;
	EMISSION = glow * (1.0 - shut);
}
