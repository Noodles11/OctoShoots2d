// A generated jelly or siphonophore's bell (docs/BOSS-GENERATOR.md §5, §13): milky and translucent, the light
// catching its rim, its gonads (COLOR.g) showing through. Corrupted, it is drained toward a dull grey-milk and the ink
// (COLOR.r, from the mutation sites) clouds it from inside — opaque, glossy, violet at the rim. Same instance
// parameters as boss_body.
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_always;

instance uniform float drained = 1.0;
instance uniform float flash = 0.0;
instance uniform float shine = 0.8;
instance uniform float surge = 0.0;
// Fading out as it leaves (1 → 0).
instance uniform float fade = 1.0;
uniform vec3 body_color : source_color = vec3(0.85, 0.88, 0.95);
uniform vec3 accent_color : source_color = vec3(0.95, 0.55, 0.7);
uniform vec3 ink : source_color = vec3(0.022, 0.016, 0.03);
uniform vec3 rim_color : source_color = vec3(0.5, 0.32, 0.85);

varying vec3 baked;
varying vec3 wpos;

float hash(vec3 p) { return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453); }
float noise(vec3 p) {
	vec3 i = floor(p);
	vec3 f = fract(p);
	vec3 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash(i), hash(i + vec3(1, 0, 0)), u.x), mix(hash(i + vec3(0, 1, 0)), hash(i + vec3(1, 1, 0)), u.x), u.y),
		mix(mix(hash(i + vec3(0, 0, 1)), hash(i + vec3(1, 0, 1)), u.x), mix(hash(i + vec3(0, 1, 1)), hash(i + vec3(1, 1, 1)), u.x), u.y), u.z);
}

void vertex() {
	baked = COLOR.rgb;
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float stain = baked.r, accent = baked.g;
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.0);
	vec3 c = mix(body_color, accent_color, clamp(accent, 0.0, 1.0));
	float grey = dot(c, vec3(0.3, 0.59, 0.11));
	c = mix(c, vec3(grey * 0.7), drained * 0.8);
	float n = noise(wpos * 2.0 + vec3(0.0, TIME * 0.15, 0.0));
	float inked = smoothstep(0.3, 0.62, stain + surge * 0.25 + (n - 0.5) * 0.4) * drained;
	c = mix(c, ink, inked);
	c = mix(c, vec3(1.0), clamp(flash, 0.0, 1.0));
	ALBEDO = c;
	ROUGHNESS = mix(0.25, 0.1, inked);
	SPECULAR = 0.6;
	// Clear in the middle of the bell, milky at the rim; the gonads and the ink are opaque.
	ALPHA = clamp((0.34 + 0.5 * fres + 0.5 * accent + 0.8 * inked) * fade, 0.0, 0.95);
	EMISSION = rim_color * fres * inked * 0.4 + (1.0 - drained) * body_color * fres * 0.08 + vec3(flash * 0.4);
}
