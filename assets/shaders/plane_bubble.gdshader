// Clementine's bubbles: nearly clear in the middle, bright and faintly iridescent at the rim (fresnel), with a small
// specular glint. `fade` (per instance) fades a bubble out; `rainbow` (a bubble merged to the cap) sweeps bright
// spectral bands over it.
// A bubble is a film, not a solid orb: `wobble` ripples its surface in a few slow, low modes (`seed` sets each one's
// own phase), livelier as it moves. `hole` pops it the way a real one goes: the film tears open at a point (xyz, a
// unit direction from the centre, in world axes) and the hole's rim races across it (w: 0 whole → 1 gone), the
// rim bright where the torn film gathers. `ink` (Ink Sac) fills it with a dark violet heart under a violet rim.
// Light bubbles (docs/LIGHT-BUBBLES.md): each of hers carries a small warm lantern inside the film. `lantern` is the
// lantern's brightness (0: a plain film, as the shield and other bubbles), `warmth` shifts it from her gold (#FFC46B,
// the gonads' gold) toward white-gold as bubbles merge. `base_light` lights every instance at once (particles).
// The core is capped below her bell's peak: she stays the warmest light on screen.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, unshaded, shadows_disabled;

instance uniform float fade = 1.0;
instance uniform float rainbow = 0.0;
instance uniform float ink = 0.0;
instance uniform float seed = 0.0;
instance uniform float wobble = 0.0;
instance uniform vec4 hole = vec4(0.0, 1.0, 0.0, 0.0);
instance uniform float lantern = 0.0;
instance uniform float warmth = 0.0;
uniform float base_light = 0.0;
uniform vec3 gold : source_color = vec3(1.0, 0.769, 0.420);
const float CORE_MAX = 1.12;

varying vec3 dir_local;

void vertex() {
	// The mesh is a unit sphere: VERTEX is its own direction from the centre.
	dir_local = VERTEX;
	vec3 p = VERTEX;
	float t = TIME;
	float d = sin(t * 6.3 + seed * 5.1) * p.x * p.z * 1.2
		+ sin(t * 5.1 + seed * 2.7) * (p.x * p.x - p.z * p.z) * 0.9
		+ sin(t * 7.7 + seed * 1.3) * p.y * p.x * 0.8
		+ sin(t * 9.4 + seed * 3.9) * p.x * p.y * p.z * 2.0
		+ sin(t * 4.2 + seed * 0.7) * (3.0 * p.y * p.y - 1.0) * 0.35;
	VERTEX += NORMAL * d * 0.075 * wobble;
}
uniform vec3 tint : source_color = vec3(0.86, 0.95, 1.0);

void fragment() {
	// The tear: everything within the hole's reach of the struck point is gone; just outside it the rim shines.
	float gap = 0.0;
	if (hole.w > 0.0) {
		float reach = 1.0 - 2.0 * hole.w;
		gap = dot(normalize(dir_local), normalize(hole.xyz)) - reach;
		if (gap > 0.0) discard;
	}
	float torn_rim = hole.w > 0.0 ? 1.0 - smoothstep(0.0, 0.22, -gap) : 0.0;
	float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.2);
	vec3 irid = 0.5 + 0.5 * cos(6.2831 * (rim * 1.4 + vec3(0.0, 0.33, 0.67)));
	float glint = smoothstep(0.9, 0.97, dot(NORMAL, normalize(vec3(-0.45, 0.55, 0.7))));
	vec3 n = (INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz;
	vec3 spectrum = 0.5 + 0.5 * cos(6.2831 * (n.x * 0.6 + n.z * 0.4 + rim * 0.8 - TIME * 0.6 + vec3(0.0, 0.33, 0.67)));
	vec3 base = mix(tint, irid, 0.3 * rim);
	ALBEDO = mix(base, spectrum * 1.25, rainbow * (0.45 + 0.5 * rim)) + vec3(glint);
	ALPHA = clamp((0.07 + 0.8 * rim + glint + rainbow * 0.22) * fade, 0.0, 1.0);
	// Ink: a swirling dark heart, the rim tinged violet.
	float swirl = 0.5 + 0.5 * sin(n.x * 9.0 + n.z * 7.0 + TIME * 3.0);
	vec3 heart = mix(vec3(0.07, 0.02, 0.12), vec3(0.3, 0.1, 0.45), swirl * 0.6);
	ALBEDO = mix(ALBEDO, mix(heart, vec3(0.75, 0.45, 1.0), rim), ink);
	ALPHA = mix(ALPHA, clamp((0.72 - 0.3 * rim + 0.6 * rim * rim + glint) * fade, 0.0, 1.0), ink);
	// The lantern: a hot point in the middle (facing the eye), a soft warm fill through the film, the rim left to
	// the iridescence. A maxed bubble is a small white-gold star; the rainbow above becomes its corona.
	float lamp = lantern + base_light;
	if (lamp > 0.0) {
		float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
		float core = pow(facing, 5.0);
		float fill = pow(facing, 1.6);
		vec3 hue = mix(gold, vec3(1.0, 0.95, 0.84), clamp(warmth, 0.0, 1.0));
		vec3 glow = hue * (1.15 * core + 0.32 * fill) * lamp * (1.0 - 0.75 * ink);
		ALBEDO = min(ALBEDO * (1.0 - 0.6 * fill * min(lamp, 1.0)) + glow, vec3(CORE_MAX));
		ALPHA = max(ALPHA, clamp((0.95 * core + 0.3 * fill) * min(lamp * 1.5, 1.0), 0.0, 1.0) * fade);
	}
	ALBEDO = mix(ALBEDO, vec3(1.0, 1.0, 0.97), torn_rim * 0.85);
	ALPHA = max(ALPHA, torn_rim * 0.9 * fade);
}
